using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace BmadPlatform.Web.Components.Artifacts;

/// <summary>
/// Turns the stored Markdown into HTML for the read view. Raw HTML is disabled, so tags typed in a document stay
/// visible text, and link addresses are limited to http, https, mailto and relative ones. Images are never loaded:
/// they show their alt text, so a viewer's browser cannot be made to fetch an external address.
/// </summary>
public sealed class MarkdownRenderer
{
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    /// <param name="skipTitle">Leaves out the leading level-1 heading, for a page that already shows the title.</param>
    public MarkupString Render(string markdown, bool skipTitle = false)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, pipeline);

        if (skipTitle && document.FirstOrDefault() is HeadingBlock { Level: 1 } title)
        {
            document.Remove(title);
        }

        // The page already has an h1, so the document headings start at h2.
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            heading.Level = Math.Min(heading.Level + 1, 6);
        }

        // An image, or a link with a blocked address, becomes plain text (the alt or the link text). A blocked anchor
        // would otherwise navigate to the current page, and an image would make the viewer's browser fetch its address.
        foreach (var link in document.Descendants<LinkInline>().Where(l => l.IsImage || !IsAllowed(l.Url)).ToList())
        {
            foreach (var child in link.ToList())
            {
                child.Remove();
                link.InsertBefore(child);
            }

            link.Remove();
        }

        // <scheme:address> autolinks are a separate node type and are not limited to http by the parser.
        foreach (var autolink in document.Descendants<AutolinkInline>().Where(a => !IsAllowed(a.Url)).ToList())
        {
            autolink.ReplaceBy(new LiteralInline(autolink.Url));
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
