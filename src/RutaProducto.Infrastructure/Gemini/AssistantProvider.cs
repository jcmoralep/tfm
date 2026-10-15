using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Gemini;

/// <summary>Which implementation backs both the assistant and the document drafter (<c>Assistant:Provider</c>).</summary>
public enum AssistantProvider
{
    Fake,
    Gemini,
}

public static class AssistantProviderSetting
{
    public const string ConfigurationKey = "Assistant:Provider";

    /// <summary>Absent or empty means <see cref="AssistantProvider.Fake"/>; the match is case-insensitive.</summary>
    public static AssistantProvider Read(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            return AssistantProvider.Fake;
        }

        if (Enum.TryParse<AssistantProvider>(value.Trim(), ignoreCase: true, out var provider)
            && Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"'{ConfigurationKey}' has an unknown value. Valid values are: Fake, Gemini.");
    }
}
