using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Artifacts;

namespace BmadPlatform.Infrastructure.Tests.Artifacts;

public sealed class ArchitectureLayoutTests
{
    private const string Prd = """
        # PRD: App

        ## 4 Funcionalidades

        - **FR-1**: Buscar un pedido
        - **FR-2**: Ver el estado
        - **FR-3**: Recibir avisos

        ### Requisitos no funcionales

        - **NFR-1**: Debe funcionar desde el celular
        """;

    private const string PrdWithoutNonFunctional = """
        # PRD: App

        - **FR-1**: Buscar un pedido
        """;

    private static DecisionItem Decision(string title, string[] binds, string prevents = "Que se pierda el dato", string rule = "Un solo lugar escribe el dato") =>
        new(title, binds, prevents, rule);

    private static string Draft(string prd, params DecisionItem[] decisions) =>
        DraftRenderer.Render(
            new DraftRequest(ArtifactType.Architecture, "App", null, InitiativeDepth.Large, [], PrdMarkdown: prd),
            decisions.Length == 0 ? null : new DraftItems(new Dictionary<string, IReadOnlyList<DraftItem>>(), Decisions: decisions));

    [Fact]
    public void Decisions_are_numbered_by_code_and_their_binds_are_filtered_to_the_prd()
    {
        var markdown = Draft(
            Prd,
            Decision("AD-9 Persistencia", ["nfr1", "FR-9", "FR 2", "fr-2"]),
            Decision("Solo inventada", ["FR-9"]),
            Decision("Acceso", ["FR-1"]));

        Assert.Equal([("AD-1", "Persistencia"), ("AD-2", "Acceso")], ArchitectureDecisionItems.Parse(markdown).Select(d => (d.Id, d.Title)));
        Assert.Contains("- **Vincula (binds):** FR-2, NFR-1\n", markdown);
        Assert.Contains("- **Vincula (binds):** FR-1\n", markdown);
        Assert.Equal(2, CountOf(markdown, "- **Previene (prevents):** [ASSUMPTION: A-"));
        Assert.Equal(2, CountOf(markdown, "- **Regla (rule):** [ASSUMPTION: A-"));
        Assert.DoesNotContain("Solo inventada", markdown);
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Architecture, markdown));
    }

    [Fact]
    public void The_capability_map_and_the_questions_are_computed_from_the_final_decisions()
    {
        var markdown = Draft(Prd, Decision("Datos", ["FR-1", "FR-2"]));

        Assert.Contains("| FR-1 | Buscar un pedido | AD-1 |", markdown);
        Assert.Contains("| FR-2 | Ver el estado | AD-1 |", markdown);
        Assert.Contains("| FR-3 | Recibir avisos | Ninguna todavía |", markdown);
        Assert.Contains("¿Qué decisión de arquitectura cubre FR-3?", markdown);
        Assert.DoesNotContain("cubre FR-1?", markdown);
    }

    [Fact]
    public void At_most_ten_decisions_are_written()
    {
        var markdown = Draft(Prd, [.. Enumerable.Range(1, 12).Select(n => Decision($"Decisión {n}", ["FR-1"]))]);

        Assert.Equal(10, ArchitectureDecisionItems.Parse(markdown).Count);
    }

    [Theory]
    [InlineData("TBD: definir la base de datos")]
    [InlineData("Usar {motor} para guardar")]
    [InlineData("Usar <motor> para guardar")]
    public void A_placeholder_never_reaches_the_document_and_becomes_a_question(string rule)
    {
        var markdown = Draft(Prd, Decision("Datos", ["FR-1"], rule: rule));

        Assert.DoesNotContain(rule, markdown);
        Assert.Contains("tenía texto sin definir", markdown);
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Architecture, markdown));
    }

    [Fact]
    public void With_no_model_decisions_the_fake_rule_writes_one_decision_per_non_functional_requirement()
    {
        var fake = Draft(Prd);
        var allDropped = Draft(Prd, Decision("Inventada", ["FR-9"]));

        Assert.Equal(["Debe funcionar desde el celular"], ArchitectureDecisionItems.Parse(fake).Select(d => d.Title));
        Assert.Equal(fake, allDropped);
    }

    [Fact]
    public void A_prd_without_non_functional_requirements_gets_one_assumption_instead_of_decisions()
    {
        var markdown = Draft(PrdWithoutNonFunctional);

        Assert.Empty(ArchitectureDecisionItems.Parse(markdown));
        Assert.Contains("[ASSUMPTION: A-2] El PRD no define requisitos no funcionales", markdown);
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Architecture, markdown));
    }

    [Fact]
    public void The_stack_is_deferred_with_a_reason_and_is_the_first_question_and_no_stack_section_exists()
    {
        var markdown = Draft(Prd);

        Assert.DoesNotContain("Stack", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("- **Tecnología (pila):**", markdown);
        Assert.Equal("¿Qué tecnología (lenguajes, marcos y versiones) usará la solución?", OpenQuestions.Parse(markdown)[0].Text);
        Assert.All(OpenQuestions.Parse(markdown), question => Assert.True(question.IsAssigned));
    }

    [Fact]
    public void Requirements_beyond_the_question_limit_share_one_summary_question()
    {
        var prd = string.Join("\n", Enumerable.Range(1, 12).Select(n => $"- **FR-{n}**: Funcionalidad {n}"));

        var questions = OpenQuestions.Parse(Draft(prd));

        Assert.Equal(1 + 8 + 1, questions.Count);
        Assert.Contains("Otros 4 requisitos funcionales", questions[^1].Text);
    }

    private static int CountOf(string text, string part) => text.Split(part).Length - 1;
}
