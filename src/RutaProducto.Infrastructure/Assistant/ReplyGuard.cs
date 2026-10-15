using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Domain.Assistant;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Infrastructure.Assistant;

/// <summary>The outcome of the guard: a cleaned reply with its verdict, or the reason it was rejected.</summary>
internal sealed record ReplyCheck(TurnReply? Reply, ReplyVerdict Verdict, string? Rejection)
{
    public bool Accepted => Rejection is null;
}

/// <summary>
/// Deterministic checks every model reply passes before it is stored. A rejection is handled like malformed
/// output (script wording), so nothing here needs to be perfect: it only has to keep bad text out of the chat.
/// </summary>
internal static partial class ReplyGuard
{
    /// <param name="reply">The parsed model reply.</param>
    /// <param name="answerPrecedes">True when the turn follows a user answer, so an acknowledgement is required.</param>
    /// <param name="followUpAllowed">True when a NeedsMore verdict can be honoured; otherwise it is rejected.</param>
    /// <param name="ownedAcknowledgement">
    /// An acknowledgement written by code that replaces the model's one (for a "No sé" answer). The length check
    /// is made on the text that will actually be stored.
    /// </param>
    public static ReplyCheck Check(TurnReply reply, bool answerPrecedes, bool followUpAllowed, string? ownedAcknowledgement = null)
    {
        var clean = reply with
        {
            Acknowledgement = Strip(reply.Acknowledgement),
            Question = Strip(reply.Question),
            Example = Strip(reply.Example),
            Why = Strip(reply.Why),
        };

        ReplyVerdict? verdict = clean.Verdict.Trim().ToLowerInvariant() switch
        {
            "proceed" => ReplyVerdict.Proceed,
            "needs_more" => ReplyVerdict.NeedsMore,
            _ => null,
        };

        if (verdict is null)
        {
            return Reject("verdict");
        }

        if (clean.Question.Length == 0 || clean.Example.Length == 0 || clean.Why.Length == 0
            || (answerPrecedes && clean.Acknowledgement.Length == 0))
        {
            return Reject("empty-field");
        }

        if (ownedAcknowledgement is not null)
        {
            clean = clean with { Acknowledgement = ownedAcknowledgement };
        }

        var text = Assemble(clean);

        if (text.Length > Message.ContentMaxLength)
        {
            return Reject("too-long");
        }

        if (JargonPattern().IsMatch(Fold(text)))
        {
            return Reject("jargon");
        }

        if (!string.IsNullOrWhiteSpace(clean.SuggestedDepth)
            && !(Enum.TryParse<InitiativeDepth>(clean.SuggestedDepth.Trim(), ignoreCase: true, out var depth)
                 && Enum.IsDefined(depth)))
        {
            return Reject("depth");
        }

        if (verdict == ReplyVerdict.NeedsMore && !followUpAllowed)
        {
            return Reject("ineligible-follow-up");
        }

        return new ReplyCheck(clean, verdict.Value, null);
    }

    /// <summary>The stored text of a cleaned reply, in the layout the fake uses.</summary>
    public static string Assemble(TurnReply reply) =>
        TurnLayout.Compose(
            reply.Acknowledgement.Length == 0 ? string.Empty : reply.Acknowledgement.TrimEnd() + " ",
            reply.Question,
            reply.Example,
            reply.Why);

    private static ReplyCheck Reject(string reason) => new(null, ReplyVerdict.Proceed, reason);

    // Markup out, plain words in: links keep their label, tags, headings, list markers, emphasis and code vanish.
    private static string Strip(string? text)
    {
        var value = text ?? string.Empty;
        value = LinkPattern().Replace(value, "$1");
        value = TagPattern().Replace(value, string.Empty);
        value = HeadingPattern().Replace(value, string.Empty);
        value = ListMarkerPattern().Replace(value, string.Empty);
        value = EmphasisPattern().Replace(value, string.Empty);

        return WhitespacePattern().Replace(value, " ").Trim();
    }

    // Lower case without accents, so "Épicas" and "epica" meet the same word list.
    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.None, 1000)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"</?[A-Za-z][^>]*>", RegexOptions.None, 1000)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"^[ \t]{0,3}#{1,6}[ \t]+", RegexOptions.Multiline, 1000)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^[ \t]*(?:[-*+•]|\d+[.)])[ \t]+", RegexOptions.Multiline, 1000)]
    private static partial Regex ListMarkerPattern();

    [GeneratedRegex(@"\*\*|__|\*|`", RegexOptions.None, 1000)]
    private static partial Regex EmphasisPattern();

    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex WhitespacePattern();

    // Whole words (and plurals) of the vocabulary RF-41 bans; "espina dorsal" is not on the list.
    [GeneratedRegex(@"\b(?:spine|epica|invariante|slug)(?:s|es)?\b", RegexOptions.None, 1000)]
    private static partial Regex JargonPattern();
}
