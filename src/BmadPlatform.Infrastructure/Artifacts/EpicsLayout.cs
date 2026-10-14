using BmadPlatform.Application.Features.Artifacts.Markdown;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// The Epics and stories draft, shaped as the BMAD <c>epics.md</c>: the structural words (<c>Epic</c>, <c>Story</c>,
/// <c>As a</c>, <c>I want</c>, <c>So that</c>, <c>Given</c>, <c>When</c>, <c>Then</c>) stay in BMAD's English and the content
/// is Spanish. Code owns the numbering, the requirement inventory (copied from the approved PRD and Architecture with the
/// same numbers), the coverage map and the questions for every requirement left without a story; the stories come from
/// the model or, with none, from the Fake rule (one story per functional requirement). Every citation is checked against
/// the approved PRD, so a story is always anchored to a requirement that exists.
/// </summary>
internal static class EpicsLayout
{
    public const int MaxEpics = 6;

    public const int MaxStoriesPerEpic = 5;

    public const int MaxStories = 20;

    public const int MaxCriteriaPerStory = 3;

    public const int MaxCoversPerStory = 6;

    private const int FakeEpicSize = 5;

    private const int MaxFakeTitle = 80;

    private const string GenericRole = "persona que usa la solución";

    private const string GenericBenefit = "se cumpla lo que pide el PRD aprobado";

    private sealed record Criterion(string Given, string When, string Then);

    private sealed record Story(string Title, string AsA, string IWant, string SoThat, IReadOnlyList<string> Covers, IReadOnlyList<Criterion> Criteria);

    private sealed record Epic(string Title, string Goal, List<Story> Stories);

    public static void Write(DraftComposer c)
    {
        var prd = new PrdFacts(c.Request.PrdMarkdown);
        var budget = LayoutBudget.For(c.AnswerBudget);
        var epics = Normalize(c.Epics, prd, budget);
        var fake = epics.Count == 0;

        if (fake)
        {
            epics = FakeEpics(prd, budget);
        }

        DropFromEnd(epics, c.DroppedStories);

        // Coverage and its questions come from the final stories, after every cap and every drop of the ladder.
        var coverage = CoverageOf(epics);
        var uncovered = prd.Functional.Where(item => !coverage.ContainsKey(item.Id)).ToList();
        var askedAbout = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in uncovered)
        {
            askedAbout[item.Id] = c.OpenQuestion($"¿Qué historia cubre {item.Id}?");
        }

        if (prd.Functional.Count == 0)
        {
            c.OpenQuestion("El PRD aprobado no define requisitos funcionales con identificador: ¿qué historias hay que construir?");
        }

        c.Title("Épicas e historias: " + c.InitiativeName);

        c.Heading("Resumen");
        WriteSummary(c, epics, prd, coverage, fake);

        c.Heading("Inventario de requisitos");
        WriteInventory(c, prd, budget);

        c.Heading(ArtifactOutline.CoverageMapTitle);
        WriteMap(c, prd, coverage, askedAbout);

        c.Heading("Lista de épicas");
        WriteEpicList(c, epics, prd);

        WriteEpics(c, epics);

        foreach (var raw in c.ModelQuestions.Take(ArchitectureLayout.MaxModelQuestions))
        {
            if (!ArchitectureLayout.HasPlaceholder(raw) && DraftComposer.ModelText(raw) is { Length: > 0 } question)
            {
                c.OpenQuestion(question);
            }
        }

