using System.Net;
using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using static RutaProducto.Infrastructure.Tests.Gemini.GeminiTestKit;

namespace RutaProducto.Infrastructure.Tests.Gemini;

public sealed class GeminiClientLoggingTests
{
    private static readonly string[] Secrets = [Key, "DISTINCTIVE-USER", "DISTINCTIVE-REPLY", "SECRET-BODY"];

    [Fact]
    public async Task A_success_log_holds_only_the_allowed_fields()
    {
        var logs = new CapturingLoggerProvider();
        var initiativeId = Guid.NewGuid();
        var handler = StubHttpHandler.Replying(StubHttpHandler.Ok("""{"answer":"DISTINCTIVE-REPLY"}"""));

        await Client(handler, logs: logs).GenerateAsync(Call("DISTINCTIVE-USER", initiativeId: initiativeId), CancellationToken.None);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("turn:idea", entry.AllText);
        Assert.Contains(initiativeId.ToString(), entry.AllText);
        Assert.Contains("m1", entry.AllText);
        Assert.Contains("Status=200", entry.AllText);
        Assert.Contains("DurationMs=", entry.AllText);
        Assert.Contains("FinishReason=STOP", entry.AllText);
        Assert.Contains("PromptTokens=125", entry.AllText);
        Assert.Contains("CandidateTokens=17", entry.AllText);
        AssertNoSecrets(logs);
    }

    [Fact]
    public async Task Failures_log_a_warning_with_the_reason_and_neither_content_nor_key_in_any_entry()
    {
        var logs = new CapturingLoggerProvider();

        await Assert.ThrowsAsync<AssistantUnavailableException>(() => Client(
            StubHttpHandler.Replying(StubHttpHandler.Status(HttpStatusCode.TooManyRequests, "SECRET-BODY")), logs: logs)
            .GenerateAsync(Call("DISTINCTIVE-USER"), CancellationToken.None));
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => Client(
            StubHttpHandler.Replying(StubHttpHandler.Status(HttpStatusCode.Unauthorized, "SECRET-BODY")), logs: logs)
            .GenerateAsync(Call("DISTINCTIVE-USER"), CancellationToken.None));
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => Client(
            new StubHttpHandler((_, token) => Never(token)),
            Options(o => o.TurnTimeoutSeconds = 1),
            logs)
            .GenerateAsync(Call("DISTINCTIVE-USER"), CancellationToken.None));

        Assert.Equal(3, logs.Entries.Count);
        Assert.All(logs.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.Contains("Reason=Quota", logs.Entries[0].AllText);
        Assert.Contains("Reason=Auth", logs.Entries[1].AllText);
        Assert.Contains("Reason=Timeout", logs.Entries[2].AllText);
        AssertNoSecrets(logs);
    }

    private static void AssertNoSecrets(CapturingLoggerProvider logs)
    {
        foreach (var entry in logs.Entries)
        {
            foreach (var secret in Secrets)
            {
                Assert.DoesNotContain(secret, entry.AllText);
            }
        }
    }
}
