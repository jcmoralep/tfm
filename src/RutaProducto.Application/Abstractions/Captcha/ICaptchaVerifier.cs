namespace RutaProducto.Application.Abstractions.Captcha;

/// <summary>Result of checking one captcha response token. Anything but <see cref="Passed"/> blocks the form.</summary>
public enum CaptchaOutcome
{
    Passed,
    Rejected,
    Unavailable,
}

/// <summary>Verifies the captcha response a form carries. Implementations fail closed and never trust the client.</summary>
public interface ICaptchaVerifier
{
    Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken cancellationToken);
}

/// <summary>What the widget needs in the browser. A null site key means no captcha: the widget renders nothing.</summary>
public sealed record CaptchaWidgetSettings(string? SiteKey);