        c.OpenQuestions(ArtifactOutline.DevelopmentQuestionsTitle, "No hay preguntas para desarrollo.", OpenQuestions.DevelopmentSuffix);
        c.AssumptionIndex();
    }

    private static void WriteSummary(DraftComposer c, List<Epic> epics, PrdFacts prd, Dictionary<string, List<(int Epic, int Story)>> coverage, bool fake)
    {
        var stories = epics.Sum(epic => epic.Stories.Count);

        c.Line("- Este borrador divide el trabajo del PRD aprobado en épicas e historias, para que el equipo de desarrollo lo revise y lo construya historia por historia.");
        c.Line($"- Épicas: {epics.Count}; historias: {stories}; requisitos funcionales con historia: {coverage.Count} de {prd.Functional.Count}.");

        if (fake)
        {
            c.Line("- " + c.Assumption("Esta primera versión propone una historia por cada requisito funcional, con un solo criterio de aceptación genérico; el equipo de desarrollo las agrupa y las afina al revisar este borrador."));
        }
    }

    // Copied from the approved documents, never written by the model: the PRD number is kept, gaps included.
    private static void WriteInventory(DraftComposer c, PrdFacts prd, LayoutBudget budget)
    {
        c.Raw("### Requisitos funcionales");
        WriteRequirements(c, prd.Functional, budget, "El PRD no define requisitos funcionales con identificador.");

        c.Raw("### Requisitos no funcionales");
        WriteRequirements(c, prd.NonFunctional, budget, "El PRD no define requisitos no funcionales con identificador.");

        c.Raw("### Requisitos adicionales");

        var decisions = ArchitectureDecisionItems.Parse(c.Request.ArchitectureMarkdown);

        if (decisions.Count == 0)
        {
            c.Line("La Arquitectura aprobada no define decisiones con identificador.");

            return;
        }

        foreach (var decision in decisions)
        {
            c.Line(ArtifactOutline.Item(decision.Id, Quote(decision.Title, budget)));
        }
    }

    private static void WriteRequirements(DraftComposer c, IReadOnlyList<RequirementItem> items, LayoutBudget budget, string none)
    {
        if (items.Count == 0)
        {
            c.Line(none);

            return;
        }

        foreach (var item in items)
        {
            c.Line(ArtifactOutline.Item(item.Id, Quote(item.Text, budget)));
        }
    }

    private static string Quote(string text, LayoutBudget budget) =>
        DraftComposer.Clip(DraftComposer.ModelText(text), budget.FieldClip);

    // One line per functional requirement in PRD order; these lines cite the requirement, only the inventory defines it.
    private static void WriteMap(DraftComposer c, PrdFacts prd, Dictionary<string, List<(int Epic, int Story)>> coverage, Dictionary<string, string> askedAbout)
    {
        if (prd.Functional.Count == 0)
        {
            c.Line("El PRD no define requisitos funcionales con identificador.");

            return;
        }

        foreach (var item in prd.Functional)
        {
            if (!coverage.TryGetValue(item.Id, out var places))
            {
                c.Line($"- {item.Id}: sin historia todavía (ver {askedAbout[item.Id]})");

                continue;
            }

            var byEpic = places.GroupBy(place => place.Epic)
                .Select(group => $"Epic {group.Key} ({string.Join(", ", group.Select(place => $"{place.Epic}.{place.Story}"))})");

            c.Line($"- {item.Id}: {string.Join(", ", byEpic)}");
        }
    }

    private static void WriteEpicList(DraftComposer c, List<Epic> epics, PrdFacts prd)
    {
        if (epics.Count == 0)
        {
            c.Line(prd.Functional.Count == 0
                ? "No hay épicas: el PRD aprobado no define requisitos funcionales."
                : "No hay épicas: ninguna historia cabe en el documento; cada requisito pendiente tiene una pregunta para desarrollo.");

            return;
        }

        for (var i = 0; i < epics.Count; i++)
        {
            c.Line($"- Epic {i + 1}: {epics[i].Title.TrimEnd('.')}. {epics[i].Goal}");
        }
    }

    private static void WriteEpics(DraftComposer c, List<Epic> epics)
    {
        for (var e = 0; e < epics.Count; e++)
        {
            c.Raw($"## Epic {e + 1}: {epics[e].Title}");
            c.Line(epics[e].Goal);
            c.Blank();

            for (var s = 0; s < epics[e].Stories.Count; s++)
            {
                var story = epics[e].Stories[s];

                c.Raw($"### Story {e + 1}.{s + 1}: {story.Title}");
                c.Line($"**As a** {story.AsA}, **I want** {story.IWant}, **So that** {story.SoThat}");
                c.Blank();
                c.Line($"**Cubre:** {string.Join(", ", story.Covers)}");
                c.Blank();
                c.Line("**Acceptance Criteria:**");
                c.Blank();

                foreach (var criterion in story.Criteria)
                {
                    c.Line($"- **Given** {criterion.Given} **When** {criterion.When} **Then** {criterion.Then}");
                }
            }
        }
    }

    // FR-n to the places that cover it (epic and story numbers), in the order the stories are written.
    private static Dictionary<string, List<(int Epic, int Story)>> CoverageOf(List<Epic> epics)
    {
        var coverage = new Dictionary<string, List<(int Epic, int Story)>>(StringComparer.Ordinal);

        for (var e = 0; e < epics.Count; e++)
        {
            for (var s = 0; s < epics[e].Stories.Count; s++)
            {
                foreach (var id in epics[e].Stories[s].Covers.Where(id => id.StartsWith("FR-", StringComparison.Ordinal)))
                {
                    if (!coverage.TryGetValue(id, out var places))
                    {
                        coverage[id] = places = [];
                    }

                    places.Add((e + 1, s + 1));
                }
            }
        }

        return coverage;
    }

    // The last rung: the trailing stories go first, then the epics left empty. Numbering stays without gaps.
    private static void DropFromEnd(List<Epic> epics, int count)
    {
        for (var i = 0; i < count && epics.Count > 0; i++)
        {
            epics[^1].Stories.RemoveAt(epics[^1].Stories.Count - 1);

            if (epics[^1].Stories.Count == 0)
            {
                epics.RemoveAt(epics.Count - 1);
            }
        }
    }

    // One story per functional requirement, five to an epic. Nothing is invented beyond the requirement itself, and the
    // texts are already clean, so they skip the model checks.
    private static List<Epic> FakeEpics(PrdFacts prd, LayoutBudget budget)
    {
        var epics = new List<Epic>();
        var chunks = prd.Functional.Take(MaxStories).Chunk(FakeEpicSize).Take(MaxEpics).ToList();

        for (var part = 0; part < chunks.Count; part++)
        {
            var ids = chunks[part].Select(item => item.Id).ToList();
            var goal = ids.Count == 1
                ? $"Entregar el requisito {ids[0]} del PRD."
                : $"Entregar los requisitos {string.Join(", ", ids)} del PRD.";

            epics.Add(new Epic(
                $"Funcionalidades del PRD (parte {part + 1})",
                goal,
                [.. chunks[part].Select(item => FakeStory(item, budget))]));
        }

        return epics;
    }

    private static Story FakeStory(RequirementItem item, LayoutBudget budget)
    {
        var text = Quote(item.Text, budget);

        return new Story(
            DraftComposer.Clip(text, MaxFakeTitle),
            GenericRole,
            $"que la solución cumpla: «{text}»",
            $"{GenericBenefit} en {item.Id}",
            [item.Id],
            [DefaultCriterion(item.Id, text)]);
    }

    private static Criterion DefaultCriterion(string id, string text) =>
        new($"que el PRD aprobado define {id}", "la historia se da por terminada", $"la solución cumple: «{text}»");

    // The model's epics, cleaned and capped. Covers are canonicalised and filtered to the PRD, de-duplicated and sorted in
    // PRD order; a story with no valid functional requirement, or without a title or a goal, is dropped; an epic left with
    // no story is dropped. Nothing the model counted is trusted.
    private static List<Epic> Normalize(IReadOnlyList<EpicItem> source, PrdFacts prd, LayoutBudget budget)
    {
        var kept = new List<Epic>();
        var total = 0;
        var texts = prd.All.ToDictionary(item => item.Id, item => item.Text, StringComparer.Ordinal);

        foreach (var epic in source.Take(MaxEpics))
        {
            var stories = new List<Story>();

            foreach (var item in epic.Stories.Take(MaxStoriesPerEpic))
            {
                if (total + stories.Count == MaxStories)
                {
                    break;
                }

                if (NormalizeStory(item, prd, budget, texts) is { } story)
                {
                    stories.Add(story);
                }
            }

            if (stories.Count == 0)
            {
                continue;
            }

            total += stories.Count;
            kept.Add(new Epic(
                Field(epic.Title, budget) ?? "Épica sin título",
                Field(epic.Goal, budget) ?? "Entregar las historias de esta épica.",
                stories));
        }

        return kept;
    }

    private static Story? NormalizeStory(StoryItem item, PrdFacts prd, LayoutBudget budget, Dictionary<string, string> texts)
    {
        var covers = EpicsReply.ValidCovers(item.Covers, prd).Take(MaxCoversPerStory).ToList();

        var title = Field(item.Title, budget);
        var wants = Field(item.IWant, budget);

        if (title is null || wants is null || !covers.Any(id => id.StartsWith("FR-", StringComparison.Ordinal)))
        {
            return null;
        }

        var criteria = new List<Criterion>();

        foreach (var raw in item.Criteria)
        {
            if (criteria.Count == Math.Min(budget.Criteria, MaxCriteriaPerStory))
            {
                break;
            }

            if (Field(raw.Given, budget) is { } given && Field(raw.When, budget) is { } when && Field(raw.Then, budget) is { } then)
            {
                criteria.Add(new Criterion(given, when, then));
            }
        }

        if (criteria.Count == 0)
        {
            criteria.Add(DefaultCriterion(covers[0], Quote(texts[covers[0]], budget)));
        }

        return new Story(
            title,
            Field(item.AsA, budget) ?? GenericRole,
            wants,
            Field(item.SoThat, budget) ?? GenericBenefit,
            covers,
            criteria);
    }

    // Clean one-line text, clipped to the rung; null when the model left it empty or wrote a placeholder.
    private static string? Field(string? raw, LayoutBudget budget) =>
        EpicsReply.Clean(raw) is { } text ? DraftComposer.Clip(text, budget.FieldClip) : null;
}
