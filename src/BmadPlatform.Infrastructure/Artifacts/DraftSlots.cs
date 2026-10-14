using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// One section the model can fill. <paramref name="Id"/> is the topic key for a slot fed by a topic.
/// <paramref name="AllowsAnswer"/> is false for content the person was never asked for (a differentiator), where the
/// model may only guess or ask. <paramref name="Guidance"/> is English and model-facing: the minimum BMAD expects.
/// </summary>
internal sealed record DraftSlot(string Id, string Section, string? TopicKey, bool AllowsAnswer, string Guidance);

/// <summary>
/// The slots of each document, derived from the outline and the layout call sites. A slot fed by a topic uses the
/// topic key as its id; the others (sections no topic feeds) have the ids below.
/// </summary>
internal static class DraftSlots
{
    public const string Differentiator = "differentiator";
    public const string ScopeInside = "scope-inside";
    public const string Vision = "vision";

    private const string MetricRule = "Every metric needs a target, a way to measure it and a counter-metric; add the missing ones as assumption or unknown items.";

    private static readonly DraftSlot[] Spec =
    [
        Topic(AssistantScript.Keys.Idea, "Por qué", "The idea in one or two plain sentences."),
        Topic(AssistantScript.Keys.Users, "Por qué", "Who will use it: the roles or groups of people, in one or two items."),
        Topic(AssistantScript.Keys.Problem, "Por qué", "The problem or pain that this solves."),
        Topic(AssistantScript.Keys.Capabilities, "Capacidades", "What a person must be able to do, one capability per item."),
        Topic(AssistantScript.Keys.OutOfScope, "No objetivos", "What is explicitly left out for now. At least one item."),
        Topic(AssistantScript.Keys.Success, "Señal de éxito", "How success will be noticed. " + MetricRule),
        Open(AssistantScript.Keys.Constraints, "Restricciones", true, "Rules, limits or deadlines. Small initiatives are not asked about them: use answer only if the answers already state one."),
    ];

    private static readonly DraftSlot[] Brief =
    [
        Topic(AssistantScript.Keys.Problem, "El problema", "The problem or pain, who has it and why it matters."),
        Topic(AssistantScript.Keys.Idea, "La solución", "The proposed solution in plain words."),
        Topic(AssistantScript.Keys.Users, "A quién sirve", "The people or groups who will use it."),
        Topic(AssistantScript.Keys.Success, "Criterios de éxito", "How success will be noticed. " + MetricRule),
        Topic(AssistantScript.Keys.OutOfScope, "Alcance, fuera", "What is explicitly left out for now."),
        Open(Differentiator, "Qué lo hace distinto", false, "What sets this apart from other options. The person was never asked: assumption or unknown only."),
        Open(ScopeInside, "Alcance, dentro", true, "What is inside the scope. Use answer only for what the answers state."),
        Open(Vision, "Visión", true, "The long-term vision. Use answer only for what the answers state."),
    ];

    /// <summary>The slots the model fills for a type. The PRD has none yet: its drafter arrives in the next slice.</summary>
    public static IReadOnlyList<DraftSlot> For(ArtifactType type) => type switch
    {
        ArtifactType.Spec => Spec,
        ArtifactType.Brief => Brief,
        _ => throw new NotSupportedException($"The model does not draft {type} yet."),
    };

    private static DraftSlot Topic(string key, string section, string guidance) =>
        new(key, section, key, true, guidance);

    private static DraftSlot Open(string id, string section, bool allowsAnswer, string guidance) =>
        new(id, section, null, allowsAnswer, guidance);
}
