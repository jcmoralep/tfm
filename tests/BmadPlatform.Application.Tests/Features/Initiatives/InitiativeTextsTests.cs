using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

public sealed class InitiativeTextsTests
{
    [Theory]
    [InlineData(InitiativeDepth.Small, "una especificación breve")]
    [InlineData(InitiativeDepth.Standard, "Brief y PRD")]
    [InlineData(InitiativeDepth.Large, "PRD, Arquitectura y Épicas e historias")]
    public void In_the_middle_of_a_sentence_only_an_article_loses_its_capital(InitiativeDepth depth, string expected)
    {
        Assert.Equal(expected, InitiativeTexts.DeliverablesInSentence(depth));
    }
}
