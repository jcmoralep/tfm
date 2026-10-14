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
/// The model's items by slot id (the topic key for a slot fed by a topic); a slot absent from the map has no items.
/// <paramref name="CarriedQuestions"/> are the open questions of the approved Brief that the PRD must not lose.
/// </summary>
internal sealed record DraftItems(
    IReadOnlyDictionary<string, IReadOnlyList<DraftItem>> BySlot,
    IReadOnlyList<string>? CarriedQuestions = null)
{
    public IReadOnlyList<DraftItem> Of(string slotId) =>
        BySlot.TryGetValue(slotId, out var items) ? items : [];
}
