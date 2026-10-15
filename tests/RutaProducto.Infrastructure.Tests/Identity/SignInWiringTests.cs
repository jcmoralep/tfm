using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Tests.Identity;

/// <summary>
/// The real SignInManager and UserManager over the in-memory store, with confirmation required. These fail if the lockout
/// check, the failure counting or the dummy-hash call is removed from <see cref="IdentityAuthService"/>.
/// </summary>
public sealed class SignInWiringTests
{
    private const string Email = "ana@example.com";
    private const string Password = "Secret-123";

    [Fact]
    public async Task Wrong_passwords_on_an_unconfirmed_account_count_and_the_fifth_locks_it()
    {
        using var host = Host.Build();
        var user = await host.AddUserAsync(Email, confirmed: false);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(AuthenticationResult.InvalidCredentials, await host.SignIn.SignInAsync(Email, "Wrong-password-1", false));
            Assert.Equal(attempt, user.AccessFailedCount);
        }

        Assert.Equal(AuthenticationResult.LockedOut, await host.SignIn.SignInAsync(Email, "Wrong-password-1", false));
    }

    [Fact]
    public async Task A_locked_unconfirmed_account_shows_the_lock_even_with_the_right_password()
    {
        using var host = Host.Build();
        var user = await host.AddUserAsync(Email, confirmed: false);
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);

        Assert.Equal(AuthenticationResult.LockedOut, await host.SignIn.SignInAsync(Email, Password, false));
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task The_right_password_on_an_unconfirmed_account_says_not_allowed_and_counts_nothing()
    {
        using var host = Host.Build();
        var user = await host.AddUserAsync(Email, confirmed: false);

        Assert.Equal(AuthenticationResult.NotAllowed, await host.SignIn.SignInAsync(Email, Password, false));
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task An_unknown_email_pays_one_dummy_hash_verification_and_a_known_one_does_not_add_another()
    {
        using var host = Host.Build();
        await host.AddUserAsync(Email, confirmed: true);

        Assert.Equal(AuthenticationResult.InvalidCredentials, await host.SignIn.SignInAsync("nobody@example.com", "Wrong-password-1", false));
        Assert.Equal(1, host.Hasher.Verifications);

        // A known account is verified once by Identity itself and the dummy hash is not used again.
        Assert.Equal(AuthenticationResult.InvalidCredentials, await host.SignIn.SignInAsync(Email, "Wrong-password-1", false));
        Assert.Equal(2, host.Hasher.Verifications);
    }

    private sealed class CountingHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> inner = new();

        public int Verifications { get; private set; }

        public string HashPassword(ApplicationUser user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            Verifications++;
            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;

        private Host(ServiceProvider provider, CountingHasher hasher)
        {
            this.provider = provider;
            scope = provider.CreateScope();
            Hasher = hasher;
            SignIn = scope.ServiceProvider.GetRequiredService<IdentityAuthService>();
        }

        public CountingHasher Hasher { get; }

        public IdentityAuthService SignIn { get; }

        public static Host Build()
        {
            var hasher = new CountingHasher();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddAuthentication();
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
            services.AddSingleton<IUserStore<ApplicationUser>>(new InMemoryUserStore());
            services.AddSingleton<IPasswordHasher<ApplicationUser>>(hasher);
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            }).AddSignInManager();
            services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, UserClaimsPrincipalFactory<ApplicationUser>>();
            services.AddScoped<IdentityAuthService>();

            return new Host(services.BuildServiceProvider(), hasher);
        }

        public async Task<ApplicationUser> AddUserAsync(string email, bool confirmed) =>
            await TokenTestHost.AddUserAsync(scope.ServiceProvider, email, confirmed);

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }
}
