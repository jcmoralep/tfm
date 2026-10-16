using RutaProducto.Application.Features.Initiatives.GetDashboard;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Initiatives;

public sealed class DashboardCalculatorTests
{
    // A Friday, in the first week of 2027 (ISO week starting Monday 2026-12-28).
    private static readonly DateTimeOffset Now = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static DashboardInitiative Item(
        InitiativeStatus status = InitiativeStatus.Draft,
        InitiativeDepth? depth = null,
        DateTimeOffset? created = null,
        DateTimeOffset? updated = null,
        DateTimeOffset? readyAt = null,
        string name = "Idea",
        DateTimeOffset? activity = null) =>
        new(Guid.NewGuid(), name, status, depth, created ?? Now.AddDays(-1), updated ?? Now.AddDays(-1), readyAt, activity);

    private static DashboardInitiative Ready(DateTimeOffset created, DateTimeOffset readyAt) =>
        Item(InitiativeStatus.ReadyToBuild, created: created, updated: readyAt, readyAt: readyAt);

    [Fact]
    public void Without_initiatives_everything_is_zero_and_the_chart_still_has_its_weeks()
    {
        var view = DashboardCalculator.Calculate([], Now);

        Assert.Equal(0, view.Total);
        Assert.Equal(0, view.CompletionPercent);
        Assert.Null(view.AverageDaysToReady);
        Assert.Equal(0, view.StalledCount);
        Assert.All(view.StatusCounts, c => Assert.Equal(0, c.Count));
        Assert.Equal(4, view.StatusCounts.Count);
        Assert.Equal(DashboardCalculator.WeeksShown, view.Weeks.Count);
        Assert.Empty(view.Resume);
        Assert.Empty(view.Recent);
    }

