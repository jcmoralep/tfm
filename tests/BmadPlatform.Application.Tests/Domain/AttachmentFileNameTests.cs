using BmadPlatform.Domain.Context;

namespace BmadPlatform.Application.Tests.Domain;

public sealed class AttachmentFileNameTests
{
    [Theory]
    [InlineData("..\\..\\Windows\\evil.txt", "evil.txt")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData("a/b\\c.txt", "c.txt")]
    [InlineData("informe final.pdf", "informe final.pdf")]
    [InlineData("ñandú.txt", "ñandú.txt")]
    [InlineData("con\ttab.txt", "con_tab.txt")]
    [InlineData("a:b*c?d|e.txt", "a_b_c_d_e.txt")]
    [InlineData("  nombre.pdf. ", "nombre.pdf")]
    [InlineData("a.exe.", "a.exe")]
    [InlineData("....", "archivo")]
    [InlineData("", "archivo")]
    [InlineData(null, "archivo")]
    public void Names_are_reduced_to_a_safe_display_name(string? input, string expected)
    {
        Assert.Equal(expected, AttachmentFileName.Normalize(input));
    }

    [Fact]
    public void Quotes_slashes_colons_and_angle_brackets_never_survive()
    {
        var name = AttachmentFileName.Normalize("a\"b\\c:d<e>.html");

        Assert.DoesNotContain(name, character => "\"\\:<>".Contains(character));
        Assert.EndsWith(".html", name);
    }

    [Fact]
    public void A_long_name_is_capped_at_255_characters_and_keeps_its_extension()
    {
        var name = AttachmentFileName.Normalize(new string('a', 296) + ".txt");

        Assert.Equal(255, name.Length);
        Assert.EndsWith(".txt", name);
    }
}
