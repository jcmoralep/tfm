namespace RutaProducto.Application.Abstractions.Registration;

/// <summary>
/// The optional shared invite code that gates registration. When <see cref="Required"/> is false the form shows no field
/// and <see cref="Matches"/> always answers true. Implementations compare in constant time and never log either value.
/// </summary>
public interface IInviteCodePolicy
{
    bool Required { get; }

    bool Matches(string? typedCode);
}
