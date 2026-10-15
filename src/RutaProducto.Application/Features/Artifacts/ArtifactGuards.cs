using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Assistant.GetConversationAnswers;
using RutaProducto.Application.Features.Context.GetContextForAssistant;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using RutaProducto.Application.Features.Initiatives.ReopenPlanning;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Initiatives;
using MediatR;

namespace RutaProducto.Application.Features.Artifacts;

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
    /// Builds the whole drafting request, so Generate and Regenerate read the same inputs. The Brief goes into a Standard
    /// PRD, the PRD into the Architecture and the Epics, and the Architecture into the Epics, each only while that document
    /// is Approved at this moment (a Draft or missing one, or another user's, is null). The Architecture and the Epics
    /// carry no conversation answers: the approved text is their source, and no attached context either. The Spec, the
    /// Brief and the PRD carry the text-like context of the initiative.
    /// </summary>
    public static async Task<DraftRequest> DraftRequestAsync(
        ISender sender,
        IArtifactRepository repository,
        InitiativeDetails initiative,
        string ownerId,
        ArtifactType type,
        ConversationAnswers answers,
        CancellationToken cancellationToken)
    {
        var depth = initiative.Depth!.Value;
        var brief = type == ArtifactType.Prd && depth == InitiativeDepth.Standard
            ? await ApprovedTextAsync(repository, initiative.Id, ownerId, ArtifactType.Brief, cancellationToken)
            : null;
        var chained = type is ArtifactType.Architecture or ArtifactType.EpicsAndStories;
        var prd = chained
            ? await ApprovedTextAsync(repository, initiative.Id, ownerId, ArtifactType.Prd, cancellationToken)
            : null;
        var architecture = type == ArtifactType.EpicsAndStories
            ? await ApprovedTextAsync(repository, initiative.Id, ownerId, ArtifactType.Architecture, cancellationToken)
            : null;
        var context = chained
            ? null
            : await sender.Send(new GetContextForAssistantQuery(initiative.Id), cancellationToken);

        return new DraftRequest(
            type,
            initiative.Name,
            initiative.Description,
            depth,
            chained ? [] : answers.Answers,
            brief,
            prd,
            architecture,
            context);
    }

    private static async Task<string?> ApprovedTextAsync(
        IArtifactRepository repository,
        Guid initiativeId,
        string ownerId,
        ArtifactType type,
        CancellationToken cancellationToken)
    {
        var artifact = await repository.GetAsync(initiativeId, type, ownerId, cancellationToken);

        return artifact is { WasApproved: true } ? artifact.Content : null;
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
