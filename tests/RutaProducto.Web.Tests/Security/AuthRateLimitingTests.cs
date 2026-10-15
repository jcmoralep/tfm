using System.Net;
using RutaProducto.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace RutaProducto.Web.Tests.Security;

public sealed class AuthRateLimitingTests
{
    [Theory]
    [InlineData("POST", "/login", true)]
    [InlineData("POST", "/LOGIN", true)]
    [InlineData("POST", "/login/", true)]
    [InlineData("POST", "/LOGIN/", true)]
    [InlineData("POST", "/registro/", true)]
    [InlineData("POST", "/confirmar-correo/reenviar/", true)]
    [InlineData("POST", "/recuperar-contrasena/", true)]
    [InlineData("POST", "/restablecer-contrasena/", true)]
    [InlineData("POST", "/logout/", false)]
    [InlineData("GET", "/login", false)]
    [InlineData("POST", "/registro", true)]
    [InlineData("POST", "/confirmar-correo/reenviar", true)]
    [InlineData("POST", "/recuperar-contrasena", true)]
    [InlineData("POST", "/restablecer-contrasena", true)]
    [InlineData("POST", "/logout", false)]
    [InlineData("POST", "/_blazor", false)]
    [InlineData("POST", "/iniciativas", false)]
    public void Only_auth_posts_are_selected(string method, string path, bool expected)
    {
        var context = Context(method, path, "10.0.0.1");

        Assert.Equal(expected, AuthRateLimiting.IsLimited(context.Request));
    }

    [Fact]
    public void The_partition_is_the_client_ip_or_unknown()
    {
        Assert.NotEqual(
            AuthRateLimiting.PartitionKey(Context("POST", "/login", "10.0.0.1")),
            AuthRateLimiting.PartitionKey(Context("POST", "/login", "10.0.0.2")));
        Assert.Equal("unknown", AuthRateLimiting.PartitionKey(Context("POST", "/login", null)));
    }

    [Fact]
    public void The_eleventh_post_of_an_ip_is_refused_while_other_ips_and_gets_are_not()
    {
        var options = RateLimitOptions.BindAndValidate(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:Enabled"] = "true" })
            .Build());
        using var limiter = AuthRateLimiting.CreateLimiter(options);

        for (var i = 0; i < 10; i++)
        {
            using var lease = limiter.AttemptAcquire(Context("POST", "/login", "10.0.0.1"));
            Assert.True(lease.IsAcquired);
        }

        using var refused = limiter.AttemptAcquire(Context("POST", "/registro", "10.0.0.1"));
        Assert.False(refused.IsAcquired);

        using var otherIp = limiter.AttemptAcquire(Context("POST", "/login", "10.0.0.2"));
        Assert.True(otherIp.IsAcquired);

        using var get = limiter.AttemptAcquire(Context("GET", "/login", "10.0.0.1"));
        Assert.True(get.IsAcquired);
    }

    private static DefaultHttpContext Context(string method, string path, string? ip)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = ip is null ? null : IPAddress.Parse(ip);

        return context;
    }
}
