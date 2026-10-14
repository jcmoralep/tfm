using System.Reflection;
using System.Text.RegularExpressions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Infrastructure.Artifacts;
using static BmadPlatform.Infrastructure.Tests.Artifacts.DraftSamples;

namespace BmadPlatform.Infrastructure.Tests.Artifacts;

public sealed partial class FakeArtifactDrafterTests
{
    private static readonly string[] BriefHeadings =
    [
        "Resumen ejecutivo", "El problema", "La solución", "Qué lo hace distinto", "A quién sirve",
        "Criterios de éxito", "Alcance", "Visión", "Índice de suposiciones", "Preguntas pendientes",
    ];

    private static readonly string[] PrdHeadings =
    [
        "## 0 Propósito del documento", "## 1 Visión", "## 2 Usuario objetivo", "### 2.1 Trabajos por hacer",
        "### 2.2 No usuarios", "### 2.3 Recorridos clave", "## 3 Glosario", "## 4 Funcionalidades",
        "## 5 No objetivos", "## 6 Alcance del MVP", "### 6.1 Dentro", "### 6.2 Fuera",
        "## 7 Métricas de éxito", "## 8 Preguntas abiertas", "## 9 Índice de suposiciones",
    ];

    private static readonly string[] SpecHeadings =
    [
        "Por qué", "Capacidades", "Restricciones", "No objetivos", "Señal de éxito", "Índice de suposiciones", "Preguntas abiertas",
    ];

    // The headings of the document that belong to the required outline (extra subsections are ignored).
    private static List<string> OutlineHeadings(string markdown, ArtifactType type)
    {
        var expected = ArtifactOutline.Sections(type).Select(s => s.Heading).ToHashSet();

        return [.. markdown.Split('\n').Where(expected.Contains)];
    }

    private static string Section(string markdown, string title)
    {
        var lines = markdown.Split('\n').ToList();
        var start = lines.FindIndex(l => l.StartsWith('#') && l.EndsWith(title, StringComparison.Ordinal));
        Assert.True(start >= 0, $"Missing section {title}");
        var end = lines.FindIndex(start + 1, l => l.StartsWith('#'));

        return string.Join('\n', lines.Skip(start + 1).Take((end < 0 ? lines.Count : end) - start - 1));
    }

    private static IEnumerable<string> Definitions(string markdown, string prefix) =>
        markdown.Split('\n').Where(l => l.StartsWith($"- **{prefix}-", StringComparison.Ordinal));

    private static List<int> Numbers(string markdown, string prefix) =>
        [.. DefinitionRegex().Matches(markdown).Where(m => m.Groups["p"].Value == prefix).Select(m => int.Parse(m.Groups["n"].Value))];

    [GeneratedRegex(@"^- \*\*(?<p>NFR|FR|UJ|SM|A|Q)-(?<n>\d+)\*\*", RegexOptions.Multiline)]
    private static partial Regex DefinitionRegex();

    [GeneratedRegex(@"\[ASSUMPTION: A-(?<n>\d+)\]")]
    private static partial Regex MarkerRegex();

