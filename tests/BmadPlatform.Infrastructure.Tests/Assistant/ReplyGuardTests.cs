using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Infrastructure.Assistant;

namespace BmadPlatform.Infrastructure.Tests.Assistant;

public sealed class ReplyGuardTests
{
    private static TurnReply Good(
        string verdict = "proceed",
        string ack = "Entendido.",
        string question = "¿Quién va a usar esto?",
        string example = "Las vendedoras de las tiendas.",
        string why = "Así sabemos para quién se construye.",
        string? depth = null) =>
        new(verdict, ack, question, example, why, depth);

    // Pads the reason so the assembled text is exactly the requested length.
    private static TurnReply OfLength(int length)
    {
        var baseline = ReplyGuard.Assemble(Good(why: "x")).Length;

        return Good(why: new string('x', length - baseline + 1));
    }

    private sealed record Case(string Name, TurnReply Reply, bool AnswerPrecedes, bool FollowUpAllowed, string? Rejection);

    private static readonly Case[] AllCases = BuildCases();

    public static TheoryData<int> CaseNumbers() => [.. Enumerable.Range(0, AllCases.Length)];

    private static Case[] BuildCases() =>
    [
        new("a complete reply passes", Good(), true, false, null),
        new("empty question", Good(question: " "), true, false, "empty-field"),
        new("empty example", Good(example: ""), true, false, "empty-field"),
        new("empty reason", Good(why: "\n"), true, false, "empty-field"),
        new("empty acknowledgement after an answer", Good(ack: ""), true, false, "empty-field"),
        new("empty acknowledgement on the opening turn", Good(ack: ""), false, false, null),
        new("exactly 4,000 characters", OfLength(Message.ContentMaxLength), true, false, null),
        new("4,001 characters", OfLength(Message.ContentMaxLength + 1), true, false, "too-long"),
        new("Spine", Good(question: "¿Cuál es el Spine del producto?"), true, false, "jargon"),
        new("las épicas", Good(example: "Se parte en las épicas del plan."), true, false, "jargon"),
        new("un slug", Good(why: "Necesito un slug para el nombre."), true, false, "jargon"),
        new("invariantes", Good(ack: "Anoto las invariantes."), true, false, "jargon"),
        new("espina dorsal is plain words", Good(question: "¿Cuál es la espina dorsal de la idea?"), true, false, null),
        new("unknown verdict", Good(verdict: "Maybe"), true, false, "verdict"),
        new("unknown depth", Good(depth: "99"), true, false, "depth"),
        new("a named depth is accepted (never applied)", Good(depth: "Large"), true, false, null),
        new("an ineligible follow-up", Good(verdict: "needs_more"), true, false, "ineligible-follow-up"),
        new("an eligible follow-up", Good(verdict: "needs_more"), true, true, null),
    ];

    [Theory]
    [MemberData(nameof(CaseNumbers))]
    public void Each_rule_accepts_or_rejects(int number)
    {
        var (name, reply, answerPrecedes, followUpAllowed, rejection) = AllCases[number];
        var check = ReplyGuard.Check(reply, answerPrecedes, followUpAllowed);

        Assert.True(rejection == check.Rejection, $"{name}: expected {rejection ?? "accepted"} but got {check.Rejection ?? "accepted"}");
    }

    [Fact]
    public void Markdown_and_markup_are_stripped_before_the_text_is_stored()
    {
        var check = ReplyGuard.Check(
            Good(
                ack: "**Anotado**, gracias.",
                question: "# ¿Quién usa [la tienda](https://x.test) <b>hoy</b>?",
                example: "- Las `vendedoras`\n- Bodega",
                why: "1. Para saber *a quién* servimos"),
            answerPrecedes: true,
            followUpAllowed: false);

        Assert.True(check.Accepted);
        Assert.Equal(
            "Anotado, gracias. ¿Quién usa la tienda hoy?\n\nPor ejemplo: Las vendedoras Bodega\n\nPor qué lo pregunto: Para saber a quién servimos",
            ReplyGuard.Assemble(check.Reply!));
    }

    [Fact]
    public void Assembly_follows_the_layout_and_an_owned_acknowledgement_replaces_the_models()
    {
        var check = ReplyGuard.Check(Good(ack: "A", question: "Q", example: "E", why: "W"), true, false, "Dueño. ");

        Assert.Equal("Dueño. Q\n\nPor ejemplo: E\n\nPor qué lo pregunto: W", ReplyGuard.Assemble(check.Reply!));
    }
}
