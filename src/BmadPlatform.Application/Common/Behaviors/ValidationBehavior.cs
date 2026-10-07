using FluentValidation;
using MediatR;

namespace BmadPlatform.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the handler.
/// Throws <see cref="ValidationException"/> when any rule fails, so the handler is never reached.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators as IReadOnlyCollection<IValidator<TRequest>> ?? validators.ToList();

        if (validatorList.Count == 0)
        {
            return await next(cancellationToken);
        }

        // One context per validator: a shared context accumulates failures and would report them twice.
        var results = await Task.WhenAll(
            validatorList.Select(validator =>
                validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
