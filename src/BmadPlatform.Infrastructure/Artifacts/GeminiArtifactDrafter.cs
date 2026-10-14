using System.Text.Json.Nodes;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Infrastructure.Gemini;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// Drafts a document with Gemini. The model returns items per slot, each with a source (answer, assumption or
/// unknown); the composer and the layouts write the Markdown, so the outline, numbering, markers and caps are the
/// same as the fake's. Any failure is an <see cref="AssistantUnavailableException"/> with a document-specific message
/// and nothing is stored; there is no fallback to the fake for a model-backed document. Only the document type and
/// the reason are logged, never the answers, the Brief or the draft.
/// </summary>
public sealed class GeminiArtifactDrafter(GeminiClient client, ILogger<GeminiArtifactDrafter> logger) : IArtifactDrafter
{
    public async Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken)
    {
        var slots = DraftSlots.For(request.Type, request.Depth);
        GeminiResponse response;

        try
        {
            response = await client.GenerateAsync(DraftPrompt.Build(request, slots), cancellationToken);
        }
        catch (AssistantUnavailableException failure)
        {
            throw new AssistantUnavailableException(ArtifactTexts.DraftUnavailableFor(failure.Reason), failure.Reason, failure);
        }

        if (!TryParse(response.Json, slots, out var items))
        {
            logger.LogWarning("The draft of a {Type} was rejected ({Reason}).", request.Type, AssistantUnavailableReason.Malformed);

            throw new AssistantUnavailableException(ArtifactTexts.DraftMalformed, AssistantUnavailableReason.Malformed);
        }

        // The PRD builds on the approved Brief: its assumptions stay assumptions and its open questions stay open.
        if (request.Type == ArtifactType.Prd && !string.IsNullOrWhiteSpace(request.BriefMarkdown))
        {
            items = BriefFacts.Apply(items, request.BriefMarkdown);
        }

        return DraftRenderer.Render(request, items);
    }

    // A section the document does not have, or a source outside the three, makes the whole draft unusable.
    private static bool TryParse(JsonNode json, IReadOnlyList<DraftSlot> slots, out DraftItems items)
    {
        items = new DraftItems(new Dictionary<string, IReadOnlyList<DraftItem>>());

        if (json is not JsonObject root)
        {
            return false;
        }

        var known = slots.ToDictionary(slot => slot.Id, StringComparer.Ordinal);
        var bySlot = new Dictionary<string, IReadOnlyList<DraftItem>>(StringComparer.Ordinal);

        foreach (var (id, node) in root)
        {
            if (!known.TryGetValue(id, out var slot) || node is not JsonArray entries)
            {
                return false;
            }

            var list = new List<DraftItem>();

            foreach (var entry in entries)
            {
                if (entry is not JsonObject item
                    || !TryText(item["text"], out var text)
                    || !TryText(item["source"], out var name)
                    || !TrySource(name, out var source))
                {
                    return false;
                }

                // Content the person was never asked for cannot be presented as their own answer.
                list.Add(new DraftItem(text, source == DraftItemSource.Answer && !slot.AllowsAnswer ? DraftItemSource.Assumption : source));
            }

            bySlot[id] = list;
        }

        items = new DraftItems(bySlot);

        return true;
    }

    private static bool TryText(JsonNode? node, out string text)
    {
        text = string.Empty;

        return node is JsonValue value && value.TryGetValue(out text!);
    }

    private static bool TrySource(string name, out DraftItemSource source)
    {
        switch (name)
        {
            case "answer":
                source = DraftItemSource.Answer;
                return true;
            case "assumption":
                source = DraftItemSource.Assumption;
                return true;
            case "unknown":
                source = DraftItemSource.Unknown;
                return true;
            default:
                source = default;
                return false;
        }
    }
}
