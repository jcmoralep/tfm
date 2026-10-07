using BmadPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BmadPlatform.Infrastructure.Persistence;

/// <summary>
/// Single application database. <see cref="IdentityUserContext{TUser}"/> is used instead of
/// <c>IdentityDbContext</c> because the MVP has no roles, so no role tables are created.
/// Always obtain instances through <see cref="IDbContextFactory{TContext}"/> in Blazor Server code.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityUserContext<ApplicationUser>(options)
{
}
