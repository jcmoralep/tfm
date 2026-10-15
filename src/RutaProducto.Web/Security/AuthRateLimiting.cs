using System.Threading.RateLimiting;
using RutaProducto.Web.Authentication;
using Microsoft.AspNetCore.RateLimiting;

namespace RutaProducto.Web.Security;

/// <summary>
/// A global limiter that throttles only the authentication form POSTs: one fixed-window bucket per client IP shared
/// by all of them. Everything else (pages, the circuit, other POSTs) is never counted.
/// </summary>
internal static class AuthRateLimiting
{
    private static readonly string[] LimitedPaths =
    [
        AuthenticationEndpoints.LoginPath,
        AuthenticationEndpoints.RegisterPath,
        AuthenticationEndpoints.ResendConfirmationPath,
        AuthenticationEndpoints.ForgotPasswordPath,
        AuthenticationEndpoints.ResetPasswordPath,
    ];

    // Routing serves "/login/" as "/login", so one trailing slash must not slip past the limiter.
    public static bool IsLimited(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        var path = (request.Path.Value ?? string.Empty).TrimEnd('/');

        return Array.Exists(LimitedPaths, limited => string.Equals(limited, path, StringComparison.OrdinalIgnoreCase));
    }

    public static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static PartitionedRateLimiter<HttpContext> CreateLimiter(RateLimitOptions options) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            !IsLimited(context.Request)
                ? RateLimitPartition.GetNoLimiter("not-limited")
                : RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = options.Window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));

    public static void Configure(RateLimiterOptions limiter, RateLimitOptions options)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.GlobalLimiter = CreateLimiter(options);
        limiter.OnRejected = (rejected, _) =>
        {
            var retryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                ? delay
                : options.Window;

            rejected.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Path only: no client IP, no form content.
            rejected.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AuthRateLimiting).FullName!)
                .LogWarning("Auth POST throttled on {Path}", rejected.HttpContext.Request.Path.Value);

            return ValueTask.CompletedTask;
        };
    }
}
