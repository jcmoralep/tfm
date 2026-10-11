using BmadPlatform.Web.Components.Artifacts;

namespace BmadPlatform.Web.Tests.Artifacts;

public sealed class MarkdownRendererTests
{
    private readonly MarkdownRenderer renderer = new();

    private string Html(string markdown) => renderer.Render(markdown).Value;

    [Theory]
    [InlineData("<script>alert(1)</script>", "<script")]
    [InlineData("<img src=x onerror=alert(1)>", "<img")]
    [InlineData("<iframe src=\"https://example.com\"></iframe>", "<iframe")]
    [InlineData("<a href=\"javascript:alert(1)\" onclick=\"x()\">x</a>", "<a ")]
    public void Raw_html_is_shown_as_text_not_interpreted(string markdown, string forbiddenTag)
    {
        var html = Html(markdown);

        Assert.DoesNotContain(forbiddenTag, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("[x](java\tscript:alert(1))")]
    [InlineData("[x](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[x](vbscript:msgbox(1))")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("[x][r]\n\n[r]: javascript:alert(1)")]
    [InlineData("<javascript:alert(1)>")]
    public void Dangerous_addresses_become_plain_text(string markdown)
    {
        var html = Html(markdown);

        Assert.DoesNotContain("<a", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("href=", html);
        Assert.DoesNotContain("src=", html);
    }

    [Theory]
    [InlineData("[x](https://example.com/a?b=c:d)", "https://example.com/a?b=c:d")]
    [InlineData("[x](http://example.com)", "http://example.com")]
    [InlineData("[x](mailto:a@example.com)", "mailto:a@example.com")]
    [InlineData("[x](/iniciativas/1)", "/iniciativas/1")]
    [InlineData("[x](#seccion)", "#seccion")]
    [InlineData("[x](docs/a:b.md)", "docs/a:b.md")]
    public void Safe_addresses_are_kept(string markdown, string address)
    {
        Assert.Contains($"href=\"{address}\"", Html(markdown));
    }

    [Fact]
    public void Blocked_link_keeps_its_text_and_blocked_image_keeps_its_alt()
    {
        var html = Html("[ver aqui](javascript:alert(1)) y ![logo](javascript:alert(1))");

        Assert.Contains("ver aqui", html);
        Assert.Contains("logo", html);
    }

    [Theory]
    [InlineData("![logo de la empresa](https://example.com/a.png)")]
    [InlineData("![logo de la empresa](/local.png)")]
    [InlineData("![logo de la empresa][r]\n\n[r]: https://example.com/a.png")]
    public void Images_are_never_rendered_and_show_their_alt_text(string markdown)
    {
        var html = Html(markdown);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("src=", html);
        Assert.Contains("logo de la empresa", html);
    }

    [Theory]
    [InlineData("# T", "<h2")]
    [InlineData("## T", "<h3")]
    [InlineData("###### T", "<h6")]
    public void Headings_are_demoted_one_level_and_capped_at_h6(string markdown, string expected)
    {
        var html = Html(markdown);

        Assert.Contains(expected, html);
        Assert.DoesNotContain("<h1", html);
    }

    [Fact]
    public void Markdown_is_presented_as_a_document()
    {
        var html = Html("# Titulo\n\n- uno\n- dos\n\n| a | b |\n|---|---|\n| 1 | 2 |\n");

        Assert.Contains("<h2", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>", html);
    }

    [Theory]
    [InlineData("# Brief: Mi idea\n\n## Visión\n\nTexto", "Brief: Mi idea", "Visión")]
    [InlineData("\n\n# Brief: Mi idea\n\nTexto\n\n# Otro\n", "Brief: Mi idea", "Otro")]
    public void Skip_title_omits_only_the_leading_level_one_heading(string markdown, string omitted, string kept)
    {
        var html = renderer.Render(markdown, skipTitle: true).Value;

        Assert.DoesNotContain(omitted, html);
        Assert.Contains(kept, html);
    }

    [Theory]
    [InlineData("Texto\n\n# Brief: Mi idea")]
    [InlineData("## Brief: Mi idea")]
    public void Skip_title_keeps_headings_when_the_first_block_is_not_a_level_one_heading(string markdown)
    {
        Assert.Contains("Brief: Mi idea", renderer.Render(markdown, skipTitle: true).Value);
    }
}