    [Fact]
    public void The_request_carries_no_identifiers_owner_email_or_timestamps()
    {
        var forbiddenName = new Regex("Id$|Owner|Mail|Timestamp|(Created|Updated|Undone)At", RegexOptions.None, TimeSpan.FromSeconds(1));

        foreach (var member in new[] { typeof(DraftRequest), typeof(TopicAnswer) }
                     .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
        {
            Assert.DoesNotMatch(forbiddenName, member.Name);
            Assert.NotEqual(typeof(Guid), member.PropertyType);
            Assert.NotEqual(typeof(DateTimeOffset), Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType);
            Assert.NotEqual(typeof(DateTime), Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType);
        }
    }

    [Fact]
    public async Task The_brief_has_its_sections_in_order()
    {
        var brief = await Draft(Request(ArtifactType.Brief, InitiativeDepth.Standard));

        Assert.Equal(BriefHeadings.Select(t => "## " + t), OutlineHeadings(brief, ArtifactType.Brief));
        Assert.StartsWith("# Brief: " + Name, brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_prd_has_sections_0_to_9_with_their_subsections()
    {
        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard));

        Assert.Equal(PrdHeadings, OutlineHeadings(prd, ArtifactType.Prd));
    }

    [Fact]
    public async Task The_spec_has_its_sections_in_order()
    {
        var spec = await Draft(Request(ArtifactType.Spec, InitiativeDepth.Small));

        Assert.Equal(SpecHeadings.Select(t => "## " + t), OutlineHeadings(spec, ArtifactType.Spec));
    }

    [Fact]
    public async Task Unfed_brief_sections_carry_assumptions_listed_in_the_index()
    {
        var brief = await Draft(Request(ArtifactType.Brief, InitiativeDepth.Standard));

        foreach (var title in new[] { "Qué lo hace distinto", "Visión" })
        {
            Assert.Matches(MarkerRegex(), Section(brief, title));
        }

        var inline = MarkerRegex().Matches(brief).Select(m => int.Parse(m.Groups["n"].Value)).ToList();
        var index = Section(brief, "Índice de suposiciones");

        Assert.Equal([1, 2, 3], inline);
        Assert.All(inline, n => Assert.Contains($"- **A-{n}**: ", index, StringComparison.Ordinal));
        Assert.Contains("diferencia a esta solución", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Several_unknowns_are_numbered_in_document_order()
    {
        var answers = Answers(InitiativeDepth.Standard, unknown: [AssistantScript.Keys.Users, AssistantScript.Keys.Success]);

        var brief = await Draft(Request(ArtifactType.Brief, InitiativeDepth.Standard, answers));

        var questions = Definitions(brief, "Q").ToList();
        Assert.Equal(2, questions.Count);
        Assert.Contains("quién usará la solución", questions[0], StringComparison.Ordinal);
        Assert.StartsWith("- **Q-1**", questions[0], StringComparison.Ordinal);
        Assert.Contains("cómo se medirá el éxito", questions[1], StringComparison.Ordinal);
        Assert.Contains("Pendiente: ver Q-1.", Section(brief, "A quién sirve"), StringComparison.Ordinal);
        Assert.Contains("Pendiente: ver Q-2.", Section(brief, "Criterios de éxito"), StringComparison.Ordinal);
        Assert.DoesNotContain("No sé", brief, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small)]
    public async Task Every_generated_question_ends_with_the_default_suffix_and_reads_as_unassigned(ArtifactType type, InitiativeDepth depth)
    {
        var unknown = new[] { AssistantScript.Keys.Users, AssistantScript.Keys.Success };

        var document = await Draft(Request(type, depth, Answers(depth, unknown: unknown)));

        var questions = Definitions(document, "Q").ToList();
        Assert.Equal(2, questions.Count);
        Assert.All(questions, q => Assert.EndsWith("(Responsable: por asignar; Revisar: por definir)", q, StringComparison.Ordinal));
        Assert.Equal(["Q-1", "Q-2"], OpenQuestions.Unassigned(document));
    }

    [Fact]
    public async Task The_spec_always_has_a_non_goal_with_an_assumption_and_a_question_when_unknown()
    {
        var answers = Answers(InitiativeDepth.Small, unknown: [AssistantScript.Keys.OutOfScope]);

        var spec = await Draft(Request(ArtifactType.Spec, InitiativeDepth.Small, answers));

        var nonGoals = Section(spec, "No objetivos");
        Assert.Contains("Pendiente: ver Q-1.", nonGoals, StringComparison.Ordinal);
        Assert.Matches(MarkerRegex(), nonGoals);
        Assert.Single(Definitions(spec, "Q"));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Spec, spec));
    }

    [Fact]
    public async Task Capabilities_become_functional_requirements_in_the_prd_and_the_spec()
    {
        var texts = new Dictionary<string, string> { [AssistantScript.Keys.Capabilities] = "Buscar un pedido\nVer su estado\nRecibir un aviso" };

        foreach (var (type, depth) in new[] { (ArtifactType.Prd, InitiativeDepth.Standard), (ArtifactType.Spec, InitiativeDepth.Small) })
        {
            var document = await Draft(Request(type, depth, Answers(depth, texts)));

            Assert.Equal(
                ["- **FR-1**: Buscar un pedido", "- **FR-2**: Ver su estado", "- **FR-3**: Recibir un aviso"],
                Definitions(document, "FR"));
        }
    }

    [Fact]
    public async Task Constraints_and_qualities_become_numbered_non_functional_requirements()
    {
        var texts = new Dictionary<string, string>
        {
            [AssistantScript.Keys.Constraints] = "Funciona en el celular\nNo muestra datos de otros clientes",
            [AssistantScript.Keys.Qualities] = "Responde en pocos segundos",
        };

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Large, Answers(InitiativeDepth.Large, texts)));