    [Fact]
    public void Counts_follow_the_lifecycle_order_and_depth_levels_ignore_unset_ones()
    {
        var view = DashboardCalculator.Calculate(
            [
                Item(InitiativeStatus.Draft),
                Item(InitiativeStatus.Clarifying, InitiativeDepth.Small),
                Item(InitiativeStatus.Clarifying, InitiativeDepth.Large),
                Item(InitiativeStatus.Planning, InitiativeDepth.Large),
                Item(InitiativeStatus.ReadyToBuild, InitiativeDepth.Large, readyAt: Now),
            ],
            Now);

        Assert.Equal(
            [InitiativeStatus.Draft, InitiativeStatus.Clarifying, InitiativeStatus.Planning, InitiativeStatus.ReadyToBuild],
            view.StatusCounts.Select(c => c.Status));
        Assert.Equal([1, 2, 1, 1], view.StatusCounts.Select(c => c.Count));
        Assert.Equal(
            [(InitiativeDepth.Small, 1), (InitiativeDepth.Standard, 0), (InitiativeDepth.Large, 3)],
            view.Depths.Select(d => (d.Depth, d.Count)));
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)] // 12.5 rounds away from zero
    [InlineData(5, 5, 100)]
    public void Completion_rate_is_ready_over_total_rounded_to_a_whole_percentage(int ready, int total, int expected)
    {
        var items = Enumerable.Range(0, total)
            .Select(index => index < ready
                ? Ready(Now.AddDays(-3), Now.AddDays(-1))
                : Item(InitiativeStatus.Planning))
            .ToList();

        Assert.Equal(expected, DashboardCalculator.Calculate(items, Now).CompletionPercent);
    }

    [Fact]
    public void Average_days_to_ready_uses_only_initiatives_that_are_ready_now()
    {
        var view = DashboardCalculator.Calculate(
            [
                Ready(Now.AddDays(-10), Now.AddDays(-8)), // 2 days
                Ready(Now.AddDays(-20), Now.AddDays(-15)), // 5 days
                Item(InitiativeStatus.Planning, created: Now.AddDays(-30)), // reopened: back in Planning, no ready date
                Item(InitiativeStatus.Clarifying, created: Now.AddDays(-30)),
            ],
            Now);

        Assert.Equal(3.5, view.AverageDaysToReady);
    }

    [Fact]
    public void Average_days_to_ready_is_unknown_until_one_initiative_is_ready()
    {
        var view = DashboardCalculator.Calculate([Item(InitiativeStatus.Planning)], Now);

        Assert.Null(view.AverageDaysToReady);
    }

    [Theory]
    [InlineData(InitiativeStatus.Draft, 167, false)]
    [InlineData(InitiativeStatus.Draft, 168, true)] // exactly 7 days counts
    [InlineData(InitiativeStatus.Clarifying, 169, true)]
    [InlineData(InitiativeStatus.Planning, 24 * 30, true)]
    [InlineData(InitiativeStatus.ReadyToBuild, 24 * 30, false)] // finished work is never "sin movimiento"
    public void Stalled_means_not_ready_and_untouched_for_seven_days_or_more(InitiativeStatus status, int hoursAgo, bool expected)
    {
        var view = DashboardCalculator.Calculate([Item(status, updated: Now.AddHours(-hoursAgo))], Now);

        Assert.Equal(expected ? 1 : 0, view.StalledCount);
    }

    [Fact]
    public void Recent_activity_keeps_an_old_initiative_from_being_stalled_and_ranks_it_first()
    {
        var worked = Item(InitiativeStatus.Planning, name: "Trabajada", updated: Now.AddDays(-30), activity: Now.AddHours(-1));
        var edited = Item(InitiativeStatus.Clarifying, name: "Editada", updated: Now.AddDays(-2));
        var idle = Item(InitiativeStatus.Draft, name: "Quieta", updated: Now.AddDays(-20), activity: Now.AddDays(-10));

        var view = DashboardCalculator.Calculate([idle, edited, worked], Now);

        Assert.Equal(1, view.StalledCount); // only the idle one
        Assert.Equal(["Trabajada", "Editada", "Quieta"], view.Resume.Select(i => i.Name));
        Assert.Equal(["Trabajada", "Editada", "Quieta"], view.Recent.Select(i => i.Name));
        Assert.Equal(Now.AddHours(-1), view.Recent[0].ActivityAt);
    }

    [Fact]
    public void Without_a_recorded_activity_the_last_change_decides()
    {
        var older = Item(InitiativeStatus.Planning, name: "Antigua", updated: Now.AddDays(-9));
        var newer = Item(InitiativeStatus.Planning, name: "Nueva", updated: Now.AddDays(-8), activity: null);

        var view = DashboardCalculator.Calculate([older, newer], Now);

        Assert.Equal(2, view.StalledCount);
        Assert.Equal(["Nueva", "Antigua"], view.Recent.Select(i => i.Name));
        Assert.Equal(Now.AddDays(-8), view.Recent[0].ActivityAt);
    }

    [Fact]
    public void Weeks_start_on_monday_cross_the_year_boundary_and_ignore_dates_outside_the_window()
    {
        var view = DashboardCalculator.Calculate(
            [
                Item(created: new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero)), // this week (Mon 2026-12-28)
                Item(created: new DateTimeOffset(2026, 12, 28, 0, 0, 0, TimeSpan.Zero)), // Monday 00:00 starts this week
                Item(created: new DateTimeOffset(2026, 12, 27, 23, 59, 0, TimeSpan.Zero)), // Sunday: previous week
                Item(created: new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero)), // first week of the window
                Item(created: new DateTimeOffset(2026, 11, 8, 23, 0, 0, TimeSpan.Zero)), // week before the window: ignored
                Ready(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 29, 10, 0, 0, TimeSpan.Zero)),
            ],
            Now);

        Assert.Equal(new DateOnly(2026, 11, 9), view.Weeks[0].WeekStart);
        Assert.Equal(new DateOnly(2026, 12, 28), view.Weeks[^1].WeekStart);
        Assert.All(view.Weeks, week => Assert.Equal(DayOfWeek.Monday, week.WeekStart.DayOfWeek));

        Assert.Equal(1, view.Weeks[0].Created);
        Assert.Equal(1, view.Weeks[^2].Created);
        Assert.Equal(2, view.Weeks[^1].Created);
        Assert.Equal(1, view.Weeks[^1].Ready);
        Assert.Equal(1, view.Weeks.Sum(w => w.Ready));
    }

    [Fact]
    public void A_reopened_initiative_counts_as_created_but_not_as_ready()
    {
        var created = Now.AddDays(-2);
        var reopened = Item(InitiativeStatus.Planning, created: created, readyAt: null);

        var view = DashboardCalculator.Calculate([reopened], Now);

        Assert.Equal(1, view.Weeks[^1].Created);
        Assert.Equal(0, view.Weeks.Sum(w => w.Ready));
    }

    [Fact]
    public void Resume_lists_three_unfinished_and_recent_lists_five_of_any_status_most_recent_first()
    {
        var items = Enumerable.Range(1, 7)
            .Select(day => Item(
                day == 7 ? InitiativeStatus.ReadyToBuild : InitiativeStatus.Planning,
                updated: Now.AddDays(-10).AddDays(day),
                readyAt: day == 7 ? Now : null,
                name: $"Idea {day}"))
            .ToList();

        var view = DashboardCalculator.Calculate(items, Now);

        Assert.Equal(["Idea 6", "Idea 5", "Idea 4"], view.Resume.Select(i => i.Name));
        Assert.Equal(["Idea 7", "Idea 6", "Idea 5", "Idea 4", "Idea 3"], view.Recent.Select(i => i.Name));
    }
}
