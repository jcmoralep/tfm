using RutaProducto.Application;
using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Registration;
using RutaProducto.Infrastructure;
using RutaProducto.Infrastructure.Identity.Seeding;
using RutaProducto.Infrastructure.Persistence;
using RutaProducto.Web.Artifacts;
using RutaProducto.Web.Authentication;
using RutaProducto.Web.Components;
using RutaProducto.Web.Components.Artifacts;
using RutaProducto.Web.Context;
using RutaProducto.Web.Hosting;
using RutaProducto.Web.Logging;
using RutaProducto.Web.Security;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Serilog;

// Bootstrap logger: captures startup failures before the configured logger exists.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Railway injects PORT. Explicit URLs (ASPNETCORE_URLS, --urls) win; without PORT nothing changes.
    var portUrl = PortSetting.ResolveUrl(
        Environment.GetEnvironmentVariable(PortSetting.VariableName),
        builder.Configuration[WebHostDefaults.ServerUrlsKey]);

    if (portUrl is not null)
    {
        builder.WebHost.UseUrls(portUrl);
    }

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        // File sink, rolling and retention come from appsettings.json (Serilog section).
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services);

        // The console in every environment: the platform (Railway) reads stdout, and the file stays for local and server use.
        configuration.WriteTo.Console(
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
    });

    builder.Services
        .AddRazorComponents()
        .AddInteractiveServerComponents()
        // The editor sends up to 20,000 characters (multibyte ones included) in one message, above the 32 KB default.
        .AddHubOptions(options => options.MaximumReceiveMessageSize = 128 * 1024);

    builder.Services.AddCascadingAuthenticationState();
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "RutaProducto.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = AuthenticationEndpoints.LoginPath;
        options.AccessDeniedPath = AuthenticationEndpoints.LoginPath;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

    builder.Services.AddAuthorization();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<CircuitHandler, CorrelationIdCircuitHandler>();

    builder.Services.AddSingleton<MarkdownRenderer>();
    builder.Services.AddSingleton<IAccountLinks, AccountLinks>();

    // Off by default: with the switch off neither AddRateLimiter nor UseRateLimiter is called.
    var rateLimit = RateLimitOptions.BindAndValidate(builder.Configuration);

    if (rateLimit.Enabled)
    {
        builder.Services.AddRateLimiter(limiter => AuthRateLimiting.Configure(limiter, rateLimit));
    }

    // Off by default: with the switch off neither the options nor UseForwardedHeaders exist. Enable only behind the proxy.
    var forwardedHeaders = ForwardedHeadersSetting.BindAndValidate(builder.Configuration);
    forwardedHeaders.AddForwardedHeadersIfEnabled(builder.Services);

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // Pre-deploy mode: apply the pending migrations and exit. No Kestrel, no seeding, no middleware.
    if (MigrateMode.IsRequested(args))
    {
        Environment.ExitCode = await MigrateMode.RunAsync(
            () => app.Services.MigrateDatabaseAsync(),
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrate"));

        return;
    }

    // First on purpose: the platform's health check calls plain http without X-Forwarded-Proto, and the probe must not be
    // redirected, authenticated, rate limited or logged per call.
    app.UseMiddleware<HealthCheckMiddleware>();

    if (forwardedHeaders.Enabled)
    {
        // Before HTTPS redirection and the rate limiter, so both see the real scheme and client address.
        app.UseForwardedHeaders();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseStatusCodePagesWithReExecute("/not-found", "?codigo={0}", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    if (rateLimit.Enabled)
    {
        app.UseRateLimiter();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
    app.MapAuthenticationEndpoints();
    app.MapArtifactEndpoints();
    app.MapContextEndpoints();

    await app.Services.SeedInitialUsersAsync();

    // A blank Registration:InviteCode means off; say so at startup (never the value) so a blank variable is not mistaken for protection.
    app.Logger.LogInformation("Registration invite code: {State}", app.Services.GetRequiredService<IInviteCodePolicy>().Required ? "on" : "off");

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Application terminated unexpectedly");

    // A non-zero exit code lets IIS, Docker and CI see that startup failed.
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
