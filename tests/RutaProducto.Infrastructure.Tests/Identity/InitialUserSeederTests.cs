using RutaProducto.Infrastructure.Identity;
using RutaProducto.Infrastructure.Identity.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class InitialUserSeederTests
{
    [Fact]
    public async Task Seeded_users_are_created_with_a_confirmed_email_so_they_keep_working_when_confirmation_is_required()
    {
        using var provider = TokenTestHost.Build(new InMemoryUserStore());
        using var scope = provider.CreateScope();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedUsers:0:Email"] = "seed@example.com",
                ["SeedUsers:0:Password"] = "Secret-123",
            })
            .Build();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await new InitialUserSeeder(users, configuration, NullLogger<InitialUserSeeder>.Instance).SeedAsync(CancellationToken.None);

        var seeded = await users.FindByEmailAsync("seed@example.com");
        Assert.NotNull(seeded);
        Assert.True(seeded.EmailConfirmed);
    }
}
