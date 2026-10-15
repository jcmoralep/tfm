using System.Text;
using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Context;

namespace BmadPlatform.Infrastructure.Gemini;

/// <summary>
/// The optional <c>&lt;context&gt;</c> block shared by the turn and the draft prompts. It holds the text-like items
/// the person attached, as data. Titles and file names are neutralized single lines; the text keeps its lines but every
/// angle bracket is neutralized, so nothing inside can close the block or fake a delimiter. No id and no URL is ever
/// written. With no items there is no block, so a request without context stays byte-identical.
/// </summary>
internal static class ContextPrompt
{
    /// <summary>The block, starting on its own line, or an empty string when there is nothing to send.</summary>
    public static string Block(IReadOnlyList<ContextExcerpt>? excerpts)
    {
        if (excerpts is not { Count: > 0 })
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\n<context>\n");

        foreach (var excerpt in excerpts)
        {
            builder
                .Append("--- ").Append(excerpt.Kind == AttachmentKind.Note ? "note" : "file").Append(": ")
                .Append(Line(excerpt.Title)).Append('\n')
                .Append(PromptText.Neutralize(excerpt.Text)).Append('\n');
        }

        return builder.Append("</context>").ToString();
    }

    public static bool Present(IReadOnlyList<ContextExcerpt>? excerpts) => excerpts is { Count: > 0 };

    // One line, with no angle brackets.
    private static string Line(string? text) =>
        string.Join(' ', PromptText.Neutralize(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
