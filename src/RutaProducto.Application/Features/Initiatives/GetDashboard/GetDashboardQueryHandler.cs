using RutaProducto.Application.Abstractions.Authentication;
using MediatR;

namespace RutaProducto.Application.Features.Initiatives.GetDashboard;

public sealed class GetDashboardQueryHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GetDashboardQuery, DashboardView>
{
    public async Task<DashboardView> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        // Today the owner set is the caller alone; a future team view only changes how this set is built.
        var snapshots = await repository.ListDashboardSnapshotsAsync([ownerId], cancellationToken);

        return DashboardCalculator.Calculate(snapshots, timeProvider.GetUtcNow());
    }
}
