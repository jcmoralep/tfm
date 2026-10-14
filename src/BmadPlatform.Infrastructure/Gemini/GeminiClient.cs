using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BmadPlatform.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadPlatform.Infrastructure.Gemini;

/// <summary>
/// Calls Gemini <c>generateContent</c> for the assistant and the drafter. Every failure is an
/// <see cref="AssistantUnavailableException"/> with a reason. One deadline covers the whole call (concurrency wait,
/// attempt, retry delay, retry); at most one retry, only after HTTP 500, 503 or 504. Nothing about the content, the
/// response body or the key is ever logged or placed in an exception.
/// </summary>
public sealed class GeminiClient
{
    private const string ApiKeyHeader = "x-goog-api-key";
    private static readonly HashSet<string> SafetyStops = ["SAFETY", "PROHIBITED_CONTENT", "BLOCKLIST", "SPII", "RECITATION"];

    private readonly IHttpClientFactory httpClientFactory;
    private readonly GeminiOptions options;
    private readonly ILogger<GeminiClient> logger;
    private readonly Func<TimeSpan> retryDelay;

    // One guard for the whole process, shared by turns and drafts.
    private readonly SemaphoreSlim gate;

    public GeminiClient(IHttpClientFactory httpClientFactory, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
        : this(httpClientFactory, options, logger, JitteredDelay)
    {
    }

    internal GeminiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiOptions> options,
        ILogger<GeminiClient> logger,
        Func<TimeSpan> retryDelay)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.logger = logger;
        this.retryDelay = retryDelay;
        gate = new SemaphoreSlim(this.options.MaxConcurrentCalls, this.options.MaxConcurrentCalls);
    }

    /// <summary>Between 1 and 2 seconds.</summary>
    internal static TimeSpan JitteredDelay() => TimeSpan.FromSeconds(1 + Random.Shared.NextDouble());

    public async Task<GeminiResponse> GenerateAsync(GeminiCall call, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(
            call.Kind == GeminiCallKind.Draft ? options.DraftTimeoutSeconds : options.TurnTimeoutSeconds);
        var clock = Stopwatch.StartNew();
        var trace = new CallTrace();

        // The caller's token and the deadline: when only the deadline fires, the reason is Timeout.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        var holdsSlot = false;

        try
        {
            await gate.WaitAsync(deadline.Token);
            holdsSlot = true;

            var response = await SendWithOneRetryAsync(call, timeout, clock, trace, deadline.Token);
            LogSuccess(call, trace, clock);

            return response;
        }
        catch (AssistantUnavailableException failure)
        {
            LogFailure(call, trace, clock, failure.Reason);
            throw;
        }
        catch (OperationCanceledException cancelled) when (!cancellationToken.IsCancellationRequested)
        {
            var failure = Fail(AssistantUnavailableReason.Timeout, cancelled);
            LogFailure(call, trace, clock, failure.Reason);
            throw failure;
        }
        finally
        {
            if (holdsSlot)
            {
                gate.Release();
            }
        }
    }

    private async Task<GeminiResponse> SendWithOneRetryAsync(
        GeminiCall call,
        TimeSpan timeout,
        Stopwatch clock,
        CallTrace trace,
        CancellationToken token)
    {
        var client = httpClientFactory.CreateClient(GeminiServiceCollectionExtensions.HttpClientName);

        for (var attempt = 1; ; attempt++)
        {
            using var request = BuildRequest(call);
            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            }
            catch (HttpRequestException failure)
            {
                throw Fail(AssistantUnavailableReason.Timeout, failure);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                trace.Status = status;

                if (response.IsSuccessStatusCode)
                {
                    return await ReadAsync(response, call, trace, token);
                }

                // The error body is never read.
                var retryable = status is 500 or 503 or 504;

                if (!retryable || attempt > 1)
                {
                    throw Fail(ReasonFor(status));
                }
            }

            var delay = retryDelay();

            if (timeout - clock.Elapsed <= delay)
            {
                throw Fail(AssistantUnavailableReason.Timeout);
            }

            await Task.Delay(delay, token);
        }
    }

    private HttpRequestMessage BuildRequest(GeminiCall call)
    {
        var body = new GenerateContentRequest(
            new Content(null, [new Part(call.SystemInstruction)]),
            [new Content("user", [new Part(call.UserContent)])],
            new GenerationConfig(
                "application/json",
                call.ResponseSchema,
                new ThinkingConfig(options.ThinkingLevel)));

        // Absolute URI so the version segment of BaseUrl (v1beta) is kept.
        var uri = new Uri($"{options.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(options.Model)}:generateContent");
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, GeminiJson.Options),
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.Add(ApiKeyHeader, options.ApiKey);

        return request;
    }

    private static async Task<GeminiResponse> ReadAsync(
        HttpResponseMessage response,
        GeminiCall call,
        CallTrace trace,
        CancellationToken token)
    {
        GenerateContentResponse? parsed;

        try
        {
            var raw = await response.Content.ReadAsStringAsync(token);
            parsed = JsonSerializer.Deserialize<GenerateContentResponse>(raw, GeminiJson.Options);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException)
        {
            throw Fail(AssistantUnavailableReason.Timeout, failure);
        }
        catch (JsonException)
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        if (parsed is null)
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        var usage = new GeminiUsage(
            parsed.UsageMetadata?.PromptTokenCount,
            parsed.UsageMetadata?.CandidatesTokenCount,
            parsed.UsageMetadata?.ThoughtsTokenCount,
            parsed.UsageMetadata?.TotalTokenCount);
        trace.Usage = usage;

        // A 2xx is judged from its body, never from the status alone.
        if (!string.IsNullOrEmpty(parsed.PromptFeedback?.BlockReason))
        {
            throw Fail(AssistantUnavailableReason.Blocked);
        }

        var candidate = parsed.Candidates?.FirstOrDefault();

        if (candidate is null)
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        var finishReason = candidate.FinishReason ?? string.Empty;
        trace.FinishReason = finishReason;

        if (SafetyStops.Contains(finishReason))
        {
            throw Fail(AssistantUnavailableReason.Blocked);
        }

        if (finishReason != "STOP")
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        var text = string.Concat(
            (candidate.Content?.Parts ?? []).Where(part => part.Thought != true).Select(part => part.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        JsonNode? json;

        try
        {
            json = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        if (!GeminiSchema.Satisfies(json, call.ResponseSchema))
        {
            throw Fail(AssistantUnavailableReason.Malformed);
        }

        return new GeminiResponse(json!, finishReason, usage);
    }

    private static AssistantUnavailableReason ReasonFor(int status) => status switch
    {
        429 => AssistantUnavailableReason.Quota,
        401 or 403 => AssistantUnavailableReason.Auth,
        408 => AssistantUnavailableReason.Timeout,
        >= 500 => AssistantUnavailableReason.Timeout,
        _ => AssistantUnavailableReason.Model,
    };

    private static AssistantUnavailableException Fail(AssistantUnavailableReason reason, Exception? inner = null) =>
        new(GeminiTexts.For(reason), reason, inner);

    // Only these fields are ever logged: ids and tags, model, status, duration, finishReason and token counts.
    private void LogSuccess(GeminiCall call, CallTrace trace, Stopwatch clock) =>
        logger.LogInformation(
            "Gemini call {Purpose} for initiative {InitiativeId}: model {Model}, status {Status}, {DurationMs} ms, finishReason {FinishReason}, tokens prompt {PromptTokens} candidates {CandidateTokens} thoughts {ThoughtTokens}.",
            call.Purpose,
            call.InitiativeId,
            options.Model,
            trace.Status,
            clock.ElapsedMilliseconds,
            trace.FinishReason,
            trace.Usage?.Prompt,
            trace.Usage?.Candidates,
            trace.Usage?.Thoughts);

    private void LogFailure(GeminiCall call, CallTrace trace, Stopwatch clock, AssistantUnavailableReason reason) =>
        logger.LogWarning(
            "Gemini call {Purpose} for initiative {InitiativeId} failed with reason {Reason}: model {Model}, status {Status}, {DurationMs} ms, finishReason {FinishReason}.",
            call.Purpose,
            call.InitiativeId,
            reason,
            options.Model,
            trace.Status,
            clock.ElapsedMilliseconds,
            trace.FinishReason);

    private sealed class CallTrace
    {
        public int? Status { get; set; }

        public string? FinishReason { get; set; }

        public GeminiUsage? Usage { get; set; }
    }
}
