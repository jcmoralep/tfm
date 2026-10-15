using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RutaProducto.Infrastructure.Gemini;

// Wire shapes of generateContent, as settled by the live probe (docs/gemini-sonda.md). Members the client does not
// map (thoughtSignature, modelVersion, responseId, ...) are ignored on read; nulls are never written.

internal sealed record GenerateContentRequest(
    Content SystemInstruction,
    IReadOnlyList<Content> Contents,
    GenerationConfig GenerationConfig);

internal sealed record Content(string? Role, IReadOnlyList<Part> Parts);

internal sealed record Part(string? Text, bool? Thought = null);

internal sealed record GenerationConfig(
    string ResponseMimeType,
    JsonObject ResponseJsonSchema,
    ThinkingConfig ThinkingConfig);

internal sealed record ThinkingConfig(string ThinkingLevel);

internal sealed record GenerateContentResponse(
    Candidate[]? Candidates,
    PromptFeedback? PromptFeedback,
    UsageMetadata? UsageMetadata);

internal sealed record Candidate(Content? Content, string? FinishReason);

internal sealed record PromptFeedback(string? BlockReason);

internal sealed record UsageMetadata(
    int? PromptTokenCount,
    int? CandidatesTokenCount,
    int? ThoughtsTokenCount,
    int? TotalTokenCount);

internal static class GeminiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
