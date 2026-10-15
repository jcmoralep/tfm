using System.Diagnostics;
using System.Net;
using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Infrastructure.Gemini;
using static RutaProducto.Infrastructure.Tests.Gemini.GeminiTestKit;

namespace RutaProducto.Infrastructure.Tests.Gemini;

public sealed class GeminiClientResilienceTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task A_server_error_is_retried_once_and_the_second_answer_is_used(HttpStatusCode status)
    {
        var handler = StubHttpHandler.Replying(StubHttpHandler.Status(status), StubHttpHandler.Ok(ValidJson));

        var response = await Client(handler).GenerateAsync(Call(), CancellationToken.None);

        Assert.Equal("respuesta", (string?)response.Json["answer"]);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_second_server_error_is_final_and_ends_as_timeout()
    {
        var handler = StubHttpHandler.Replying(
            StubHttpHandler.Status(HttpStatusCode.ServiceUnavailable),
            StubHttpHandler.Status(HttpStatusCode.ServiceUnavailable));

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => Client(handler).GenerateAsync(Call(), CancellationToken.None));

        Assert.Equal(AssistantUnavailableReason.Timeout, error.Reason);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_connection_failure_is_a_timeout_that_keeps_the_inner_exception_and_is_not_retried()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("connection refused"));

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => Client(handler).GenerateAsync(Call("DISTINCTIVE-USER"), CancellationToken.None));

        Assert.Equal(AssistantUnavailableReason.Timeout, error.Reason);
        Assert.IsType<HttpRequestException>(error.InnerException);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("DISTINCTIVE-USER", ChainText(error));
        Assert.DoesNotContain(Key, ChainText(error));
    }

    [Fact]
    public void The_retry_delay_is_jittered_between_one_and_two_seconds()
    {
        var delays = Enumerable.Range(0, 30).Select(_ => GeminiClient.JitteredDelay()).ToList();

        Assert.All(delays, delay => Assert.InRange(delay, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        Assert.True(delays.Distinct().Count() > 1);
    }

    [Fact]
    public async Task The_retry_is_skipped_when_the_remaining_deadline_is_shorter_than_the_delay()
    {
        var handler = StubHttpHandler.Replying(StubHttpHandler.Status(HttpStatusCode.ServiceUnavailable), StubHttpHandler.Ok(ValidJson));
        var client = Client(handler, Options(o => o.TurnTimeoutSeconds = 1), retryDelay: () => TimeSpan.FromSeconds(5));

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(() => client.GenerateAsync(Call(), CancellationToken.None));

        Assert.Equal(AssistantUnavailableReason.Timeout, error.Reason);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_turn_that_never_answers_ends_as_timeout_after_its_deadline()
    {
        var handler = new StubHttpHandler((_, token) => GeminiTestKit.Never(token));
        var clock = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => Client(handler, Options(o => o.TurnTimeoutSeconds = 1)).GenerateAsync(Call(), CancellationToken.None));

        Assert.Equal(AssistantUnavailableReason.Timeout, error.Reason);
        Assert.InRange(clock.Elapsed, TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Drafts_use_their_own_longer_timeout()
    {
        var handler = new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);

            return StubHttpHandler.Ok(ValidJson);
        });
        var client = Client(handler, Options(o => { o.TurnTimeoutSeconds = 1; o.DraftTimeoutSeconds = 3; }));

        var draft = await client.GenerateAsync(Call(kind: GeminiCallKind.Draft), CancellationToken.None);
        var turn = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => client.GenerateAsync(Call(kind: GeminiCallKind.Turn), CancellationToken.None));

        Assert.NotNull(draft);
        Assert.Equal(AssistantUnavailableReason.Timeout, turn.Reason);
    }

    [Fact]
    public async Task The_deadline_covers_the_retry_and_no_second_attempt_starts_after_it()
    {
        var handler = new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1500), token);

            return StubHttpHandler.Status(HttpStatusCode.ServiceUnavailable);
        });
        var client = Client(handler, Options(o => o.TurnTimeoutSeconds = 2), retryDelay: () => TimeSpan.FromSeconds(1));

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(() => client.GenerateAsync(Call(), CancellationToken.None));

        Assert.Equal(AssistantUnavailableReason.Timeout, error.Reason);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_as_cancellation_and_is_not_retried()
    {
        var handler = new StubHttpHandler((_, token) => GeminiTestKit.Never(token));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GenerateAsync(Call(), source.Token));

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task At_most_the_configured_number_of_calls_are_in_flight_and_the_rest_wait(int limit)
    {
        var release = new TaskCompletionSource();
        var handler = new StubHttpHandler(async (_, _) =>
        {
            await release.Task;

            return StubHttpHandler.Ok(ValidJson);
        });
        var client = Client(handler, Options(o => o.MaxConcurrentCalls = limit));

        var calls = Enumerable.Range(0, 4).Select(_ => client.GenerateAsync(Call(), CancellationToken.None)).ToList();
        await Until(() => handler.Requests.Count == limit);
        await Task.Delay(200);

        Assert.Equal(limit, handler.Requests.Count);

        release.SetResult();
        await Task.WhenAll(calls);

        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(limit, handler.MaxInFlight);
    }

    [Fact]
    public async Task A_slot_is_released_when_a_call_fails()
    {
        var handler = StubHttpHandler.Replying(StubHttpHandler.Status(HttpStatusCode.TooManyRequests), StubHttpHandler.Ok(ValidJson));
        var client = Client(handler, Options(o => o.MaxConcurrentCalls = 1));

        await Assert.ThrowsAsync<AssistantUnavailableException>(() => client.GenerateAsync(Call(), CancellationToken.None));
        var second = await client.GenerateAsync(Call(), CancellationToken.None);

        Assert.NotNull(second);
    }

    [Fact]
    public async Task Waiting_for_a_slot_past_the_deadline_times_out_without_a_request_and_turns_and_drafts_share_the_guard()
    {
        var release = new TaskCompletionSource();
        var handler = new StubHttpHandler(async (_, _) =>
        {
            await release.Task;

            return StubHttpHandler.Ok(ValidJson);
        });
        var client = Client(handler, Options(o => { o.MaxConcurrentCalls = 1; o.TurnTimeoutSeconds = 1; }));

        var draft = client.GenerateAsync(Call(kind: GeminiCallKind.Draft), CancellationToken.None);
        await Until(() => handler.Requests.Count == 1);

        var turn = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => client.GenerateAsync(Call(kind: GeminiCallKind.Turn), CancellationToken.None));

        Assert.Equal(AssistantUnavailableReason.Timeout, turn.Reason);
        Assert.Single(handler.Requests);

        release.SetResult();
        await draft;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }
}
