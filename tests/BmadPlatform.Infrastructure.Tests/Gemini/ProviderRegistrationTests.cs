using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Infrastructure.Artifacts;
using BmadPlatform.Infrastructure.Assistant;
using BmadPlatform.Infrastructure.Gemini;
using BmadPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BmadPlatform.Infrastructure.Tests.Gemini;

public sealed class ProviderRegistrationTests
{
    private const string Key = "K-12345";

    [Theory]
    [InlineData(null, AssistantProvider.Fake)]
    [InlineData("", AssistantProvider.Fake)]
    [InlineData("Fake", AssistantProvider.Fake)]
    [InlineData("gemini", AssistantProvider.Gemini)]
    [InlineData("GEMINI", AssistantProvider.Gemini)]
    public void The_provider_is_read_case_insensitively_and_defaults_to_fake(string? value, AssistantProvider expected)
    {
        Assert.Equal(expected, AssistantProviderSetting.Read(Config(("Assistant:Provider", value))));
    }

    [Fact]
    public void An_unknown_provider_fails_naming_the_setting_and_the_valid_values()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => AssistantProviderSetting.Read(Config(("Assistant:Provider", "OpenAI"))));

        Assert.Contains("Assistant:Provider", error.Message);
        Assert.Contains("Fake, Gemini", error.Message);
    }

    [Fact]
    public void Defaults_apply_when_only_the_key_is_set()
    {
        var options = GeminiOptions.BindAndValidate(Config(("Gemini:ApiKey", Key)));

        Assert.Equal(Key, options.ApiKey);
        Assert.Equal("gemini-3.8-flash", options.Model);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta", options.BaseUrl);
        Assert.Equal("low", options.ThinkingLevel);
        Assert.Equal((30, 90, 2), (options.TurnTimeoutSeconds, options.DraftTimeoutSeconds, options.MaxConcurrentCalls));
    }

    [Fact]
    public void Overrides_are_honored()
    {
        var options = GeminiOptions.BindAndValidate(Config(
            ("Gemini:ApiKey", Key),
            ("Gemini:Model", "other-model"),
            ("Gemini:ThinkingLevel", "HIGH"),
            ("Gemini:TurnTimeoutSeconds", "10"),
            ("Gemini:MaxConcurrentCalls", "1"),
            ("Gemini:BaseUrl", "http://localhost:9000/v1")));

        Assert.Equal("other-model", options.Model);
        Assert.Equal("high", options.ThinkingLevel);
        Assert.Equal(10, options.TurnTimeoutSeconds);
        Assert.Equal(1, options.MaxConcurrentCalls);
    }

    [Theory]
    [InlineData("ApiKey", null)]
    [InlineData("ApiKey", "")]
    [InlineData("ApiKey", "   ")]
    public void A_missing_or_blank_key_fails_naming_the_variable(string option, string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => GeminiOptions.BindAndValidate(Config(($"Gemini:{option}", value))));

        Assert.Contains("Gemini__ApiKey", error.Message);
    }

    [Theory]
    [InlineData("ThinkingLevel", "minimal")]
    [InlineData("ThinkingLevel", "")]
    [InlineData("TurnTimeoutSeconds", "0")]
    [InlineData("TurnTimeoutSeconds", "abc")]
    [InlineData("DraftTimeoutSeconds", "-5")]
    [InlineData("MaxConcurrentCalls", "0")]
    [InlineData("BaseUrl", "not a url")]
    [InlineData("BaseUrl", "ftp://example.com")]
    [InlineData("Model", " ")]
    public void An_invalid_option_fails_naming_it_and_never_echoing_the_key(string option, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => GeminiOptions.BindAndValidate(Config(("Gemini:ApiKey", Key), ($"Gemini:{option}", value))));

        Assert.Contains($"Gemini:{option}", error.Message);
        Assert.DoesNotContain(Key, error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Fake")]
    public void Fake_needs_nothing_from_gemini_and_resolves_both_fakes(string? provider)
    {
        // An invalid Gemini option and no key: nothing under Gemini is read when the provider is Fake.
        var configuration = Config(("Assistant:Provider", provider), ("Gemini:ThinkingLevel", "minimal"));

        using var serviceProvider = Build(configuration);

        Assert.IsType<FakeAssistantService>(serviceProvider.GetRequiredService<IAssistantService>());
        Assert.IsType<FakeArtifactDrafter>(serviceProvider.GetRequiredService<IArtifactDrafter>());
    }

    [Fact]
    public void Gemini_with_an_empty_key_fails_at_startup_naming_the_variable()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build(Config(("Assistant:Provider", "Gemini"))));

        Assert.Contains("Gemini__ApiKey", error.Message);
    }

    [Fact]
    public void Gemini_with_valid_options_still_refuses_to_start_until_the_client_exists()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build(Config(("Assistant:Provider", "gemini"), ("Gemini:ApiKey", Key))));

        Assert.Contains("not implemented", error.Message);
        Assert.DoesNotContain(Key, error.Message);
    }

    private static ServiceProvider Build(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Never used to connect; it only satisfies the provider configuration.
                    [$"ConnectionStrings:{MySqlDatabase.ConnectionStringName}"] = "Server=localhost;Database=bmadplatform_tests;User=tests",
                }.Concat(values.Select(v => KeyValuePair.Create(v.Key, v.Value))))
            .Build();
}
