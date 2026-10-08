using BmadPlatform.Application;
using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Infrastructure;
using BmadPlatform.Infrastructure.Identity.Seeding;
using BmadPlatform.Web.Authentication;
using BmadPlatform.Web.Components;
using BmadPlatform.Web.Logging;
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
        .AddInteractiveServerComponents();

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
        options.Cookie.Name = "BmadPlatform.Auth";
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
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
    app.MapAuthenticationEndpoints();

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
