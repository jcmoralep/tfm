using System.Text.Json.Nodes;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// Reads and filters the model's reply for the Epics: <c>{epics: [{title, goal, stories: [{title, as_a, i_want, so_that,
/// covers, criteria: [{given, when, then}]}]}], questions: []}</c>. The structure must be exact (the root, the keys and the
/// JSON types), otherwise the reply is unusable. Content is then filtered by what the approved PRD allows: a story without a
/// title or an <c>i_want</c>, or without a functional requirement the PRD defines, is dropped, and so is an epic left with no
/// story. When nothing survives the reply is unusable too. Counts the model gave are never trusted: the layout caps them.
/// </summary>
internal static class EpicsReply
{
    public static bool TryParse(JsonNode json, PrdFacts prd, out DraftItems items)
    {
        items = new DraftItems(new Dictionary<string, IReadOnlyList<DraftItem>>());

        if (json is not JsonObject root
            || root.Count != 2
            || root["epics"] is not JsonArray entries
            || !TryStrings(root["questions"], out var questions))
        {
            return false;
        }

        var epics = new List<EpicItem>();

        foreach (var entry in entries)
        {
            if (!TryEpic(entry, prd, out var epic))
            {
                return false;
            }

            if (epic.Stories.Count > 0)
            {
                epics.Add(epic);
            }
        }

        if (epics.Count == 0)
        {
            return false;
        }

        items = new DraftItems(new Dictionary<string, IReadOnlyList<DraftItem>>(), DevelopmentQuestions: questions, Epics: epics);

        return true;
    }

    /// <summary>One-line clean text, or null when the model left it empty or wrote a placeholder.</summary>
    internal static string? Clean(string? raw) =>
        !ArchitectureLayout.HasPlaceholder(raw) && DraftComposer.ModelText(raw) is { Length: > 0 } text ? text : null;

    /// <summary>The identifiers the PRD defines among the raw citations: canonical, de-duplicated and in PRD order.</summary>
    internal static List<string> ValidCovers(IEnumerable<string> raw, PrdFacts prd) =>
    [
        .. raw.Select(PrdFacts.CanonicalId)
            .OfType<string>()
            .Where(prd.Knows)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(prd.PositionOf),
    ];

    private static bool TryEpic(JsonNode? node, PrdFacts prd, out EpicItem epic)
    {
        epic = new EpicItem(string.Empty, string.Empty, []);

        if (node is not JsonObject obj
            || !GeminiArtifactDrafter.TryText(obj["title"], out var title)
            || !GeminiArtifactDrafter.TryText(obj["goal"], out var goal)
            || obj["stories"] is not JsonArray entries)
        {
            return false;
        }

        var stories = new List<StoryItem>();

        foreach (var entry in entries)
        {
            if (!TryStory(entry, out var story))
            {
                return false;
            }

            if (Clean(story.Title) is not null
                && Clean(story.IWant) is not null
                && ValidCovers(story.Covers, prd).Any(id => id.StartsWith("FR-", StringComparison.Ordinal)))
            {
                stories.Add(story);
            }
        }

        epic = new EpicItem(title, goal, stories);

        return true;
    }

    private static bool TryStory(JsonNode? node, out StoryItem story)
    {
        story = new StoryItem(string.Empty, string.Empty, string.Empty, string.Empty, [], []);

        if (node is not JsonObject obj
            || !GeminiArtifactDrafter.TryText(obj["title"], out var title)
            || !GeminiArtifactDrafter.TryText(obj["as_a"], out var asA)
            || !GeminiArtifactDrafter.TryText(obj["i_want"], out var iWant)
            || !GeminiArtifactDrafter.TryText(obj["so_that"], out var soThat)
            || !TryStrings(obj["covers"], out var covers)
            || obj["criteria"] is not JsonArray entries)
        {
            return false;
        }

        var criteria = new List<CriterionItem>();

        foreach (var entry in entries)
        {
            if (entry is not JsonObject criterion
                || !GeminiArtifactDrafter.TryText(criterion["given"], out var given)
                || !GeminiArtifactDrafter.TryText(criterion["when"], out var when)
                || !GeminiArtifactDrafter.TryText(criterion["then"], out var then))
            {
                return false;
            }

            criteria.Add(new CriterionItem(given, when, then));
        }

        story = new StoryItem(title, asA, iWant, soThat, covers, criteria);

        return true;
    }

    private static bool TryStrings(JsonNode? node, out List<string> values)
    {
        values = [];

        if (node is not JsonArray entries)
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (!GeminiArtifactDrafter.TryText(entry, out var text))
            {
                return false;
            }

            values.Add(text);
        }

        return true;
    }
}
