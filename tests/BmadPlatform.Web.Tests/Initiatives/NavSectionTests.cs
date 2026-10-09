using BmadPlatform.Web.Components.Layout;

namespace BmadPlatform.Web.Tests.Initiatives;

public sealed class NavSectionTests
{
    [Theory]
    [InlineData("iniciativas", true)]
    [InlineData("iniciativas/", true)]
    [InlineData("Iniciativas", true)]
    [InlineData("iniciativas?q=a", true)]
    [InlineData("iniciativas#top", true)]
    [InlineData("iniciativas/nueva", true)]
    [InlineData("iniciativas/6f1c0f0e-0000-0000-0000-000000000000/editar?x=1", true)]
    [InlineData("", false)]
    [InlineData("login", false)]
    [InlineData("iniciativas-otras", false)]
    [InlineData("otra/iniciativas", false)]
    public void Section_matches_itself_and_its_children_only(string relativePath, bool expected)
    {
        Assert.Equal(expected, NavSection.IsActive(relativePath, "iniciativas"));
    }
}
