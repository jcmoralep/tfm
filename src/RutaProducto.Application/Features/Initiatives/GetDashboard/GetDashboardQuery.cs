using MediatR;

namespace RutaProducto.Application.Features.Initiatives.GetDashboard;

/// <summary>
/// The home dashboard of the caller. It carries no data: the owner scope comes from the current user, and
/// nothing about the initiatives is logged.
/// </summary>
public sealed record GetDashboardQuery : IRequest<DashboardView>
{
    public override string ToString() => nameof(GetDashboardQuery);
}
