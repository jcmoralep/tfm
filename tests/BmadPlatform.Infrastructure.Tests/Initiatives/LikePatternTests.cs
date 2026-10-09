using BmadPlatform.Infrastructure.Initiatives;

namespace BmadPlatform.Infrastructure.Tests.Initiatives;

public sealed class LikePatternTests
{
    [Theory]
    [InlineData("pagos", "%pagos%")]
    [InlineData("100%", "%100\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("c:\\temp", "%c:\\\\temp%")]
    [InlineData("\\%_", "%\\\\\\%\\_%")]
    [InlineData("Reseña única", "%Reseña única%")]
    public void Contains_wraps_the_term_and_escapes_wildcards_and_the_escape_character(string term, string expected)
    {
        Assert.Equal(expected, LikePattern.Contains(term));
    }

    [Fact]
    public void Escape_character_is_a_single_backslash()
    {
        Assert.Equal("\\", LikePattern.EscapeCharacter);
    }
}
