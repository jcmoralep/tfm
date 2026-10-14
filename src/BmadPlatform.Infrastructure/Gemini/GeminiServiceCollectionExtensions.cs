using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BmadPlatform.Infrastructure.Gemini;

public static class GeminiServiceCollectionExtensions
{
    public const string HttpClientName = "Gemini";

    /// <summary>
    /// Registers the validated options and the named HTTP client. The client owns its deadline (so the HTTP timeout is
    /// infinite) and the factory loggers are removed so request URIs and headers are never logged.
    /// <see cref="GeminiClient"/> is a singleton so its concurrency guard is shared by every caller.
    /// </summary>
    public static IServiceCollection AddGeminiClient(this IServiceCollection services, GeminiOptions options)
    {
        services.AddSingleton(Options.Create(options));
        services
            .AddHttpClient(HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers();
        services.AddSingleton<GeminiClient>();

        return services;
    }
}
