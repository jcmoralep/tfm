using RutaProducto.Application.Features.Artifacts;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Artifacts;

public static class ArtifactServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Gemini document drafter. It needs the Gemini client (<c>AddGeminiClient</c>), which holds the
    /// options and the concurrency guard.
    /// </summary>
    public static IServiceCollection AddGeminiDrafter(this IServiceCollection services) =>
        services.AddSingleton<IArtifactDrafter, GeminiArtifactDrafter>();
}
