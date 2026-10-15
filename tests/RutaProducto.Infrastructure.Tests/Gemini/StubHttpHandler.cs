using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Infrastructure.Gemini;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Tests.Gemini;

/// <summary>A request as the stub saw it. The body is read inside the handler, before anything is disposed.</summary>
internal sealed record SeenRequest(HttpMethod Method, Uri Uri, string? ApiKey, string Body);

/// <summary>Scripts the HTTP layer of the Gemini client: no network, no key.</summary>
internal sealed class StubHttpHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly List<SeenRequest> requests = [];
    private int inFlight;
    private int maxInFlight;

    public IReadOnlyList<SeenRequest> Requests
    {
        get
        {
            lock (requests)
            {
                return requests.ToArray();
            }
        }
    }

    public int MaxInFlight => Volatile.Read(ref maxInFlight);

    public static StubHttpHandler Replying(params HttpResponseMessage[] responses) =>
        new((attempt, _) => Task.FromResult(responses[Math.Min(attempt, responses.Length) - 1]));

    public static HttpResponseMessage Status(HttpStatusCode status, string body = "{}") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Ok(string text, string finishReason = "STOP") =>
        Status(HttpStatusCode.OK, Candidate(text, finishReason));

    public static string Candidate(string text, string finishReason) =>
        new JsonObject
        {
            ["candidates"] = new JsonArray(new JsonObject
            {
                ["content"] = new JsonObject
                {
                    ["role"] = "model",
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = text, ["thoughtSignature"] = "sig" }),
                },
                ["finishReason"] = finishReason,
            }),
            ["usageMetadata"] = new JsonObject
            {
                ["promptTokenCount"] = 125,
                ["candidatesTokenCount"] = 17,
                ["totalTokenCount"] = 142,
            },
        }.ToJsonString();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        int attempt;

        lock (requests)
        {
            requests.Add(new SeenRequest(request.Method, request.RequestUri!, request.Headers.TryGetValues("x-goog-api-key", out var key) ? key.Single() : null, body));
            attempt = requests.Count;
        }

        var current = Interlocked.Increment(ref inFlight);
        int seen;

        while (current > (seen = Volatile.Read(ref maxInFlight)) && Interlocked.CompareExchange(ref maxInFlight, current, seen) != seen)
        {
        }

        try
        {
            return await respond(attempt, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref inFlight);
        }
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
}

internal static class GeminiTestKit
{
    public const string Key = "K-12345";

    public static readonly JsonObject Schema = JsonNode.Parse(
        """{"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"]}""")!.AsObject();

    /// <summary>The message and the whole inner-exception chain, as one string to search.</summary>
    public static string ChainText(Exception error)
    {
        var parts = new List<string>();

        for (var current = error; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts) + " | " + error;
    }

    /// <summary>A server that never answers; it ends only when the call is cancelled.</summary>
    public static async Task<HttpResponseMessage> Never(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token);

        return StubHttpHandler.Ok(ValidJson);
    }

    public const string ValidJson ="""{"answer":"respuesta"}""";

    public static GeminiCall Call(
        string userContent = "<transcript>hola</transcript>",
        GeminiCallKind kind = GeminiCallKind.Turn,
        Guid? initiativeId = null) =>
        new("turn:idea", kind, "Instrucciones del sistema", userContent, Schema, initiativeId);

    public static GeminiOptions Options(Action<GeminiOptions>? change = null)
    {
        var options = new GeminiOptions { ApiKey = Key, BaseUrl = "https://example.test/v1beta", Model = "m1" };
        change?.Invoke(options);

        return options;
    }

    public static GeminiClient Client(
        StubHttpHandler handler,
        GeminiOptions? options = null,
        ILoggerProvider? logs = null,
        Func<TimeSpan>? retryDelay = null)
    {
        var factory = LoggerFactory.Create(builder =>
        {
            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });

        return new GeminiClient(
            new StubHttpClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(options ?? Options()),
            factory.CreateLogger<GeminiClient>(),
            retryDelay ?? (() => TimeSpan.Zero));
    }
}
