using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RutaProducto.Infrastructure.Persistence;

/// <summary>
/// Single application database. <see cref="IdentityUserContext{TUser}"/> is used instead of
/// <c>IdentityDbContext</c> because the MVP has no roles, so no role tables are created.
/// Always obtain instances through <see cref="IDbContextFactory{TContext}"/> in Blazor Server code.
/// It also implements <see cref="IDataProtectionKeyContext"/> so the Data Protection keys can live in the same database.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityUserContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Each module keeps its own IEntityTypeConfiguration next to its entities; the context only applies them.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
