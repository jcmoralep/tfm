using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace BmadPlatform.Web.Components.Artifacts;

/// <summary>
/// Turns the stored Markdown into HTML for the read view. Raw HTML is disabled, so tags typed in a document stay
/// visible text, and link and image addresses are limited to http, https, mailto and relative ones.
/// </summary>
public sealed class MarkdownRenderer
{
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    public MarkupString Render(string markdown)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, pipeline);

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsAllowed(link.Url))
            {
                link.Url = string.Empty;
            }
        }

        // <scheme:address> autolinks are a separate node type and are not limited to http by the parser.
        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (!IsAllowed(autolink.Url))
            {
                autolink.Url = string.Empty;
            }
        }

        return new MarkupString(document.ToHtml(pipeline));
    }

    /// <summary>True for an empty address, a relative one, or one whose scheme is on the allow list.</summary>
    internal static bool IsAllowed(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        // Browsers ignore whitespace and control characters inside the scheme ("java\tscript:"), so drop them first.
        var cleaned = new string(url.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)).ToArray());
        var colon = cleaned.IndexOf(':');

        if (colon < 0)
        {
            return true;
        }

        var firstPathMark = cleaned.IndexOfAny(['/', '?', '#']);
        if (firstPathMark >= 0 && firstPathMark < colon)
        {
            return true;
        }

        return AllowedSchemes.Contains(cleaned[..colon], StringComparer.OrdinalIgnoreCase);
    }
}
