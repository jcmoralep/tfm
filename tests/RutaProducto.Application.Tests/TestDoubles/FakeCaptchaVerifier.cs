using RutaProducto.Application.Abstractions.Captcha;

namespace RutaProducto.Application.Tests.TestDoubles;

/// <summary>Returns a configured outcome and counts how often it was asked.</summary>
public sealed class FakeCaptchaVerifier(CaptchaOutcome outcome = CaptchaOutcome.Passed) : ICaptchaVerifier
{
    public int Calls { get; private set; }

    public string? LastToken { get; private set; }

    public Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        Calls++;
        LastToken = token;

        return Task.FromResult(outcome);
    }
}
