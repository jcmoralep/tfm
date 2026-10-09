namespace BmadPlatform.Domain.Initiatives;

/// <summary>Lifecycle of an initiative. It is never edited by hand; it only changes through domain operations.</summary>
public enum InitiativeStatus
{
    Draft,
    Clarifying,
    Planning,
    ReadyToBuild,
}
