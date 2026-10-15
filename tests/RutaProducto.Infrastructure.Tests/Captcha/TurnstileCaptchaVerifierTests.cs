using System.Net;
using RutaProducto.Application.Abstractions.Captcha;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Infrastructure.Captcha;
using RutaProducto.Infrastructure.Tests.Gemini;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Tests.Captcha;

public sealed class TurnstileCaptchaVerifierTests
{
    private const string Secret = "S3CRET";
    private const string Token = "TOK";

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"success":true}""", CaptchaOutcome.Passed)]
    [InlineData(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}""", CaptchaOutcome.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, "{}", CaptchaOutcome.Unavailable)]
    [InlineData(HttpStatusCode.OK, "garbage", CaptchaOutcome.Unavailable)]
    [InlineData(HttpStatusCode.OK, """{"ok":1}""", CaptchaOutcome.Unavailable)]
    public async Task Siteverify_answers_map_to_an_outcome(HttpStatusCode status, string body, CaptchaOutcome expected)
    {
        var handler = StubHttpHandler.Replying(StubHttpHandler.Status(status, body));

        Assert.Equal(expected, await Verifier(handler).VerifyAsync(Token, CancellationToken.None));
    }

    [Fact]
    public async Task A_timeout_is_unavailable()
    {
        var handler = new StubHttpHandler((_, _) => throw new TaskCanceledException("timed out"));

        Assert.Equal(CaptchaOutcome.Unavailable, await Verifier(handler).VerifyAsync(Token, CancellationToken.None));
    }

    [Fact]
    public async Task A_network_error_is_unavailable()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("down"));

        Assert.Equal(CaptchaOutcome.Unavailable, await Verifier(handler).VerifyAsync(Token, CancellationToken.None));
    }

    [Fact]
    public async Task A_caller_cancellation_is_not_swallowed()
    {
        using var source = new CancellationTokenSource();
        var handler = new StubHttpHandler(async (_, token) =>
        {
            await source.CancelAsync();
            token.ThrowIfCancellationRequested();

            return StubHttpHandler.Status(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Verifier(handler).VerifyAsync(Token, source.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-long")]
    public async Task A_missing_blank_or_oversized_token_is_rejected_without_a_call(string? token)
    {
        if (token == "too-long")
        {
            token = new string('a', TurnstileCaptchaVerifier.MaxTokenLength + 1);
        }

        var handler = StubHttpHandler.Replying(StubHttpHandler.Status(HttpStatusCode.OK, """{"success":true}"""));

        Assert.Equal(CaptchaOutcome.Rejected, await Verifier(handler).VerifyAsync(token, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_request_carries_only_the_secret_and_the_token_and_nothing_leaks_to_the_log()
    {
        var logs = new CapturingLoggerProvider();
        var handler = StubHttpHandler.Replying(StubHttpHandler.Status(
            HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"],"detail":"S3CRET TOK"}"""));

        await Verifier(handler, logs).VerifyAsync(Token, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith(TurnstileCaptchaVerifier.VerifyPath, request.Uri.ToString());
        Assert.Equal("secret=S3CRET&response=TOK", request.Body);

        var text = string.Join(" ", logs.Entries.Select(entry => entry.AllText));
        Assert.Contains("Rejected", text);
        Assert.Contains("invalid-input-response", text);
        Assert.DoesNotContain(Secret, text);
        Assert.DoesNotContain(Token, text);
        Assert.DoesNotContain("detail", text);
    }

    private static TurnstileCaptchaVerifier Verifier(StubHttpHandler handler, CapturingLoggerProvider? logs = null)
    {
        var options = CaptchaOptions.BindAndValidate(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Captcha:Provider"] = "Turnstile",
                ["Turnstile:SiteKey"] = "SITE",
                ["Turnstile:SecretKey"] = Secret,
            })
            .Build());
        var factory = LoggerFactory.Create(builder =>
        {
            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });

        return new TurnstileCaptchaVerifier(new BaseAddressFactory(handler), options, factory.CreateLogger<TurnstileCaptchaVerifier>());
    }

    /// <summary>Like the real named client: the base address is set, so the relative verify path resolves.</summary>
    private sealed class BaseAddressFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://challenges.example.test/") };
    }
}
