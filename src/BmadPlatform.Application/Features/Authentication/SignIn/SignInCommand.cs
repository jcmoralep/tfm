using BmadPlatform.Application.Abstractions.Authentication;
using MediatR;

namespace BmadPlatform.Application.Features.Authentication.SignIn;

public sealed record SignInCommand(string Email, string Password, bool RememberMe = false) : IRequest<AuthenticationResult>
{
    // Records print every property by default; never expose the password through ToString.
    public override string ToString() => nameof(SignInCommand);
}
