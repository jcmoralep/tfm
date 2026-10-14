using System.Text.RegularExpressions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Artifacts;

namespace BmadPlatform.Infrastructure.Tests.Artifacts;

public sealed class EpicsLayoutTests
{
    private const string Prd = """
        # PRD: App

        ## 4 Funcionalidades

        - **FR-1**: Buscar un pedido
        - **FR-3**: Ver el estado
        - **FR-4**: Recibir avisos

        ### Requisitos no funcionales

        - **NFR-1**: Debe funcionar desde el celular
        """;

    private const string Architecture = """
        # Arquitectura: App

        ## Invariantes y reglas

        ### AD-1 — Datos en un solo lugar

        ### AD-2 — Acceso con inicio de sesión
        """;

    private static StoryItem Story(string title, params string[] covers) =>
        new(title, "vendedora", "buscar pedidos", "no llamar a bodega", covers, [new CriterionItem("un pedido", "lo busco", "lo veo")]);

    private static EpicItem Epic(string title, params StoryItem[] stories) => new(title, "Un objetivo", stories);

    private static string Draft(string prd, params EpicItem[] epics) =>
        DraftRenderer.Render(
            new DraftRequest(ArtifactType.EpicsAndStories, "App", null, InitiativeDepth.Large, [], PrdMarkdown: prd, ArchitectureMarkdown: Architecture),
            epics.Length == 0 ? null : new DraftItems(new Dictionary<string, IReadOnlyList<DraftItem>>(), Epics: epics));

