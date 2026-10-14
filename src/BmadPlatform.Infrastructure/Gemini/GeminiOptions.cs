using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace BmadPlatform.Infrastructure.Gemini;

/// <summary>
/// Settings under <c>Gemini:</c>. Read and validated only when <c>Assistant:Provider</c> is Gemini. Failure messages
/// name the option and never echo a value, so the key cannot leak through a startup error.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    private static readonly string[] ThinkingLevels = ["low", "medium", "high"];

    /// <summary>No default: it comes from the environment variable <c>Gemini__ApiKey</c> or user-secrets.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.8-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public string ThinkingLevel { get; set; } = "low";

    public int TurnTimeoutSeconds { get; set; } = 60;

    public int DraftTimeoutSeconds { get; set; } = 120;

    public int MaxConcurrentCalls { get; set; } = 2;

    public static GeminiOptions BindAndValidate(IConfiguration configuration)
    {
        var options = new GeminiOptions();

        options.ApiKey = configuration[Key(nameof(ApiKey))] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Assistant:Provider is Gemini but the API key is not configured; the assistant cannot start without it. " +
                "Set the environment variable Gemini__ApiKey (or the user-secret Gemini:ApiKey).");
        }

        options.Model = Text(configuration, nameof(Model), options.Model);
        options.BaseUrl = Text(configuration, nameof(BaseUrl), options.BaseUrl);
        options.ThinkingLevel = Text(configuration, nameof(ThinkingLevel), options.ThinkingLevel).ToLowerInvariant();
        options.TurnTimeoutSeconds = Positive(configuration, nameof(TurnTimeoutSeconds), options.TurnTimeoutSeconds);
        options.DraftTimeoutSeconds = Positive(configuration, nameof(DraftTimeoutSeconds), options.DraftTimeoutSeconds);
        options.MaxConcurrentCalls = Positive(configuration, nameof(MaxConcurrentCalls), options.MaxConcurrentCalls);

        if (!ThinkingLevels.Contains(options.ThinkingLevel))
        {
            throw Invalid(nameof(ThinkingLevel), "must be one of: low, medium, high");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl)
            || (baseUrl.Scheme != Uri.UriSchemeHttps && baseUrl.Scheme != Uri.UriSchemeHttp))
        {
            throw Invalid(nameof(BaseUrl), "must be an absolute http or https URL");
        }

        return options;
    }

    private static string Key(string name) => $"{SectionName}:{name}";

    // Absent keeps the default; a present but blank value is an error.
    private static string Text(IConfiguration configuration, string name, string fallback)
    {
        var value = configuration[Key(name)];

        if (value is null)
        {
            return fallback;
        }

        return string.IsNullOrWhiteSpace(value) ? throw Invalid(name, "must not be empty") : value.Trim();
    }

    private static int Positive(IConfiguration configuration, string name, int fallback)
    {
        var value = configuration[Key(name)];

        if (value is null)
        {
            return fallback;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw Invalid(name, "must be a positive integer");
    }

    private static InvalidOperationException Invalid(string name, string rule) =>
        new($"'{Key(name)}' {rule}.");
}