        Assert.Equal(
            [
                "- **NFR-1**: Funciona en el celular",
                "- **NFR-2**: No muestra datos de otros clientes",
                "- **NFR-3**: Responde en pocos segundos",
            ],
            Definitions(prd, "NFR"));
        Assert.True(prd.IndexOf("NFR-1", StringComparison.Ordinal) < prd.IndexOf("NFR-3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Large_adds_an_integrations_feature_with_functional_requirements_after_the_capabilities()
    {
        var texts = new Dictionary<string, string>
        {
            [AssistantScript.Keys.Capabilities] = "Buscar un pedido",
            [AssistantScript.Keys.Integrations] = "Sistema de inventario\nEquipo de logística",
        };

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Large, Answers(InitiativeDepth.Large, texts)));

        Assert.Equal(["- **FR-1**: Buscar un pedido", "- **FR-2**: Debe integrarse con Sistema de inventario", "- **FR-3**: Debe integrarse con Equipo de logística"], Definitions(prd, "FR"));
        Assert.Contains("### Integraciones", prd, StringComparison.Ordinal);
        Assert.Contains("Todas las funcionalidades de la sección 4.", Section(prd, "6.1 Dentro"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Standard_has_no_integrations_feature()
    {
        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard));

        Assert.DoesNotContain("Integraciones", prd, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small)]
    public async Task Success_answers_become_metrics(ArtifactType type, InitiativeDepth depth)
    {
        var texts = new Dictionary<string, string> { [AssistantScript.Keys.Success] = "Menos llamadas\nMenos quejas" };

        var document = await Draft(Request(type, depth, Answers(depth, texts)));

        Assert.Equal(["- **SM-1**: Menos llamadas", "- **SM-2**: Menos quejas"], Definitions(document, "SM"));
    }

    [Theory]
    [InlineData("Vendedoras\nPersonal de bodega\nGerencia", false)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public async Task The_key_journeys_section_holds_one_assumption_and_no_journey(string? users, bool unknown)
    {
        var depth = InitiativeDepth.Standard;
        var answers = users is null && !unknown
            ? Answers(depth).Where(a => a.TopicKey != AssistantScript.Keys.Users)
            : Answers(depth, new Dictionary<string, string> { [AssistantScript.Keys.Users] = users ?? string.Empty }, unknown ? [AssistantScript.Keys.Users] : null);

        var prd = await Draft(Request(ArtifactType.Prd, depth, answers));

        var section = Section(prd, "2.3 Recorridos clave");
        var line = Assert.Single(section.Split('\n'), l => l.Length > 0);
        var marker = Assert.Single(MarkerRegex().Matches(line));
        Assert.StartsWith("- [ASSUMPTION: A-", line, StringComparison.Ordinal);
        Assert.EndsWith("(una persona con nombre) al revisar el documento.", line, StringComparison.Ordinal);
        Assert.DoesNotContain("UJ", prd, StringComparison.Ordinal);
        Assert.Contains($"- **A-{marker.Groups["n"].Value}**: Aún no se describió un recorrido", Section(prd, "Índice de suposiciones"), StringComparison.Ordinal);
        Assert.Equal(unknown, Section(prd, "2 Usuario objetivo").Contains("Pendiente: ver Q-1.", StringComparison.Ordinal));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Prd, prd));
    }

