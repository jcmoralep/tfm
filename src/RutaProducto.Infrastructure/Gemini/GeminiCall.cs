using System.Text.Json.Nodes;

namespace RutaProducto.Infrastructure.Gemini;

/// <summary>Selects the deadline: turns are short, document drafts are long.</summary>
public enum GeminiCallKind
{
    Turn,
    Draft,
}

/// <summary>
/// One logical request. The caller owns the wording: instructions, the delimited user content and the schema.
/// <see cref="Purpose"/> and <see cref="InitiativeId"/> are only log tags; they are never sent to the model.
/// </summary>
public sealed record GeminiCall(
    string Purpose,
    GeminiCallKind Kind,
    string SystemInstruction,
    string UserContent,
    JsonObject ResponseSchema,
    Guid? InitiativeId = null);

/// <summary>Token counts reported by the API; any of them may be absent (thoughts are omitted when zero).</summary>
public sealed record GeminiUsage(int? Prompt, int? Candidates, int? Thoughts, int? Total);

/// <summary>A successful call: the parsed JSON (already checked against the schema) and its usage.</summary>
public sealed record GeminiResponse(System.Text.Json.Nodes.JsonNode Json, string FinishReason, GeminiUsage Usage);
