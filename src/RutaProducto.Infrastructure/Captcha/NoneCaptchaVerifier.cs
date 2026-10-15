using RutaProducto.Application.Abstractions.Captcha;

namespace RutaProducto.Infrastructure.Captcha;

/// <summary>The default: no captcha. Always passes, never touches the network.</summary>
public sealed class NoneCaptchaVerifier : ICaptchaVerifier
{
    public Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken cancellationToken) =>
        Task.FromResult(CaptchaOutcome.Passed);
}