    [Theory]
    [InlineData("- **UJ-1**: Ana pide un reporte", 0)]
    [InlineData("- **UJ1** Ana pide un reporte", 1)]
    public async Task A_hand_written_journey_is_checked_like_any_identifier(string journey, int warnings)
    {
        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard));

        var edited = prd.Replace("### 2.3 Recorridos clave\n\n", "### 2.3 Recorridos clave\n\n" + journey + "\n", StringComparison.Ordinal);

        Assert.NotEqual(prd, edited);
        Assert.Equal(warnings, ArtifactFormatChecker.Check(ArtifactType.Prd, edited).Count);
        Assert.Single(ArtifactFormatChecker.Check(ArtifactType.Prd, edited.Replace("### 2.3 Recorridos clave\n", string.Empty, StringComparison.Ordinal)), w => w.Message.Contains("Recorridos clave", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Large)]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small)]
    public async Task Only_hyphenated_identifiers_are_generated(ArtifactType type, InitiativeDepth depth)
    {
        var document = await Draft(Request(type, depth, Answers(depth, unknown: [AssistantScript.Keys.Success])));

        Assert.DoesNotMatch(@"\b(?:FR|NFR|UJ|SM)\d", document);
    }

    [Fact]
    public async Task The_spec_lists_the_users_in_one_labelled_bullet()
    {
        var texts = new Dictionary<string, string> { [AssistantScript.Keys.Users] = "Vendedoras\nPersonal de bodega" };

        var spec = await Draft(Request(ArtifactType.Spec, InitiativeDepth.Small, Answers(InitiativeDepth.Small, texts)));

        Assert.Contains("- **Quién lo usará**: Vendedoras; Personal de bodega", spec, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(spec, "Quién lo usará"));
    }

    [Fact]
    public async Task The_prd_glossary_and_non_users_carry_assumptions()
    {
        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard));

        Assert.Matches(MarkerRegex(), Section(prd, "2.2 No usuarios"));
        Assert.Matches(MarkerRegex(), Section(prd, "3 Glosario"));
    }

    [Fact]
    public async Task An_unknown_constraints_answer_yields_a_question_instead_of_an_invented_requirement()
    {
        var answers = Answers(InitiativeDepth.Standard, unknown: [AssistantScript.Keys.Constraints]);

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, answers));

        var features = Section(prd, "Requisitos no funcionales");
        Assert.Contains("Pendiente: ver Q-1.", features, StringComparison.Ordinal);
        Assert.Empty(Definitions(prd, "NFR"));
        var question = Assert.Single(Definitions(prd, "Q"));
        Assert.Contains("los límites o reglas", question, StringComparison.Ordinal);
        Assert.Contains(question, Section(prd, "8 Preguntas abiertas"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_typed_dont_know_phrase_flagged_as_unknown_is_treated_like_the_quick_reply()
    {
        var typed = Answers(InitiativeDepth.Standard);
        typed[typed.FindIndex(a => a.TopicKey == AssistantScript.Keys.Constraints)] =
            Answer(AssistantScript.Keys.Constraints, "ni idea", unknown: true);
        var button = Answers(InitiativeDepth.Standard, unknown: [AssistantScript.Keys.Constraints]);

        var fromTyped = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, typed));
        var fromButton = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, button));

        Assert.Equal(fromButton, fromTyped);
        Assert.DoesNotContain("ni idea", fromTyped, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard, "No hay preguntas pendientes.")]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Standard, "No hay preguntas abiertas.")]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small, "No hay preguntas abiertas.")]
    public async Task Without_unknowns_the_questions_section_says_there_are_none(ArtifactType type, InitiativeDepth depth, string expected)
    {
        var document = await Draft(Request(type, depth));

        Assert.Empty(Definitions(document, "Q"));
        Assert.Contains(expected, document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_topic_without_an_answer_takes_an_assumption_instead_of_being_left_empty()
    {
        var answers = Answers(InitiativeDepth.Standard).Where(a => a.TopicKey != AssistantScript.Keys.Problem);

        var brief = await Draft(Request(ArtifactType.Brief, InitiativeDepth.Standard, answers));

        Assert.Matches(MarkerRegex(), Section(brief, "El problema"));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Brief, brief));
    }

    [Theory]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Large)]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small)]
    public async Task Identifiers_are_unique_and_numbered_from_one_in_document_order(ArtifactType type, InitiativeDepth depth)
    {
        var unknown = new[] { AssistantScript.Keys.Users, AssistantScript.Keys.Success, AssistantScript.Keys.Constraints };
        var texts = new Dictionary<string, string>
        {
            [AssistantScript.Keys.Capabilities] = "uno\ndos\ntres",
            [AssistantScript.Keys.Success] = "a\nb",
        };

        foreach (var answers in new[] { Answers(depth), Answers(depth, texts, unknown), Answers(depth, texts) })
        {
            var document = await Draft(Request(type, depth, answers));

            foreach (var prefix in ItemPrefix.All)
            {
                var numbers = Numbers(document, prefix);
                Assert.Equal(Enumerable.Range(1, numbers.Count), numbers);
            }

            var inline = MarkerRegex().Matches(document).Select(m => int.Parse(m.Groups["n"].Value)).ToList();
            Assert.Equal(Enumerable.Range(1, inline.Count), inline);
            Assert.Equal(inline, Numbers(document, ItemPrefix.Assumption));
        }
    }

    [Fact]
    public async Task Multi_line_answers_split_into_items_with_markers_removed_and_headings_escaped()
    {
        var texts = new Dictionary<string, string>
        {
            [AssistantScript.Keys.Capabilities] = "1. Buscar\n- Ver\n* Avisar\n\n   Exportar\n# Título falso\n## Otro",
        };

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, Answers(InitiativeDepth.Standard, texts)));

        Assert.Equal(
            [
                "- **FR-1**: Buscar", "- **FR-2**: Ver", "- **FR-3**: Avisar", "- **FR-4**: Exportar",
                @"- **FR-5**: \# Título falso", @"- **FR-6**: \## Otro",
            ],
            Definitions(prd, "FR"));
        Assert.DoesNotContain("\n# Título", prd, StringComparison.Ordinal);
        Assert.Equal(PrdHeadings, OutlineHeadings(prd, ArtifactType.Prd));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Prd, prd));
    }

    [Fact]
    public async Task A_description_or_name_starting_with_a_hash_cannot_create_a_heading()
    {
        var request = Request(ArtifactType.Brief, InitiativeDepth.Standard, description: "# Una descripción\ncon dos líneas") with
        {
            InitiativeName = "  App   de   pedidos ",
        };

        var brief = await Draft(request);

        Assert.StartsWith("# Brief: App de pedidos", brief, StringComparison.Ordinal);
        Assert.Contains("Iniciativa «App de pedidos»: \\# Una descripción con dos líneas", brief, StringComparison.Ordinal);
        Assert.Equal(BriefHeadings.Select(t => "## " + t), OutlineHeadings(brief, ArtifactType.Brief));
    }

    [Fact]
    public async Task Topics_outside_the_required_set_produce_nothing()
    {
        var extra = new[]
        {
            Answer(AssistantScript.Keys.Integrations, "texto-integraciones"),
            Answer(AssistantScript.Keys.Qualities, "texto-calidades"),
            Answer(AssistantScript.Keys.Constraints, "texto-limites"),
            Answer(AssistantScript.Keys.Capabilities, "texto-capacidades"),
        };

        var standardPrd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, Answers(InitiativeDepth.Standard).Concat(extra.Take(2))));
        var brief = await Draft(Request(ArtifactType.Brief, InitiativeDepth.Standard, Answers(InitiativeDepth.Standard).Concat(extra.Skip(2))));
        var spec = await Draft(Request(ArtifactType.Spec, InitiativeDepth.Small, Answers(InitiativeDepth.Small).Concat(extra.Take(3))));

        Assert.DoesNotContain("texto-integraciones", standardPrd, StringComparison.Ordinal);
        Assert.DoesNotContain("texto-calidades", standardPrd, StringComparison.Ordinal);
        Assert.DoesNotContain("texto-limites", brief + spec, StringComparison.Ordinal);
        Assert.DoesNotContain("texto-capacidades", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("texto-integraciones", spec, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_answer_is_used_at_most_once_in_a_document()
    {
        foreach (var (type, depth) in Combinations())
        {
            var texts = Topics(depth).ToDictionary(key => key, key => $"marca-{key}-única");
            var document = await Draft(Request(type, depth, Answers(depth, texts)));

            foreach (var marker in texts.Values)
            {
                Assert.True(Regex.Count(document, Regex.Escape(marker)) <= 1, $"{marker} is repeated in {type} {depth}");
            }
        }
    }

    [Fact]
    public async Task Equal_requests_give_equal_markdown()
    {
        foreach (var (type, depth) in Combinations())
        {
            var first = await Draft(Request(type, depth));
            var second = await new FakeArtifactDrafter().DraftAsync(Request(type, depth), default);

            Assert.Equal(first, second);
        }
    }

    [Fact]
    public async Task Each_answer_is_clipped_with_an_ellipsis_and_the_largest_document_stays_under_the_cap()
    {
        var texts = Topics(InitiativeDepth.Large).ToDictionary(key => key, _ => new string('x', 2000));

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Large, Answers(InitiativeDepth.Large, texts)));

        Assert.True(prd.Length <= Artifact.ContentMaxLength, $"Length {prd.Length}");
        Assert.Contains(new string('x', 1499) + "…", prd, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 1500), prd, StringComparison.Ordinal);
        Assert.Equal(PrdHeadings, OutlineHeadings(prd, ArtifactType.Prd));
        Assert.Matches(@"- \*\*A-\d+\*\*: [^\n]+$", prd);
        Assert.Equal("## 9 Índice de suposiciones", prd.Split('\n').Last(l => l.StartsWith("## ", StringComparison.Ordinal)));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Prd, prd));
    }

    [Fact]
    public async Task A_multi_line_answer_is_clipped_as_a_whole()
    {
        var texts = new Dictionary<string, string>
        {
            [AssistantScript.Keys.Capabilities] = string.Join('\n', Enumerable.Repeat(new string('y', 700), 3)),
        };

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, Answers(InitiativeDepth.Standard, texts)));

        var lines = Definitions(prd, "FR").Select(l => l[(l.IndexOf(": ", StringComparison.Ordinal) + 2)..]).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(1500, lines.Sum(l => l.Length));
        Assert.EndsWith("…", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Very_long_names_never_break_the_cap()
    {
        var texts = Topics(InitiativeDepth.Large).ToDictionary(key => key, _ => new string('z', 2000));
        var request = Request(ArtifactType.Prd, InitiativeDepth.Large, Answers(InitiativeDepth.Large, texts), new string('d', 1000)) with
        {
            InitiativeName = new string('n', 120),
        };

        var prd = await Draft(request);

        Assert.True(prd.Length <= Artifact.ContentMaxLength);
    }

    [Fact]
    public async Task Every_type_and_level_passes_the_domain_and_the_format_checker_without_warnings()
    {
        var scenarios = new Func<InitiativeDepth, List<TopicAnswer>>[]
        {
            depth => Answers(depth),
            depth => Answers(depth, unknown: Topics(depth).ToList()),
            depth => [],
            depth => Answers(depth, Topics(depth).ToDictionary(key => key, _ => "uno\n- dos\n3) tres")),
        };

        foreach (var (type, depth) in Combinations())
        {
            foreach (var scenario in scenarios)
            {
                var document = await Draft(Request(type, depth, scenario(depth), description: null));

                var artifact = Artifact.Create(Guid.NewGuid(), "user-a", type, document, DateTimeOffset.UnixEpoch);
                Assert.Equal(document, artifact.Content);
                Assert.Empty(ArtifactFormatChecker.Check(type, document));
                Assert.Equal(Definitions(document, "Q").Select(q => q[4..q.IndexOf("**:", StringComparison.Ordinal)]), OpenQuestions.Unassigned(document));
            }
        }
    }

    [Fact]
    public async Task No_frontmatter_status_or_version_is_written()
    {
        foreach (var (type, depth) in Combinations())
        {
            var document = await Draft(Request(type, depth));

            Assert.False(document.StartsWith("---", StringComparison.Ordinal));
            Assert.DoesNotContain(document.Split('\n'), l => Regex.IsMatch(l, @"^(status|version|title|created|updated):", RegexOptions.IgnoreCase));
        }
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_draft()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new FakeArtifactDrafter().DraftAsync(Request(ArtifactType.Brief, InitiativeDepth.Standard), cancelled.Token));
    }

    [Theory]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Large, AssistantScript.Keys.Integrations, "Ninguno", "No se indicaron integraciones.")]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Standard, AssistantScript.Keys.Constraints, "Ninguno que yo sepa", "No se indicaron restricciones.")]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Large, AssistantScript.Keys.Qualities, "Nada especial", "No se indicaron condiciones especiales de velocidad o seguridad.")]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small, AssistantScript.Keys.OutOfScope, "Nada por ahora", "No se indicó nada fuera del alcance por ahora.")]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard, AssistantScript.Keys.OutOfScope, "Nada por ahora", "No se indicó nada fuera del alcance por ahora.")]
    public async Task A_nothing_quick_reply_becomes_a_sentence_and_never_a_requirement(
        ArtifactType type,
        InitiativeDepth depth,
        string key,
        string label,
        string sentence)
    {
        var answers = Answers(depth).Select(a => a.TopicKey == key ? None(key, label) : a).ToList();
        var withText = await Draft(Request(type, depth, Answers(depth, new Dictionary<string, string> { [key] = label })));

        var document = await Draft(Request(type, depth, answers));

        Assert.Contains("- " + sentence, document, StringComparison.Ordinal);
        Assert.DoesNotContain(label, document, StringComparison.Ordinal);
        Assert.Contains(label, withText, StringComparison.Ordinal);
        Assert.Empty(ArtifactFormatChecker.Check(type, document));
    }

    [Fact]
    public async Task Other_quick_replies_keep_their_label_as_text()
    {
        var answers = Answers(InitiativeDepth.Standard, new Dictionary<string, string> { [AssistantScript.Keys.Users] = "Ambos" });

        var prd = await Draft(Request(ArtifactType.Prd, InitiativeDepth.Standard, answers));

        Assert.Contains("- Ambos", prd, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Large)]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Small)]
    public async Task Many_tiny_lines_are_capped_per_answer_and_stay_under_the_content_cap(ArtifactType type, InitiativeDepth depth)
    {
        var thousand = string.Join('\n', Enumerable.Repeat("a", 1000));
        var texts = Topics(depth).ToDictionary(key => key, _ => thousand);

        var document = await Draft(Request(type, depth, Answers(depth, texts)));

        Assert.True(document.Length <= Artifact.ContentMaxLength, $"Length {document.Length}");
        Assert.Contains("- y 980 más.", document, StringComparison.Ordinal);
        Assert.All(new[] { "FR", "UJ", "SM" }, prefix => Assert.True(Definitions(document, prefix).Count() <= 40));
        Assert.Empty(ArtifactFormatChecker.Check(type, document));
    }

    [Fact]
    public async Task The_fake_ignores_the_brief_text()
    {
        var request = Request(ArtifactType.Prd, InitiativeDepth.Standard);

        var withBrief = await Draft(request with { BriefMarkdown = "# Brief\n\n- [ASSUMPTION: A-1] algo del Brief" });

        Assert.Equal(await Draft(request), withBrief);
    }
}
