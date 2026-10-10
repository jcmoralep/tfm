using BmadPlatform.Application.Features.Artifacts.Markdown;

namespace BmadPlatform.Application.Tests.Features.Artifacts.Markdown;

public sealed class ItemNumbererTests
{
    [Fact]
    public void Numbers_are_unique_and_ordered_per_prefix_from_one()
    {
        var numberer = new ItemNumberer();

        var ids = new[]
        {
            numberer.Next(ItemPrefix.FunctionalRequirement),
            numberer.Next(ItemPrefix.FunctionalRequirement),
            numberer.Next(ItemPrefix.NonFunctionalRequirement),
            numberer.Next(ItemPrefix.FunctionalRequirement),
            numberer.Next(ItemPrefix.Question),
            numberer.Next(ItemPrefix.NonFunctionalRequirement),
        };

        Assert.Equal(["FR-1", "FR-2", "NFR-1", "FR-3", "Q-1", "NFR-2"], ids);
        Assert.Equal(3, numberer.Count(ItemPrefix.FunctionalRequirement));
        Assert.Equal(0, numberer.Count(ItemPrefix.UserJourney));
    }

    [Fact]
    public void A_new_numberer_numbers_from_scratch()
    {
        var first = new ItemNumberer();
        first.Next(ItemPrefix.Assumption);
        first.Next(ItemPrefix.Assumption);

        var second = new ItemNumberer();

        Assert.Equal("A-1", second.Next(ItemPrefix.Assumption));
    }

    [Fact]
    public void All_kinds_are_listed_longest_first()
    {
        Assert.Equal(["NFR", "FR", "UJ", "SM", "A", "Q"], ItemPrefix.All);
    }
}