    [Fact]
    public void The_inventory_copies_the_prd_numbers_with_their_gaps_and_the_architecture_decisions()
    {
        var markdown = Draft(Prd);

        Assert.Contains("- **FR-1**: Buscar un pedido\n- **FR-3**: Ver el estado\n- **FR-4**: Recibir avisos\n", markdown);
        Assert.Contains("- **NFR-1**: Debe funcionar desde el celular\n", markdown);
        Assert.Contains("- **AD-1**: Datos en un solo lugar\n- **AD-2**: Acceso con inicio de sesión\n", markdown);
        Assert.DoesNotContain("FR-2", markdown);
        Assert.DoesNotMatch(@"\bN?FR\d", markdown);
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.EpicsAndStories, markdown));
    }

    [Fact]
    public void The_fake_writes_one_story_per_functional_requirement_in_the_bmad_shape_with_no_forward_reference()
    {
        var markdown = Draft(Prd);

        Assert.Equal(["Story 1.1", "Story 1.2", "Story 1.3"], Regex.Matches(markdown, @"^### (Story \d+\.\d+):", RegexOptions.Multiline).Select(m => m.Groups[1].Value));
        Assert.Equal(3, Regex.Matches(markdown, @"^\*\*As a\*\* .+, \*\*I want\*\* .+, \*\*So that\*\* .+$", RegexOptions.Multiline).Count);
        Assert.Contains("**Cubre:** FR-3\n", markdown);
        Assert.Equal(3, Regex.Matches(markdown, @"^- \*\*Given\*\* .+ \*\*When\*\* .+ \*\*Then\*\* .+$", RegexOptions.Multiline).Count);
        Assert.Contains("- FR-1: Epic 1 (1.1)\n- FR-3: Epic 1 (1.2)\n- FR-4: Epic 1 (1.3)\n", markdown);
        Assert.Empty(OpenQuestions.Parse(markdown));
        Assert.Contains("No hay preguntas para desarrollo.", markdown);
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.EpicsAndStories, markdown));
    }

    [Theory]
    [InlineData("fr1, FR 3, FR-1, NFR-1", "FR-1, FR-3, NFR-1")]
    [InlineData("FR-9, FR-3, NFR-7, AD-1", "FR-3")]
    [InlineData("NFR-1, FR-4", "FR-4, NFR-1")]
    public void Citations_are_filtered_to_the_prd_deduplicated_and_sorted_in_prd_order(string covers, string expected)
    {
        var markdown = Draft(Prd, Epic("Pedidos", Story("Una historia", covers.Split(", "))));

        Assert.Contains($"**Cubre:** {expected}\n", markdown);
    }

    [Theory]
    [InlineData("FR-9")]
    [InlineData("NFR-1")]
    public void A_story_without_a_valid_functional_requirement_is_dropped_and_the_numbering_has_no_gap(string covers)
    {
        var markdown = Draft(Prd, Epic("Pedidos", Story("Inventada", covers), Story("Real", "FR-1")));

        Assert.DoesNotContain("Inventada", markdown);
        Assert.Contains("### Story 1.1: Real", markdown);
        Assert.DoesNotContain("Story 1.2", markdown);
    }

    [Fact]
    public void Coverage_and_the_questions_come_from_the_final_stories()
    {
        var markdown = Draft(Prd, Epic("Pedidos", Story("Buscar", "FR-1")), Epic("Avisos", Story("Avisar", "FR-1", "FR-4")));

        Assert.Contains("- FR-1: Epic 1 (1.1), Epic 2 (2.1)\n", markdown);
        Assert.Contains("- FR-3: sin historia todavía (ver Q-1)\n", markdown);
        Assert.Contains("- FR-4: Epic 2 (2.1)\n", markdown);
        Assert.Equal(["¿Qué historia cubre FR-3?"], OpenQuestions.Parse(markdown).Select(q => q.Text));
        Assert.All(OpenQuestions.Parse(markdown), question => Assert.Equal("Equipo de desarrollo", question.Owner));
        Assert.Empty(OpenQuestions.Unassigned(markdown));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.EpicsAndStories, markdown));
    }

    [Fact]
    public void Caps_hold_and_what_only_a_dropped_story_covered_becomes_a_question()
    {
        var criteria = Enumerable.Range(1, 6).Select(n => new CriterionItem($"dado {n}", $"cuando {n}", $"entonces {n}")).ToList();
        var crowded = Epic(
            "Muchas",
            [.. Enumerable.Range(1, 9).Select(n => new StoryItem($"Historia {n}", "a", "b", "c", [n == 7 ? "FR-4" : "FR-1"], criteria))]);
        var epics = new[] { crowded }.Concat(Enumerable.Range(1, 7).Select(n => Epic($"Épica {n}", Story($"Otra {n}", "FR-3")))).ToArray();

        var markdown = Draft(Prd, epics);

        Assert.Equal(6, Regex.Matches(markdown, @"^## Epic \d+:", RegexOptions.Multiline).Count);
        Assert.Equal(5, Regex.Matches(markdown, @"^### Story 1\.\d+:", RegexOptions.Multiline).Count);
        Assert.Equal(5 * 3, Regex.Matches(markdown, @"^- \*\*Given\*\* dado", RegexOptions.Multiline).Count);
        Assert.Contains("- FR-4: sin historia todavía (ver Q-1)", markdown);
        Assert.Contains("¿Qué historia cubre FR-4?", markdown);
    }

    [Theory]
    [InlineData("TBD: definir")]
    [InlineData("Usar {motor}")]
    public void A_placeholder_never_reaches_the_document_and_costs_the_story(string title)
    {
        var markdown = Draft(Prd, Epic("Pedidos", Story(title, "FR-1"), Story("Real", "FR-3")));

        Assert.DoesNotContain(title, markdown);
        Assert.Contains("- FR-1: sin historia todavía", markdown);
        Assert.DoesNotMatch(@"\b(TBD|TODO)\b", markdown);
    }

    [Fact]
    public void A_prd_without_functional_requirements_has_no_epic_and_one_question()
    {
        var markdown = Draft("# PRD: App\n\n- **NFR-1**: Debe funcionar desde el celular");

        Assert.DoesNotContain("## Epic", markdown);
        Assert.Contains("El PRD no define requisitos funcionales con identificador.", markdown);
        Assert.Single(OpenQuestions.Parse(markdown));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.EpicsAndStories, markdown));
    }

    [Fact]
    public void Forty_long_requirements_fit_the_cap_with_every_requirement_mapped_and_every_uncovered_one_asked_about()
    {
        var prd = string.Join("\n", Enumerable.Range(1, 40).Select(n => $"- **FR-{n}**: {new string('x', 380)} requisito {n}"));

        var markdown = Draft(prd);

        Assert.True(markdown.Length <= Artifact.ContentMaxLength, $"{markdown.Length} characters");
        Assert.Equal(40, Regex.Matches(markdown, @"^- FR-\d+: (?:Epic|sin historia)", RegexOptions.Multiline).Count);

        var uncovered = Regex.Matches(markdown, @"^- (FR-\d+): sin historia", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        var asked = OpenQuestions.Parse(markdown).Select(q => q.Text).ToList();

        Assert.NotEmpty(uncovered);
        Assert.All(uncovered, id => Assert.Contains($"¿Qué historia cubre {id}?", asked));
        Assert.Equal(uncovered.Count, asked.Count);
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.EpicsAndStories, markdown));
    }
}
