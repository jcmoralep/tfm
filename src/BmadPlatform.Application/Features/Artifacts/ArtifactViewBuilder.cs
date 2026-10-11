using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>The single projection of the documents, shared by the queries and the commands.</summary>
public static class ArtifactViewBuilder
{
    public static ArtifactView Build(
        InitiativeDetails initiative,
        ConversationAnswers? answers,
        ArtifactType type,
        Artifact? artifact,
        IReadOnlyList<ArtifactSummary> summaries)
    {
        var availability = ArtifactCatalog.Availability(type, initiative.Status, initiative.Depth, answers);
        var required = ArtifactCatalog.RequiredFor(initiative.Depth).Any(slot => slot.Type == type);

        // A document of a type the level no longer requires is kept but hidden, and nothing can be done with it.
        var shown = artifact is not null && required ? artifact : null;
        var open = initiative.Status != InitiativeStatus.Draft;

        // Approving needs the coverage of the document to still hold: an undone answer takes it back.
        var canApprove = shown is { Status: ArtifactStatus.Draft } && open && availability == ArtifactAvailability.Available;
        var planning = initiative.Status == InitiativeStatus.Planning;
        var covered = ArtifactCatalog.IsSetAvailable(initiative.Status, initiative.Depth, answers);

        return new ArtifactView(
            initiative.Id,
            initiative.Name,
            initiative.Status,
            initiative.Depth,
            type,
            shown is not null,
            availability,
            shown?.Version ?? 0,
            shown?.Status,
            shown?.Content ?? string.Empty,
            shown?.UpdatedAt,
            shown?.ApprovedAt,
            shown is null ? [] : ArtifactFormatChecker.Check(type, shown.Content),
            CanGenerate: artifact is null && availability == ArtifactAvailability.Available,
            CanEdit: shown is not null && open,
            CanRegenerate: shown is not null && availability == ArtifactAvailability.Available,
            CanApprove: canApprove,
            ApprovalCompletesSet: canApprove && planning && covered && ArtifactCatalog.IsSetComplete(initiative.Depth, WithApproved(summaries, type)),
            CompletionPending: planning && covered && ArtifactCatalog.IsSetComplete(initiative.Depth, summaries));
    }

    public static ArtifactsOverview BuildOverview(
        InitiativeDetails initiative,
        ConversationAnswers? answers,
        IReadOnlyList<ArtifactSummary> summaries)
    {
        if (initiative.Status == InitiativeStatus.Draft)
        {
            return new ArtifactsOverview(initiative.Id, [], ArtifactTexts.DraftInitiative, false);
        }

        var slots = ArtifactCatalog.RequiredFor(initiative.Depth).Select(slot => SlotOf(initiative, answers, summaries, slot)).ToList();

        return new ArtifactsOverview(
            initiative.Id,
            slots,
            null,
            initiative.Status == InitiativeStatus.Planning
                && ArtifactCatalog.IsSetAvailable(initiative.Status, initiative.Depth, answers)
                && ArtifactCatalog.IsSetComplete(initiative.Depth, summaries));
    }

    private static ArtifactSlotView SlotOf(
        InitiativeDetails initiative,
        ConversationAnswers? answers,
        IReadOnlyList<ArtifactSummary> summaries,
        ArtifactSlot slot)
    {
        if (slot.Type is not { } type)
        {
            return new ArtifactSlotView(slot.Label, null, ArtifactAvailability.NotYetSupported, false, null, 0, ArtifactTexts.ComingLater);
        }

        var availability = ArtifactCatalog.Availability(type, initiative.Status, initiative.Depth, answers);
        var summary = summaries.FirstOrDefault(s => s.Type == type);

        return summary is null
            ? new ArtifactSlotView(slot.Label, type, availability, false, null, 0, ArtifactTexts.ReasonFor(availability))
            : new ArtifactSlotView(slot.Label, type, availability, true, summary.Status, summary.Version, null);
    }

    // The set as it would be once this type is approved; used to tell whether approving it completes the level.
    private static List<ArtifactSummary> WithApproved(IReadOnlyList<ArtifactSummary> summaries, ArtifactType type) =>
    [
        .. summaries.Where(s => s.Type != type),
        .. summaries.Where(s => s.Type == type).Select(s => s with { Status = ArtifactStatus.Approved }),
    ];
}
