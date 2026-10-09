namespace BmadPlatform.Domain.Initiatives;

/// <summary>Wizard step to reopen when a draft is resumed. Only meaningful while the initiative is a draft.</summary>
public enum CreationStep
{
    Details,
    Depth,
    Review,
}
