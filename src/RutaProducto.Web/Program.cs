using RutaProducto.Application;
using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Infrastructure;
using RutaProducto.Infrastructure.Identity.Seeding;
using RutaProducto.Web.Artifacts;
using RutaProducto.Web.Authentication;
using RutaProducto.Web.Components;
using RutaProducto.Web.Components.Artifacts;
using RutaProducto.Web.Context;
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

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        // File sink, rolling and retention come from appsettings.json (Serilog section).
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services);

        if (context.HostingEnvironment.IsDevelopment())
        {
            configuration.WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
        }
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

    // Off by default: with the switch off neither AddRateLimiter nor UseRateLimiter is called.
    var rateLimit = RateLimitOptions.BindAndValidate(builder.Configuration);

    if (rateLimit.Enabled)
    {
        builder.Services.AddRateLimiter(limiter => AuthRateLimiting.Configure(limiter, rateLimit));
    }

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

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
