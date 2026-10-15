using RutaProducto.Application.Abstractions.Authentication;
using FluentValidation;
using MediatR;

namespace RutaProducto.Application.Features.Authentication.ConfirmEmail;

/// <summary>Confirms an address from the emailed link. The result is only true or false, so every failure looks the same.</summary>
public sealed record ConfirmEmailCommand(string UserId, string Code) : IRequest<bool>
{
    // The code is a credential; never print it.
    public override string ToString() => nameof(ConfirmEmailCommand);
}

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public const int MaxCodeLength = 2048;

    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().MaximumLength(128).WithMessage("El enlace no es válido o venció. Pide uno nuevo.");
        RuleFor(command => command.Code).NotEmpty().MaximumLength(MaxCodeLength).WithMessage("El enlace no es válido o venció. Pide uno nuevo.");
    }
}

public sealed class ConfirmEmailCommandHandler(IAccountTokenService tokens) : IRequestHandler<ConfirmEmailCommand, bool>
{
    public Task<bool> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken) =>
        tokens.ConfirmEmailAsync(request.UserId, request.Code, cancellationToken);
}
