using RutaProducto.Application.Features.Assistant.Script;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Infrastructure.Artifacts;

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
    public const string NonUsers = "non-users";
    public const string Glossary = "glossary";
    public const string Paradigm = "paradigm";
    public const string Deferred = "deferred";

    private const string MetricRule = "Every metric needs a target, a way to measure it and a counter-metric; add the missing ones as assumption or unknown items.";

    private static readonly DraftSlot[] Spec =
    [
        Topic(AssistantScript.Keys.Idea, "Por qué", "The idea in one or two plain sentences."),
        Topic(AssistantScript.Keys.Users, "Por qué", "Who will use it: the roles or groups of people, in one or two items."),
        Topic(AssistantScript.Keys.Problem, "Por qué", "The problem or pain that this solves."),
        Topic(AssistantScript.Keys.Capabilities, "Capacidades", "What a person must be able to do, one capability per item. Leave out anything the person said can wait: it goes to the out-of-scope slot."),
        Topic(AssistantScript.Keys.OutOfScope, "No objetivos", "What is explicitly left out for now, plus anything the person herself said can wait, phrased as deferred. At least one item."),
        Topic(AssistantScript.Keys.Success, "Señal de éxito", "How success will be noticed. " + MetricRule),
        Open(AssistantScript.Keys.Constraints, "Restricciones", true, "Rules, limits or deadlines. Small initiatives are not asked about them: use answer only if the answers already state one."),
    ];

    private static readonly DraftSlot[] Brief =
    [
        Topic(AssistantScript.Keys.Problem, "El problema", "The problem or pain, who has it and why it matters."),
        Topic(AssistantScript.Keys.Idea, "La solución", "The proposed solution in plain words."),
        Topic(AssistantScript.Keys.Users, "A quién sirve", "The people or groups who will use it."),
        Topic(AssistantScript.Keys.Success, "Criterios de éxito", "How success will be noticed. " + MetricRule),
        Topic(AssistantScript.Keys.OutOfScope, "Alcance, fuera", "What is explicitly left out for now, plus anything the person herself said can wait, phrased as deferred."),
        Open(Differentiator, "Qué lo hace distinto", false, "What sets this apart from other options. The person was never asked: assumption or unknown only."),
        Open(ScopeInside, "Alcance, dentro", true, "What is inside the scope. Use answer only for what the answers state."),
        Open(Vision, "Visión", true, "The long-term vision. Use answer only for what the answers state."),
    ];

    private static readonly DraftSlot[] PrdStandard =
    [
        Topic(AssistantScript.Keys.Idea, "Visión", "The vision: what the solution is and the change it brings, in one or two plain sentences."),
        Topic(AssistantScript.Keys.Users, "Usuario objetivo", "Who will use it: the roles or groups of people."),
        Topic(AssistantScript.Keys.Problem, "Trabajos por hacer", "The jobs people need to get done, each tied to the problem."),
        Open(NonUsers, "No usuarios", false, "Who will not use it. The person was never asked: assumption or unknown only."),
        Open(Glossary, "Glosario", false, "Terms specific to the initiative. The person was never asked: assumption or unknown only."),
        Topic(AssistantScript.Keys.Capabilities, "Funcionalidades", "What a person must be able to do, one capability per item. Leave out anything the person said can wait: it goes to the out-of-scope slot."),
        Topic(AssistantScript.Keys.Constraints, "Requisitos no funcionales", "Rules, limits or deadlines the solution must respect, one per item. Bounds and numbers the person did not give are assumptions or unknowns."),
        Topic(AssistantScript.Keys.OutOfScope, "No objetivos", "What is explicitly left out for now, plus anything the person herself said can wait, phrased as deferred. At least one item."),
        Topic(AssistantScript.Keys.Priorities, "Alcance del MVP", "What matters most and what comes first. Do not restate deferred items here."),
        Topic(AssistantScript.Keys.Success, "Métricas de éxito", "How success will be noticed. " + MetricRule),
    ];

    private static readonly DraftSlot[] PrdLarge =
    [
        .. PrdStandard.TakeWhile(slot => slot.Id != AssistantScript.Keys.OutOfScope),
        Topic(AssistantScript.Keys.Qualities, "Requisitos no funcionales", "Conditions of speed, security or availability, one per item. Any bound the person did not give is an assumption or unknown."),
        Topic(AssistantScript.Keys.Integrations, "Integraciones", "The systems or teams the solution must coordinate with, one per item."),
        .. PrdStandard.SkipWhile(slot => slot.Id != AssistantScript.Keys.OutOfScope),
    ];

    /// <summary>
    /// The slots the model fills for a type and level. Only the PRD depends on the level: the integrations and the quality
    /// conditions are asked (and drafted) for Large alone, so they are not slots of a Standard PRD.
    /// </summary>
    public static IReadOnlyList<DraftSlot> For(ArtifactType type, InitiativeDepth depth = InitiativeDepth.Standard) => type switch
    {
        ArtifactType.Spec => Spec,
        ArtifactType.Brief => Brief,
        ArtifactType.Prd => depth == InitiativeDepth.Large ? PrdLarge : PrdStandard,
        _ => throw new NotSupportedException($"The model does not draft {type}."),
    };

    private static DraftSlot Topic(string key, string section, string guidance) =>
        new(key, section, key, true, guidance);

    private static DraftSlot Open(string id, string section, bool allowsAnswer, string guidance) =>
        new(id, section, null, allowsAnswer, guidance);
}
