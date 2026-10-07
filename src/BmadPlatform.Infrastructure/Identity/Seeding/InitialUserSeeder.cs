using BmadPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Infrastructure.Identity.Seeding;

/// <summary>
/// Creates the configured initial users at startup. Idempotent: existing users are left untouched,
/// so passwords changed later are never overwritten. Emails and passwords are never logged.
/// </summary>
internal sealed class InitialUserSeeder(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<InitialUserSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seedUsers = configuration.GetSection(SeedUser.SectionName).Get<List<SeedUser>>() ?? [];

        if (seedUsers.Count == 0)
        {
            logger.LogWarning("No seed users configured. Set SeedUsers__0__Email and SeedUsers__0__Password to create one");
            return;
        }

        var created = 0;

        for (var index = 0; index < seedUsers.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var email = seedUsers[index].Email?.Trim();
            var password = seedUsers[index].Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                logger.LogWarning("Seed user {SeedUserIndex} is missing email or password and was skipped", index);
                continue;
            }

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                created++;
                continue;
            }

            logger.LogError(
                "Seed user {SeedUserIndex} could not be created: {IdentityErrorCodes}",
                index,
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        logger.LogInformation(
            "User seeding finished: {CreatedCount} created, {ConfiguredCount} configured",
            created,
            seedUsers.Count);
    }
}

public static class InitialUserSeederExtensions
{
    public static async Task SeedInitialUsersAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            PendingMigrationsGuard.EnsureNonePending(pending);
        }

        var seeder = scope.ServiceProvider.GetRequiredService<InitialUserSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
