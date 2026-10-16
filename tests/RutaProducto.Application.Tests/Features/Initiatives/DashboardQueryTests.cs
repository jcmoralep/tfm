using RutaProducto.Application.Features.Initiatives.GetDashboard;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Initiatives;

public sealed class DashboardQueryTests
{
    private readonly InitiativeTestContext context = new();

    private Task<DashboardView> Dashboard() =>
        new GetDashboardQueryHandler(context.Repository, context.User, context.Clock)
            .Handle(new GetDashboardQuery(), default);

    [Fact]
    public async Task The_dashboard_counts_only_the_callers_own_initiatives()
    {
        await context.CreateDraft("Mía 1");
        await context.CreateClarifying("Mía 2");

        context.User.UserId = InitiativeTestContext.UserB;
        await context.CreateDraft("De otra persona");
        await context.CreateDraft("De otra persona 2");
        await context.CreateDraft("De otra persona 3");

        context.User.UserId = InitiativeTestContext.UserA;
        var view = await Dashboard();

        Assert.Equal(2, view.Total);
        Assert.DoesNotContain(view.Recent, item => item.Name.StartsWith("De otra", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deleted_initiatives_are_not_counted()
    {
        var id = await context.CreateDraft("Para borrar");
        await context.CreateDraft("Se queda");
        await context.Delete(id);

        Assert.Equal(1, (await Dashboard()).Total);
    }

    [Fact]
    public async Task Marking_ready_records_the_moment_and_reopening_clears_it()
    {
        var id = await context.CreateClarifying("Lista pronto");
        context.ForceStatus(id, InitiativeStatus.Planning);
        await context.MarkReadyToBuild(id);

        Assert.Equal(context.Clock.GetUtcNow(), context.Repository.Stored.Single().ReadyToBuildAt);
        Assert.Equal(100, (await Dashboard()).CompletionPercent);

        await context.ReopenPlanning(id);

        Assert.Null(context.Repository.Stored.Single().ReadyToBuildAt);
        Assert.Equal(0, (await Dashboard()).CompletionPercent);
    }
}
