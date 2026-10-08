using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Common.Behaviors;

/// <summary>
/// Outermost pipeline behavior. Logs the request name and its duration only.
/// The request payload is never logged because commands may carry initiative content or credentials.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next(cancellationToken);

            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds:0.0} ms",
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return response;
        }
        catch (Exception exception)
        {
            // A validation failure is expected user input handled by the UI (warning); anything else is a defect (error).
            // Only the exception type is logged here: messages may echo request data.
            logger.Log(
                exception is ValidationException ? LogLevel.Warning : LogLevel.Error,
                "{RequestName} failed after {ElapsedMilliseconds:0.0} ms with {ExceptionType}",
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                exception.GetType().Name);

            throw;
        }
    }
}
