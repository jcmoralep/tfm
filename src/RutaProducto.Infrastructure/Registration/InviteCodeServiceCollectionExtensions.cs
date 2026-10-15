using Microsoft.Extensions.DependencyInjection;
using RutaProducto.Application.Abstractions.Registration;

namespace RutaProducto.Infrastructure.Registration;

public static class InviteCodeServiceCollectionExtensions
{
    /// <summary>Registers the invite code policy. Without <c>Registration:InviteCode</c> it is off and registration is unchanged.</summary>
    public static IServiceCollection AddInviteCode(this IServiceCollection services, InviteCodeOptions options)
    {
        services.AddSingleton<IInviteCodePolicy>(new InviteCodePolicy(options.InviteCode));

        return services;
    }
}
