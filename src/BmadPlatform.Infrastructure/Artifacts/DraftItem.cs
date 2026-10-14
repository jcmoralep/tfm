namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>Where a model-provided item comes from: what the person said, a guess to confirm, or a gap.</summary>
internal enum DraftItemSource
{
    Answer,
    Assumption,
    Unknown,
}

/// <summary>One entry of a section as the model returned it. The text is raw: the composer cleans it before writing.</summary>
internal sealed record DraftItem(string Text, DraftItemSource Source);

/// <summary>
/// One architecture decision as the model (or the fake) proposes it. The binds are raw requirement identifiers: the
/// layout canonicalises them and keeps only those the approved PRD defines. The code numbers the decision.
/// </summary>
internal sealed record DecisionItem(string Title, IReadOnlyList<string> Binds, string Prevents, string Rule);

/// <summary>
/// The model's items by slot id (the topic key for a slot fed by a topic); a slot absent from the map has no items.
/// <paramref name="CarriedQuestions"/> are the open questions of the approved Brief that the PRD must not lose;
/// <paramref name="Decisions"/> and <paramref name="DevelopmentQuestions"/> belong to the Architecture.
/// </summary>
internal sealed record DraftItems(
    IReadOnlyDictionary<string, IReadOnlyList<DraftItem>> BySlot,
    IReadOnlyList<string>? CarriedQuestions = null,
    IReadOnlyList<DecisionItem>? Decisions = null,
    IReadOnlyList<string>? DevelopmentQuestions = null)
{
    public IReadOnlyList<DraftItem> Of(string slotId) =>
        BySlot.TryGetValue(slotId, out var items) ? items : [];
}
