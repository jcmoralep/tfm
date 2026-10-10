namespace BmadPlatform.Application.Features.Assistant.Script;

/// <summary>The phase the journey panel marks as current.</summary>
public enum JourneyStep
{
    Clarify,
    Plan,
    ReadyToBuild,
}

/// <summary>Covered topics out of required topics for one phase. Confirmation and proposals are never counted.</summary>
public sealed record PhaseProgress(int Covered, int Total)
{
    public int Remaining => Math.Max(0, Total - Covered);

    public bool IsComplete => Covered >= Total;
}

/// <summary>Where a conversation stands, derived on read from the initiative and the visible answers.</summary>
/// <param name="Step">The current phase, from the initiative status.</param>
/// <param name="NextTopic">What to ask next; null when nothing can be asked (Draft, ReadyToBuild, or no level yet).</param>
/// <param name="Clarify">Progress of the Aclarar topics.</param>
/// <param name="Plan">Progress of the Planificar topics; null when the level has none (Small) or is not set yet.</param>
/// <param name="PlanUndefined">True while the level is not set, so Planificar is "Se define al elegir el nivel".</param>
/// <param name="Overall">Aclarar plus Planificar; null while the level is not set, so no total is shown.</param>
/// <param name="PendingTransition">True when the user confirmed planning but the initiative is still Clarifying.</param>
public sealed record JourneySnapshot(
    JourneyStep Step,
    AssistantTopic? NextTopic,
    PhaseProgress Clarify,
    PhaseProgress? Plan,
    bool PlanUndefined,
    PhaseProgress? Overall,
    bool PendingTransition)
{
    /// <summary>For example "2 de 8 temas cubiertos"; null when the total is not shown.</summary>
    public string? ProgressText => Overall is null ? null : $"{Overall.Covered} de {Overall.Total} temas cubiertos";
}
