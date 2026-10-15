using RutaProducto.Application.Abstractions.Registration;
using RutaProducto.Infrastructure.Persistence;
using RutaProducto.Infrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Tests.Registration;

public sealed class InviteCodeTests
{
    private const string Code = "Invite-Code-2026";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_code_the_feature_is_off_and_accepts_anything(string? configured)
    {
        var policy = new InviteCodePolicy(InviteCodeOptions.BindAndValidate(Config(configured)).InviteCode);

        Assert.False(policy.Required);
        Assert.True(policy.Matches(null));
        Assert.True(policy.Matches("whatever"));
    }

    [Theory]
    [InlineData(Code, true)]
    [InlineData("  Invite-Code-2026  ", true)]
    [InlineData("invite-code-2026", false)]
    [InlineData("Invite-Code-2027", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void With_a_code_only_the_exact_trimmed_case_sensitive_text_matches(string? typed, bool expected)
    {
        var policy = new InviteCodePolicy(InviteCodeOptions.BindAndValidate(Config($"  {Code}  ")).InviteCode);

        Assert.True(policy.Required);
        Assert.Equal(expected, policy.Matches(typed));
    }

    [Theory]
    [InlineData("qwerty7")]
    [InlineData("  elevenchars ")]
    public void A_code_shorter_than_twelve_characters_stops_startup_without_echoing_it(string configured)
    {
        var error = Assert.Throws<InvalidOperationException>(() => InviteCodeOptions.BindAndValidate(Config(configured)));

        Assert.Contains("Registration:InviteCode", error.Message);
        Assert.DoesNotContain(configured.Trim(), error.Message);
    }

    [Fact]
    public void Registration_follows_the_setting()
    {
        using var off = Build(null);
        Assert.False(off.GetRequiredService<IInviteCodePolicy>().Required);

        using var on = Build(Code);
        Assert.True(on.GetRequiredService<IInviteCodePolicy>().Required);
    }

    private static IConfiguration Config(string? code) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>(InviteCodeOptions.InviteCodeKey, code)])
            .Build();

    private static ServiceProvider Build(string? code)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Never used to connect; it only satisfies the provider configuration.
                    [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=rutaproducto_tests;User=tests",
                    [InviteCodeOptions.InviteCodeKey] = code,
                })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
