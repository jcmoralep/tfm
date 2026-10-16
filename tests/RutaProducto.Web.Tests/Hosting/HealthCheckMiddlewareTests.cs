using System.Text;
using RutaProducto.Web.Hosting;
using Microsoft.AspNetCore.Http;

namespace RutaProducto.Web.Tests.Hosting;

/// <remarks>
/// Covers what the middleware does. That it sits first in the pipeline (before HTTPS redirection and the rate limiter) is
/// a line in <c>Program.cs</c> with no test seam without a host; it is left to the runtime verification.
/// </remarks>
public sealed class HealthCheckMiddlewareTests
{
    [Theory]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/HEALTH")]
    [InlineData("GET", "/health/")]
    [InlineData("HEAD", "/health")]
    public async Task The_probe_answers_200_without_reaching_the_rest_of_the_pipeline(string method, string path)
    {
        var (context, nextCalled) = await Run(method, path);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(method == "GET" ? "ok" : string.Empty, ReadBody(context));
    }

    [Theory]
    [InlineData("GET", "/")]
    [InlineData("GET", "/health/deep")]
    [InlineData("GET", "/healthz")]
    [InlineData("POST", "/health")]
    public async Task Any_other_request_goes_on_down_the_pipeline_untouched(string method, string path)
    {
        var (context, nextCalled) = await Run(method, path);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(context));
    }

    private static async Task<(DefaultHttpContext Context, bool NextCalled)> Run(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await new HealthCheckMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        return (context, nextCalled);
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;

        return Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
    }
}
