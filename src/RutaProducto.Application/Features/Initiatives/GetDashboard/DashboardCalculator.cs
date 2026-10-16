using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Features.Initiatives.GetDashboard;

/// <summary>
/// Every dashboard rule, as a pure function of the initiatives of an owner set and the current time.
/// It does not know who owns what, so a team view can reuse it with the initiatives of several people.
/// </summary>
public static class DashboardCalculator
{
    public const int WeeksShown = 8;
    public const int ResumeCount = 3;
    public const int RecentCount = 5;

    /// <summary>An unfinished initiative without activity for this long (or longer) counts as "sin movimiento".</summary>
    public static readonly TimeSpan StalledAfter = TimeSpan.FromDays(7);

    public static DashboardView Calculate(IReadOnlyCollection<DashboardInitiative> initiatives, DateTimeOffset now)
    {
        var total = initiatives.Count;
        var ready = initiatives.Count(i => i.Status == InitiativeStatus.ReadyToBuild);

        var completionPercent = total == 0
            ? 0
            : (int)Math.Round(ready * 100.0 / total, MidpointRounding.AwayFromZero);

        // Only initiatives that are ready right now have a cycle: a reopened one is back in Planning.
        var cycles = initiatives
            .Where(i => i.Status == InitiativeStatus.ReadyToBuild && i.ReadyToBuildAt is not null)
            .Select(i => Math.Max(0, (i.ReadyToBuildAt!.Value - i.CreatedAt).TotalDays))
            .ToList();

        double? averageDays = cycles.Count == 0 ? null : Math.Round(cycles.Average(), 1, MidpointRounding.AwayFromZero);

        var stalled = initiatives.Count(i => i.Status != InitiativeStatus.ReadyToBuild && now - i.ActivityAt >= StalledAfter);

        var statusCounts = Enum.GetValues<InitiativeStatus>()
            .Select(status => new DashboardStatusCount(status, initiatives.Count(i => i.Status == status)))
            .ToList();

        var depths = Enum.GetValues<InitiativeDepth>()
            .Select(depth => new DashboardDepthCount(depth, initiatives.Count(i => i.Depth == depth)))
            .ToList();

        var byRecency = initiatives
            .OrderByDescending(i => i.ActivityAt)
            .ThenByDescending(i => i.Id)
            .ToList();

        var resume = byRecency
            .Where(i => i.Status != InitiativeStatus.ReadyToBuild)
            .Take(ResumeCount)
            .Select(ToItem)
            .ToList();

        var recent = byRecency.Take(RecentCount).Select(ToItem).ToList();

        return new DashboardView(
            total, completionPercent, averageDays, stalled, statusCounts, depths, BuildWeeks(initiatives, now), resume, recent);
    }

    /// <summary>The Monday (UTC) that starts the ISO week of the moment.</summary>
    public static DateOnly WeekStart(DateTimeOffset moment)
    {
        var date = DateOnly.FromDateTime(moment.UtcDateTime);

        return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }

    private static List<DashboardWeek> BuildWeeks(IReadOnlyCollection<DashboardInitiative> initiatives, DateTimeOffset now)
    {
        var currentWeek = WeekStart(now);
        var weeks = new List<DashboardWeek>(WeeksShown);

        for (var offset = WeeksShown - 1; offset >= 0; offset--)
        {
            var start = currentWeek.AddDays(-7 * offset);

            // Each initiative is counted in the week of its own dates; anything outside the window is ignored.
            var created = initiatives.Count(i => WeekStart(i.CreatedAt) == start);
            var becameReady = initiatives.Count(i =>
                i.Status == InitiativeStatus.ReadyToBuild && i.ReadyToBuildAt is { } at && WeekStart(at) == start);

            weeks.Add(new DashboardWeek(start, created, becameReady));
        }

        return weeks;
    }

    private static DashboardItem ToItem(DashboardInitiative i) => new(i.Id, i.Name, i.Status, i.ActivityAt);
}
