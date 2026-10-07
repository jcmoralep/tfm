using Serilog.Context;

namespace BmadPlatform.Web.Logging;

/// <summary>
/// Adds a CorrelationId to every log event written while an HTTP request is processed.
/// The id is always generated on the server (client-provided values are ignored to avoid log injection)
/// and returned in the response header so a user report can be matched with the logs.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string PropertyName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString("N");

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(PropertyName, correlationId))
        {
            await next(context);
        }
    }
}
