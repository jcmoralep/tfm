using RutaProducto.Application.Abstractions.Captcha;
using Microsoft.Extensions.DependencyInjection;

namespace RutaProducto.Infrastructure.Captcha;

public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the verifier selected by <c>Captcha:Provider</c> and the widget settings. With <c>None</c> nothing
    /// touches the network and the widget renders nothing. The factory loggers are removed so request details are not logged.
    /// </summary>
    public static IServiceCollection AddCaptcha(this IServiceCollection services, CaptchaOptions options)
    {
        if (options.Provider == CaptchaProvider.Turnstile)
        {
            services.AddSingleton(options);
            services
                .AddHttpClient(TurnstileCaptchaVerifier.HttpClientName, client =>
                {
                    client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
                    client.Timeout = TimeSpan.FromSeconds(10);
                })
                .RemoveAllLoggers();
            services.AddSingleton<ICaptchaVerifier, TurnstileCaptchaVerifier>();
            services.AddSingleton(new CaptchaWidgetSettings(options.SiteKey));
        }
        else
        {
            services.AddSingleton<ICaptchaVerifier, NoneCaptchaVerifier>();
            services.AddSingleton(new CaptchaWidgetSettings(null));
        }

        return services;
    }
}
