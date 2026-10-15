using RutaProducto.Application.Abstractions.Authentication;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.Register;

public sealed record RegisterCommand(string Email, string Password, string ConfirmPassword, string? CaptchaToken = null) : IRequest<RegistrationResult>
{
    // Records print every property by default; never expose the passwords or the captcha token through ToString.
    public override string ToString() => nameof(RegisterCommand);
}
