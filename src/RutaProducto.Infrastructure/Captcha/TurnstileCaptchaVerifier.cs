using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using RutaProducto.Application.Abstractions.Captcha;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Captcha;

/// <summary>
/// Checks the response token with Cloudflare Turnstile <c>siteverify</c>. Fails closed: anything that is not a clear
/// "success" blocks the form. Logs carry the outcome, the duration, the HTTP status and Cloudflare's short error codes,
/// never the token, the secret or a response body.
/// </summary>
public sealed partial class TurnstileCaptchaVerifier(
    IHttpClientFactory httpClientFactory,
    CaptchaOptions options,
    ILogger<TurnstileCaptchaVerifier> logger) : ICaptchaVerifier
{
    public const string HttpClientName = "Turnstile";

    public const string VerifyPath = "turnstile/v0/siteverify";

    public const int MaxTokenLength = 2048;

    private const int MaxLoggedCodes = 5;

    public async Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            logger.LogInformation("Captcha {Outcome}: no usable response token", CaptchaOutcome.Rejected);

            return CaptchaOutcome.Rejected;
        }

        var clock = Stopwatch.StartNew();
        int? status = null;

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey ?? string.Empty,
                ["response"] = token,
            });

            using var response = await client.PostAsync(VerifyPath, form, cancellationToken);
            status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return Finish(CaptchaOutcome.Unavailable, clock, status, []);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("success", out var success)
                || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return Finish(CaptchaOutcome.Unavailable, clock, status, []);
            }

            return Finish(
                success.GetBoolean() ? CaptchaOutcome.Passed : CaptchaOutcome.Rejected,
                clock,
                status,
                ReadErrorCodes(document.RootElement));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client timeout: not the caller giving up.
            return Finish(CaptchaOutcome.Unavailable, clock, status, ["timeout"]);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            return Finish(CaptchaOutcome.Unavailable, clock, status, [exception.GetType().Name]);
        }
    }

    private CaptchaOutcome Finish(CaptchaOutcome outcome, Stopwatch clock, int? status, IReadOnlyList<string> codes)
    {
        logger.LogInformation(
            "Captcha {Outcome} in {ElapsedMs} ms (HTTP {Status}, codes {Codes})",
            outcome,
            clock.ElapsedMilliseconds,
            status,
            string.Join(',', codes));

        return outcome;
    }

    /// <summary>Only short, closed-looking codes such as <c>invalid-input-response</c> are ever logged.</summary>
    private static List<string> ReadErrorCodes(JsonElement root)
    {
        var codes = new List<string>();

        if (root.TryGetProperty("error-codes", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var code = item.ValueKind == JsonValueKind.String ? item.GetString() : null;

                if (code is not null && ErrorCode().IsMatch(code) && codes.Count < MaxLoggedCodes)
                {
                    codes.Add(code);
                }
            }
        }

        return codes;
    }

    [GeneratedRegex("^[a-z0-9-]{1,48}$")]
    private static partial Regex ErrorCode();
}
