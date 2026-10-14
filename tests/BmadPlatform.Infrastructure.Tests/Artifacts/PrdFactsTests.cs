using BmadPlatform.Infrastructure.Artifacts;

namespace BmadPlatform.Infrastructure.Tests.Artifacts;

public sealed class PrdFactsTests
{
    [Theory]
    [InlineData("FR-3", "FR-3")]
    [InlineData("fr3", "FR-3")]
    [InlineData("FR 3", "FR-3")]
    [InlineData(" nfr - 12 ", "NFR-12")]
    [InlineData("FR-0", null)]
    [InlineData("FR-03", null)]
    [InlineData("AD-1", null)]
    [InlineData("FR-3 y FR-4", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void An_identifier_written_loosely_is_canonical_or_rejected(string? raw, string? expected)
    {
        Assert.Equal(expected, PrdFacts.CanonicalId(raw));
    }

    [Fact]
    public void The_facts_list_the_requirements_in_prd_order_and_know_only_those()
    {
        var facts = new PrdFacts("# PRD\n\n- **FR-1**: Buscar\n- **NFR-1**: Rápido\n- **FR-2**: Ver\n");

        Assert.Equal(["FR-1", "FR-2"], facts.Functional.Select(r => r.Id));
        Assert.Equal(["NFR-1"], facts.NonFunctional.Select(r => r.Id));
        Assert.True(facts.Knows("FR-2"));
        Assert.False(facts.Knows("FR-9"));
        Assert.True(facts.PositionOf("NFR-1") < facts.PositionOf("FR-2"));
    }

    [Theory]
    [InlineData(1500, 400, 3)]
    [InlineData(1000, 250, 3)]
    [InlineData(600, 160, 2)]
    [InlineData(300, 100, 2)]
    [InlineData(100, 60, 1)]
    public void Each_rung_of_the_ladder_maps_to_a_field_clip_and_a_criteria_count(int answerBudget, int clip, int criteria)
    {
        Assert.Equal(new LayoutBudget(clip, criteria), LayoutBudget.For(answerBudget));
    }
}
