using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Domain.Common;

namespace BmadPlatform.Application.Tests.Features.Artifacts.Markdown;

public sealed class QuestionOwnersTests
{
    private const string Default = " (Responsable: por asignar; Revisar: por definir)";

    [Theory]
    [InlineData("# T\n- **Q-1**: uno" + Default + "\n- **Q-2**: dos" + Default + "\nfin   ", "Q-1", "Legal", "mañana",
        "# T\n- **Q-1**: uno (Responsable: Legal; Revisar: mañana)\n- **Q-2**: dos" + Default + "\nfin   ")]
    [InlineData("- **Q-1**: uno" + Default + "\n- **Q-1**: repetida" + Default, "Q-1", "TI", "ya",
        "- **Q-1**: uno (Responsable: TI; Revisar: ya)\n- **Q-1**: repetida (Responsable: TI; Revisar: ya)")]
    [InlineData("- **Q-1**: uno" + Default, "Q-1", "TI", "  ", "- **Q-1**: uno (Responsable: TI; Revisar: por definir)")]
    [InlineData("- **Q-1**: uno" + Default, "Q-1", null, "x", "- **Q-1**: uno (Responsable: por asignar; Revisar: x)")]
    [InlineData("- **Q-1**: uno" + Default, "Q-1", "  Quien   propone ", "antes de", "- **Q-1**: uno (Responsable: Quien propone; Revisar: antes de)")]
    [InlineData("- **Q-1**: ¿Límite (diario)? (Responsable: a; Revisar: b)", "Q-1", "TI", "c", "- **Q-1**: ¿Límite (diario)? (Responsable: TI; Revisar: c)")]
    [InlineData("- **Q-1**: algo (ver anexo)", "Q-1", "TI", "c", "- **Q-1**: algo (ver anexo) (Responsable: TI; Revisar: c)")]
    [InlineData("**Q-1:** uno", "Q-1", "TI", "c", "**Q-1:** uno (Responsable: TI; Revisar: c)")]
    [InlineData("Q-1. uno" + Default, "Q-1", "TI", "c", "Q-1. uno (Responsable: TI; Revisar: c)")]
    [InlineData("- **Q-1**:", "Q-1", "TI", "c", "- **Q-1**: (Responsable: TI; Revisar: c)")]
    [InlineData("a\r\n- **Q-1**: uno" + Default + "\r\n- **Q-2**: dos" + Default + "\r\nz", "Q-2", "TI", "c",
        "a\r\n- **Q-1**: uno" + Default + "\r\n- **Q-2**: dos (Responsable: TI; Revisar: c)\r\nz")]
    [InlineData("```\n- **Q-1**: dentro\n```\n- **Q-1**: fuera", "Q-1", "TI", "c",
        "```\n- **Q-1**: dentro\n```\n- **Q-1**: fuera (Responsable: TI; Revisar: c)")]
    public void Only_the_targeted_lines_change_and_every_other_byte_stays(
        string markdown, string id, string? owner, string? revisit, string expected)
    {
        var result = QuestionOwners.Apply(markdown, [new QuestionAssignment(id, owner, revisit)]);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("- **Q-1**: uno" + Default + "\n- **Q-2**: dos")]
    [InlineData("- **Q-1**: uno (ver anexo)\n\n- **Q-1**: otra" + Default)]
    public void Applying_twice_equals_applying_once_and_the_current_values_return_the_input(string markdown)
    {
        QuestionAssignment[] assignments = [new("Q-1", "Finanzas", "antes de la arquitectura")];

        var once = QuestionOwners.Apply(markdown, assignments);

        Assert.Equal(once, QuestionOwners.Apply(once, assignments));
        Assert.Equal(once, QuestionOwners.Apply(once, [new QuestionAssignment("Q-1", "Finanzas", "antes de la arquitectura")]));
    }

    [Fact]
    public void The_parser_reads_back_what_was_written()
    {
        const string Markdown = "- **Q-1**: uno" + Default + "\n- **Q-2**: dos (ver anexo)\n- **Q-3**: tres" + Default;

        var result = QuestionOwners.Apply(Markdown, [new("Q-1", "Legal", "mañana"), new("Q-2", "TI", null)]);
        var questions = OpenQuestions.Parse(result);

        Assert.Equal(["Legal", "TI", "por asignar"], questions.Select(q => q.Owner));
        Assert.Equal(["mañana", "por definir", "por definir"], questions.Select(q => q.Revisit));
        Assert.Equal(["uno", "dos (ver anexo)", "tres"], questions.Select(q => q.Text));
        Assert.Equal(["Q-2", "Q-3"], OpenQuestions.Unassigned(result));
    }

    [Theory]
    [InlineData("- **Q-1**: uno" + Default, "Q-9")]
    [InlineData("```\n- **Q-9**: dentro\n```\n- **Q-1**: uno" + Default, "Q-9")]
    public void An_id_with_no_open_question_throws_and_nothing_is_returned(string markdown, string id)
    {
        var error = Assert.Throws<DomainException>(() => QuestionOwners.Apply(markdown, [new QuestionAssignment("Q-1", "TI", "x"), new QuestionAssignment(id, "TI", "x")]));

        Assert.Equal("La pregunta Q-9 no existe en este documento.", error.Message);
    }

    [Theory]
    [InlineData("Legal", true)]
    [InlineData("Quien propone la iniciativa", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("Legal (TI)", false)]
    [InlineData("Legal)", false)]
    [InlineData("a;b", false)]
    [InlineData("[ASSUMPTION", false)]
    [InlineData("a]", false)]
    [InlineData("*x*", false)]
    [InlineData("a|b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a\nb", false)]
    [InlineData("a\rb", false)]
    public void The_allowed_text_rule_rejects_the_characters_that_break_the_suffix(string? value, bool allowed) =>
        Assert.Equal(allowed, QuestionOwners.IsAllowed(value));
}
