using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Initiatives;

public sealed record InitiativeListFilter(string? Search, InitiativeStatus? Status, InitiativeDepth? Depth);

public sealed record InitiativeSummary(
    Guid Id,
    string Name,
    InitiativeStatus Status,
    DepthMode? DepthMode,
    InitiativeDepth? Depth,
    DateTimeOffset UpdatedAt);

public sealed record InitiativeDetails(
    Guid Id,
    string Name,
    string? Description,
    InitiativeStatus Status,
    DepthMode? DepthMode,
    InitiativeDepth? Depth,
    CreationStep CreationStep,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Text shown instead of the depth while the assistant has not suggested one; <c>null</c> otherwise.</summary>
    public string? DepthPendingText =>
        DepthMode == Domain.Initiatives.DepthMode.Automatic && Depth is null ? InitiativeTexts.DepthPending : null;
}

/// <summary>User-facing texts shared by the initiatives use cases.</summary>
public static class InitiativeTexts
{
    public const string DepthPending = "Pendiente de sugerencia";

    public const string NotFound = "La iniciativa no existe.";
}
