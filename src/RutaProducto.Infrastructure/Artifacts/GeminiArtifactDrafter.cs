using System.Text.Json.Nodes;
using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Infrastructure.Gemini;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// Drafts a document with Gemini. The Architecture and the Epics have their own prompt and reply shape (<see cref="ArchitecturePrompt"/>, <see cref="EpicsPrompt"/>). Otherwise the model returns items per slot, each with a source (answer, assumption or
/// unknown); the composer and the layouts write the Markdown, so the outline, numbering, markers and caps are the
/// same as the fake's. Any failure is an <see cref="AssistantUnavailableException"/> with a document-specific message
/// and nothing is stored; there is no fallback to the fake for a model-backed document. Only the document type and
/// the reason are logged, never the answers, the Brief or the draft.
/// </summary>
public sealed class GeminiArtifactDrafter(GeminiClient client, ILogger<GeminiArtifactDrafter> logger) : IArtifactDrafter
{
    public async Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken)
    {
        // The Architecture and the Epics have their own prompt and reply shape; the other types are flat slots.
        var type = request.Type;
        IReadOnlyList<DraftSlot> slots = type is ArtifactType.Architecture or ArtifactType.EpicsAndStories ? [] : DraftSlots.For(type, request.Depth);
        GeminiResponse response;

        try
        {
            var call = type switch
            {
                ArtifactType.Architecture => ArchitecturePrompt.Build(request),
                ArtifactType.EpicsAndStories => EpicsPrompt.Build(request),
                _ => DraftPrompt.Build(request, slots),
            };

            response = await client.GenerateAsync(call, cancellationToken);
        }
        catch (AssistantUnavailableException failure)
        {
            throw new AssistantUnavailableException(ArtifactTexts.DraftUnavailableFor(failure.Reason), failure.Reason, failure);
        }

        DraftItems items;

        var parsed = type switch
        {
            ArtifactType.Architecture => ArchitecturePrompt.TryParse(response.Json, out items!),
            ArtifactType.EpicsAndStories => EpicsPrompt.TryParse(response.Json, request, out items!),
            _ => TryParse(response.Json, slots, out items!),
        };

        if (!parsed)
        {
            logger.LogWarning("The draft of a {Type} was rejected ({Reason}).", request.Type, AssistantUnavailableReason.Malformed);

            throw new AssistantUnavailableException(ArtifactTexts.DraftMalformed, AssistantUnavailableReason.Malformed);
        }

        // An assumption may not bring a figure nobody gave: it becomes an open question about its section (counts only in the log).
        if (type is not (ArtifactType.Architecture or ArtifactType.EpicsAndStories))
        {
            items = FigureGuard.Filter(items, slots, FigureInputs(request), out var converted);

            if (converted > 0)
            {
                logger.LogInformation("{Count} assumption(s) with figures not given by the person became open questions in the draft of a {Type}.", converted, request.Type);
            }
        }

        // The PRD builds on the approved Brief: its assumptions stay assumptions and its open questions stay open.
        if (request.Type == ArtifactType.Prd && !string.IsNullOrWhiteSpace(request.BriefMarkdown))
        {
            items = BriefFacts.Apply(items, request.BriefMarkdown);
        }

        return DraftRenderer.Render(request, items);
    }

    // Every text the person wrote or approved, the only place a figure of the draft may come from.
    private static IEnumerable<string?> FigureInputs(DraftRequest request) =>
        new[] { request.InitiativeName, request.Description }
            .Concat(request.Answers.Where(a => !a.IsUnknown && !a.IsNone).Select(a => a.Text))
            .Concat(request.Type == ArtifactType.Prd ? [request.BriefMarkdown] : [])
            .Concat((request.Context ?? []).SelectMany(c => new[] { c.Title, c.Text }));

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

    internal static bool TryText(JsonNode? node, out string text)
    {
        text = string.Empty;

        return node is JsonValue value && value.TryGetValue(out text!);
    }

    internal static bool TrySource(string name, out DraftItemSource source)
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
