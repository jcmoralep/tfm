using Microsoft.AspNetCore.Components.Server.Circuits;
using Serilog.Context;

namespace BmadPlatform.Web.Logging;

/// <summary>
/// Adds a CorrelationId to log events raised inside an interactive Blazor circuit
/// (UI events, MediatR requests), which the HTTP middleware does not cover.
/// One id per circuit, so every action of a user session can be followed in the logs.
/// </summary>
internal sealed class CorrelationIdCircuitHandler : CircuitHandler
{
    private readonly string correlationId = Guid.NewGuid().ToString("N");

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            using (LogContext.PushProperty(CorrelationIdMiddleware.PropertyName, correlationId))
            {
                await next(context);
            }
        };
}
