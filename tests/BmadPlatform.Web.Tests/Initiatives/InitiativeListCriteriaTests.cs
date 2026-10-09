using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Web.Components.Initiatives;

namespace BmadPlatform.Web.Tests.Initiatives;

public sealed class InitiativeListCriteriaTests
{
    [Fact]
    public void Empty_query_means_no_criteria()
    {
        var criteria = InitiativeListCriteria.FromQuery(null, null, null);

        Assert.True(criteria.IsEmpty);
        Assert.Equal("/iniciativas", criteria.ToRelativeUri());
    }

    [Fact]
    public void Whitespace_search_counts_as_no_search()
    {
        var criteria = InitiativeListCriteria.FromQuery("   ", "", "");

        Assert.True(criteria.IsEmpty);
        Assert.Null(criteria.Search);
    }

    [Fact]
    public void Search_status_and_depth_round_trip_through_the_address()
    {
        var original = new InitiativeListCriteria("café & té", InitiativeStatus.ReadyToBuild, InitiativeDepth.Large);

        var uri = new Uri("http://localhost" + original.ToRelativeUri());
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var restored = InitiativeListCriteria.FromQuery(query["q"], query["estado"], query["nivel"]);

        Assert.Equal(original, restored);
        Assert.False(restored.IsEmpty);
    }

    [Fact]
    public void Address_carries_only_the_active_criteria()
    {
        var uri = new InitiativeListCriteria(null, InitiativeStatus.Draft, null).ToRelativeUri();

        Assert.Equal("/iniciativas?estado=Draft", uri);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("99")]
    [InlineData("-1")]
    public void Unknown_status_and_depth_values_are_ignored(string value)
    {
        var criteria = InitiativeListCriteria.FromQuery(null, value, value);

        Assert.Null(criteria.Status);
        Assert.Null(criteria.Depth);
    }

    [Fact]
    public void Values_are_parsed_ignoring_case()
    {
        var criteria = InitiativeListCriteria.FromQuery(null, "clarifying", "SMALL");

        Assert.Equal(InitiativeStatus.Clarifying, criteria.Status);
        Assert.Equal(InitiativeDepth.Small, criteria.Depth);
    }

    [Fact]
    public void Filter_options_list_every_status_and_level_in_order()
    {
        Assert.Equal(
            [InitiativeStatus.Draft, InitiativeStatus.Clarifying, InitiativeStatus.Planning, InitiativeStatus.ReadyToBuild],
            InitiativeListCriteria.StatusOptions);
        Assert.Equal(
            [InitiativeDepth.Small, InitiativeDepth.Standard, InitiativeDepth.Large],
            InitiativeListCriteria.DepthOptions);
    }
}
