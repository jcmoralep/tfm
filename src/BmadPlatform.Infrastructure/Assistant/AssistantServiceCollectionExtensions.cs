using BmadPlatform.Application.Features.Assistant;
using Microsoft.Extensions.DependencyInjection;

namespace BmadPlatform.Infrastructure.Assistant;

public static class AssistantServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Gemini-worded assistant. It needs the Gemini client (<c>AddGeminiClient</c>), which holds the
    /// options and the concurrency guard.
    /// </summary>
    public static IServiceCollection AddGeminiAssistant(this IServiceCollection services) =>
        services.AddSingleton<IAssistantService, GeminiAssistantService>();
}
