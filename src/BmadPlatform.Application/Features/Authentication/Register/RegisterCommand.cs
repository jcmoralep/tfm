using BmadPlatform.Application.Abstractions.Authentication;
using MediatR;

namespace BmadPlatform.Application.Features.Authentication.Register;

public sealed record RegisterCommand(string Email, string Password, string ConfirmPassword) : IRequest<RegistrationResult>
{
    // Records print every property by default; never expose the passwords through ToString.
    public override string ToString() => nameof(RegisterCommand);
}
