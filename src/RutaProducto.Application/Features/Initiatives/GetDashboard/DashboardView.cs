using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Features.Initiatives.GetDashboard;

/// <summary>The minimum an initiative contributes to the dashboard figures.</summary>
public sealed record DashboardInitiative(
    Guid Id,
    string Name,
    InitiativeStatus Status,
    InitiativeDepth? Depth,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReadyToBuildAt);

public sealed record DashboardStatusCount(InitiativeStatus Status, int Count);

public sealed record DashboardDepthCount(InitiativeDepth Depth, int Count);

/// <summary>One ISO week (Monday start, UTC): initiatives created and initiatives that became ready in it.</summary>
public sealed record DashboardWeek(DateOnly WeekStart, int Created, int Ready);

public sealed record DashboardItem(Guid Id, string Name, InitiativeStatus Status, DateTimeOffset UpdatedAt);

/// <param name="CompletionPercent">Ready over total, rounded to a whole percentage; 0 without initiatives.</param>
/// <param name="AverageDaysToReady">Mean days from creation to ready, one decimal; null until one is ready.</param>
/// <param name="StalledCount">Not-ready initiatives without movement for the stalled threshold or more.</param>
/// <param name="StatusCounts">One entry per status, in lifecycle order.</param>
/// <param name="Depths">Small, Standard and Large, in that order; initiatives without a level are not counted.</param>
/// <param name="Weeks">The last weeks ending with the current one, oldest first.</param>
/// <param name="Resume">Up to three unfinished initiatives, most recently modified first.</param>
/// <param name="Recent">The last five touched initiatives of any status, most recent first.</param>
public sealed record DashboardView(
    int Total,
    int CompletionPercent,
    double? AverageDaysToReady,
    int StalledCount,
    IReadOnlyList<DashboardStatusCount> StatusCounts,
    IReadOnlyList<DashboardDepthCount> Depths,
    IReadOnlyList<DashboardWeek> Weeks,
    IReadOnlyList<DashboardItem> Resume,
    IReadOnlyList<DashboardItem> Recent);
