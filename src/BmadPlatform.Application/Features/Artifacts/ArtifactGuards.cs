using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.GetConversationAnswers;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Artifacts;

/// <summary>Checks and reads shared by the document use cases, so each rule is worded and enforced once.</summary>
internal static class ArtifactGuards
{
    /// <summary>The initiative of the current user; the same not-found whether it is foreign, deleted or unknown.</summary>
    public static async Task<InitiativeDetails> GetInitiativeAsync(ISender sender, Guid initiativeId, CancellationToken cancellationToken) =>
        await sender.Send(new GetInitiativeQuery(initiativeId), cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

    public static async Task<ConversationAnswers> GetAnswersAsync(ISender sender, Guid initiativeId, CancellationToken cancellationToken) =>
        await sender.Send(new GetConversationAnswersQuery(initiativeId), cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

    /// <summary>Edit, regenerate and approve need a started initiative and a type its level still requires.</summary>
    public static void RequireManageable(InitiativeDetails initiative, ArtifactType type)
    {
        if (initiative.Status == InitiativeStatus.Draft)
        {
            throw new DomainException(ArtifactTexts.DraftInitiative);
        }

        if (!ArtifactCatalog.RequiredFor(initiative.Depth).Any(slot => slot.Type == type))
        {
            throw new DomainException(ArtifactTexts.NotForLevel);
        }
    }

    public static void RequireAvailable(ArtifactAvailability availability)
    {
        if (ArtifactTexts.ReasonFor(availability) is { } reason)
        {
            throw new DomainException(reason);
        }
    }

    public static Artifact RequireGenerated(Artifact? artifact) =>
        artifact ?? throw new DomainException(ArtifactTexts.NotGenerated);

    public static void RequireVersion(Artifact artifact, int expectedVersion)
    {
        if (artifact.Version != expectedVersion)
        {
            throw new ConflictException(ArtifactTexts.Conflict);
        }
    }

    /// <summary>
    /// The shared edit path; <paramref name="content"/> is already normalized and validated. The same text touches
    /// nothing. Otherwise Planning is reopened first (a failed save then leaves Planning with approved documents, which
    /// the next approval completes, never Ready to build with a Draft), then the document is edited and saved.
    /// </summary>
    public static async Task SaveEditAsync(
        ISender sender,
        IArtifactRepository repository,
        TimeProvider timeProvider,
        InitiativeDetails initiative,
        Artifact artifact,
        string content,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        if (string.Equals(content, artifact.Content, StringComparison.Ordinal))
        {
            return;
        }

        if (initiative.Status == InitiativeStatus.ReadyToBuild)
        {
            await sender.Send(new ReopenPlanningCommand(initiative.Id), cancellationToken);
        }

        artifact.Edit(content, timeProvider.GetUtcNow());
        await repository.SaveAsync(artifact, expectedVersion, cancellationToken);
    }

    /// <summary>
    /// The text of the Brief the PRD may build on: only for a Standard PRD, and only while the owner's Brief is Approved
    /// at this moment. Every other case (a Draft or missing Brief, Small, Large, another user's Brief) is null.
    /// </summary>
    public static async Task<string?> ApprovedBriefAsync(
        IArtifactRepository repository,
        Guid initiativeId,
        string ownerId,
        ArtifactType type,
        InitiativeDepth depth,
        CancellationToken cancellationToken)
    {
        if (type != ArtifactType.Prd || depth != InitiativeDepth.Standard)
        {
            return null;
        }

        var brief = await repository.GetAsync(initiativeId, ArtifactType.Brief, ownerId, cancellationToken);

        return brief is { WasApproved: true } ? brief.Content : null;
    }

    /// <summary>Reads everything the view needs, fresh. Null when the initiative does not exist for the caller.</summary>
    public static async Task<ArtifactView?> LoadViewAsync(
        ISender sender,
        IArtifactRepository repository,
        string ownerId,
        Guid initiativeId,
        ArtifactType type,
        CancellationToken cancellationToken)
    {
        var initiative = await sender.Send(new GetInitiativeQuery(initiativeId), cancellationToken);

        if (initiative is null)
        {
            return null;
        }

        var answers = await sender.Send(new GetConversationAnswersQuery(initiativeId), cancellationToken);
        var artifact = await repository.GetAsync(initiativeId, type, ownerId, cancellationToken);
        var summaries = await repository.ListAsync(initiativeId, ownerId, cancellationToken);

        return ArtifactViewBuilder.Build(initiative, answers, type, artifact, summaries);
    }

    /// <summary>The result of a command: the stored state, read again after the writes.</summary>
    public static async Task<ArtifactView> ReloadViewAsync(
        ISender sender,
        IArtifactRepository repository,
        string ownerId,
        Guid initiativeId,
        ArtifactType type,
        CancellationToken cancellationToken) =>
        await LoadViewAsync(sender, repository, ownerId, initiativeId, type, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);
}
