namespace RutaProducto.Web.Hosting;

/// <summary>
/// Liveness probe for the platform's health check: <c>GET /health</c> answers 200 <c>ok</c> and nothing else. It does not
/// touch the database (a database blip must not restart the container) and it is registered first in the pipeline, so it needs
/// no authentication, is not redirected to HTTPS, is not counted by the rate limiter and is not logged per call.
/// </summary>
internal sealed class HealthCheckMiddleware(RequestDelegate next)
{
    public const string Path = "/health";

    public Task InvokeAsync(HttpContext context)
    {
        var isProbe = (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && (context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.Equals(Path + "/", StringComparison.OrdinalIgnoreCase));

        if (!isProbe)
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        return HttpMethods.IsHead(context.Request.Method) ? Task.CompletedTask : context.Response.WriteAsync("ok");
    }
}
