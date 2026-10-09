namespace BmadPlatform.Domain.Initiatives;

/// <summary>Who decides the depth: the user (<see cref="Manual"/>) or the assistant later (<see cref="Automatic"/>).</summary>
public enum DepthMode
{
    Manual,
    Automatic,
}
