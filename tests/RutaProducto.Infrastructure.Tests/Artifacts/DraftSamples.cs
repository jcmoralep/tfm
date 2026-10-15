using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Assistant.Script;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;
using RutaProducto.Infrastructure.Artifacts;

namespace RutaProducto.Infrastructure.Tests.Artifacts;

/// <summary>Builds drafting requests the way the answers query would return them for a finished conversation.</summary>
internal static class DraftSamples
{
    public const string Name = "Consulta de pedidos";

    /// <summary>The content topics of the level in script order.</summary>
    public static IReadOnlyList<string> Topics(InitiativeDepth depth) =>
    [
        .. AssistantScript.ClarifyTopics(DepthMode.Manual, depth)
            .Concat(AssistantScript.PlanTopics(depth) ?? [])
            .Where(key => AssistantScript.Get(key).Kind == TopicKind.Question),
    ];

    public static TopicAnswer Answer(string key, string text, bool unknown = false) =>
        new(key, AssistantScript.Get(key).Prompt, text, unknown);

    /// <summary>A "nothing to list" quick reply such as "Ninguno": its label is the text, flagged so no document makes an item of it.</summary>
    public static TopicAnswer None(string key, string label) =>
        new(key, AssistantScript.Get(key).Prompt, label, IsUnknown: false, IsNone: true);

    public static TopicAnswer Unknown(string key) => Answer(key, AssistantScript.UnknownLabel, unknown: true);

    /// <summary>Every topic answered with a text derived from its key, unless overridden.</summary>
    public static List<TopicAnswer> Answers(
        InitiativeDepth depth,
        IReadOnlyDictionary<string, string>? texts = null,
        IReadOnlyCollection<string>? unknown = null) =>
    [
        .. Topics(depth).Select(key => unknown?.Contains(key) == true
            ? Unknown(key)
            : Answer(key, texts?.GetValueOrDefault(key) ?? $"respuesta de {key}")),
    ];

    public static DraftRequest Request(
        ArtifactType type,
        InitiativeDepth depth,
        IEnumerable<TopicAnswer>? answers = null,
        string? description = "Consultar el estado de los pedidos sin llamar a bodega.") =>
        new(type, Name, description, depth, [.. answers ?? Answers(depth)]);

    public static Task<string> Draft(DraftRequest request) => new FakeArtifactDrafter().DraftAsync(request, default);

    /// <summary>The level each document type is drafted for in the tests that go through every combination.</summary>
    public static IEnumerable<(ArtifactType Type, InitiativeDepth Depth)> Combinations() =>
    [
        (ArtifactType.Spec, InitiativeDepth.Small),
        (ArtifactType.Brief, InitiativeDepth.Standard),
        (ArtifactType.Prd, InitiativeDepth.Standard),
        (ArtifactType.Prd, InitiativeDepth.Large),
    ];
}
