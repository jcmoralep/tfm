using Microsoft.AspNetCore.HttpOverrides;

namespace RutaProducto.Web.Hosting;

/// <summary>
/// Setting <c>ForwardedHeaders:Enabled</c> (off by default). Turn it on only behind a TLS-terminating proxy such as Railway's:
/// the app then trusts <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from any sender, so a client that reaches the app
/// directly could forge them and evade the per-IP rate limiter. Failure messages name the key and never echo the value.
/// </summary>
internal sealed class ForwardedHeadersSetting
{
    public const string EnabledKey = "ForwardedHeaders:Enabled";

    public bool Enabled { get; private set; }

    public static ForwardedHeadersSetting BindAndValidate(IConfiguration configuration)
    {
        var raw = configuration[EnabledKey];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ForwardedHeadersSetting();
        }

        return bool.TryParse(raw.Trim(), out var enabled)
            ? new ForwardedHeadersSetting { Enabled = enabled }
            : throw new InvalidOperationException($"'{EnabledKey}' must be true or false. Set ForwardedHeaders__Enabled or leave it empty.");
    }

    /// <summary>
    /// Railway's proxy addresses are not fixed, so the known proxies and networks are cleared: the forwarded headers
    /// are accepted from the immediate peer, whatever its address.
    /// </summary>
    public IServiceCollection AddForwardedHeadersIfEnabled(IServiceCollection services)
    {
        if (Enabled)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        return services;
    }
}
