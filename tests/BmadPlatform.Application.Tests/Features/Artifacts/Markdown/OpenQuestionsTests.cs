using BmadPlatform.Application.Features.Artifacts.Markdown;

namespace BmadPlatform.Application.Tests.Features.Artifacts.Markdown;

public sealed class OpenQuestionsTests
{
    [Theory]
    [InlineData("- **Q-1**: Falta presupuesto", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: ; Revisar: mañana)", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: Por Asignar; Revisar: mañana)", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: TI; Revisar: )", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: TI; Revisar: por definir)", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: TI)", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: por asignar; Revisar: por definir)", false)]
    [InlineData("- **Q-1**: Falta presupuesto (Responsable: TI; Revisar: mañana)", true)]
    [InlineData("- **Q-1**: Falta presupuesto (responsable: TI; revisar: mañana)   ", true)]
    public void A_question_is_assigned_only_with_both_values_filled_in(string line, bool assigned)
    {
        var question = Assert.Single(OpenQuestions.Parse("## Preguntas abiertas\n\n" + line + "\n"));

        Assert.Equal(assigned, question.IsAssigned);
        Assert.Equal(assigned, OpenQuestions.Unassigned(line).Count == 0);
    }

    [Theory]
    [InlineData("- **Q-2**: Falta el límite (¿diario?) (Responsable: TI; Revisar: mañana)", "Falta el límite (¿diario?)")]
    [InlineData("- **Q-2**: Falta el límite (¿diario?) (ver anexo) (Responsable: TI; Revisar: mañana)", "Falta el límite (¿diario?) (ver anexo)")]
    [InlineData("**Q-2:** Falta el límite (¿diario?) (Responsable: TI; Revisar: mañana)", "Falta el límite (¿diario?)")]
    [InlineData("Q-2. Falta el límite (¿diario?) (Responsable: TI; Revisar: mañana)", "Falta el límite (¿diario?)")]
    public void The_definition_shapes_are_read_and_inner_parentheses_stay_in_the_text(string line, string text)
    {
        var question = Assert.Single(OpenQuestions.Parse(line));

        Assert.Equal(("Q-2", text, "TI", "mañana"), (question.Id, question.Text, question.Owner, question.Revisit));
    }

    [Fact]
    public void A_trailing_group_that_is_not_the_suffix_leaves_the_question_unassigned()
    {
        var question = Assert.Single(OpenQuestions.Parse("- **Q-3**: Falta saber (ver anexo)"));

        Assert.Equal(("Falta saber (ver anexo)", null, null), (question.Text, question.Owner, question.Revisit));
        Assert.False(question.IsAssigned);
    }

    [Theory]
    [InlineData("```\n- **Q-9**: x\n```")]
    [InlineData("## Índice de suposiciones\n\n- **Q-9**: x")]
    [InlineData("## Q-9 como título")]
    [InlineData("Pendiente: ver Q-9.")]
    [InlineData("- **Q9**: x")]
    [InlineData("- **q-9**: x")]
    [InlineData("- **Q-09**: x")]
    [InlineData("- **Q-0**: x")]
    public void Fenced_code_the_assumption_index_headings_and_malformed_ids_are_not_questions(string markdown)
    {
        Assert.Empty(OpenQuestions.Parse(markdown));
        Assert.Empty(OpenQuestions.Unassigned(markdown));
    }

    [Fact]
    public void Questions_come_in_document_order_with_duplicates_kept_and_none_gives_an_empty_list()
    {
        var markdown = "- **Q-3**: c\r\n- **Q-1**: a (Responsable: TI; Revisar: x)\r\n- **Q-3**: d\r\n- **Q-2**: b";

        Assert.Equal(["Q-3", "Q-1", "Q-3", "Q-2"], OpenQuestions.Parse(markdown).Select(q => q.Id));
        Assert.Equal([1, 2, 3, 4], OpenQuestions.Parse(markdown).Select(q => q.Line));
        Assert.Equal(["Q-3", "Q-2"], OpenQuestions.Unassigned(markdown));
        Assert.Empty(OpenQuestions.Parse("No hay preguntas abiertas."));
    }

    [Fact]
    public void The_development_suffix_reads_as_assigned()
    {
        var line = "- **Q-1**: ¿Qué tecnología usará la solución?" + OpenQuestions.DevelopmentSuffix;

        var question = Assert.Single(OpenQuestions.Parse(line));

        Assert.True(question.IsAssigned);
        Assert.Equal(("Equipo de desarrollo", "antes de construir"), (question.Owner, question.Revisit));
        Assert.Empty(OpenQuestions.Unassigned(line));
    }
}
