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
    DateTimeOffset UpdatedAt);
