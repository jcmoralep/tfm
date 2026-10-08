using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

public sealed class InitiativeListTests
{
    private readonly InitiativeTestContext context = new();

    [Fact]
    public async Task List_contains_only_the_callers_initiatives()
    {
        await context.CreateDraft("A1");
        await context.CreateDraft("A2");
        context.User.UserId = InitiativeTestContext.UserB;
        await context.CreateDraft("B1");
        await context.CreateDraft("B2");
        await context.CreateDraft("B3");
        context.User.UserId = InitiativeTestContext.UserA;

        var list = await context.List();

        Assert.Equal(["A2", "A1"], list.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task List_is_ordered_by_last_modification_newest_first()
    {
        await context.CreateDraft("T1");
        await context.CreateDraft("T2");
        await context.CreateDraft("T3");

        var list = await context.List();

        Assert.Equal(["T3", "T2", "T1"], list.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Search_matches_names_ignoring_case_and_surrounding_whitespace()
    {
        await context.CreateDraft("Portal clientes");
        await context.CreateDraft("App móvil");
        await context.CreateDraft("portal interno");

        var list = await context.List(search: "  PORTAL ");

        Assert.Equal(["portal interno", "Portal clientes"], list.Select(i => i.Name).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_or_whitespace_search_returns_everything(string? search)
    {
        await context.CreateDraft("Uno");
        await context.CreateDraft("Dos");
        await context.CreateDraft("Tres");

        Assert.Equal(3, (await context.List(search: search)).Count);
    }

    [Fact]
    public async Task Search_without_a_match_returns_an_empty_list()
    {
        await context.CreateDraft("Uno");

        Assert.Empty(await context.List(search: "zzz"));
    }

    [Fact]
    public async Task Filters_by_status_and_by_depth()
    {
        await context.CreateDraft("Borrador");
        await context.CreateClarifying("Pequeña", InitiativeDepth.Small);
        await context.CreateClarifying("Grande", InitiativeDepth.Large);

        var clarifying = await context.List(status: InitiativeStatus.Clarifying);
        var large = await context.List(depth: InitiativeDepth.Large);

        Assert.Equal(["Grande", "Pequeña"], clarifying.Select(i => i.Name).ToArray());
        Assert.Equal(["Grande"], large.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Search_status_and_depth_combine_with_and()
    {
        await context.CreateClarifying("Portal grande", InitiativeDepth.Large);   // matches all three
        await context.CreateClarifying("Portal pequeño", InitiativeDepth.Small);  // search + status only
        await context.CreateClarifying("App grande", InitiativeDepth.Large);      // status + depth only
        var draft = await context.CreateDraft("Portal borrador");                 // search only
        await context.SaveDepth(draft, DepthMode.Manual, InitiativeDepth.Large);  // search + depth, still a draft

        var list = await context.List("portal", InitiativeStatus.Clarifying, InitiativeDepth.Large);

        Assert.Equal(["Portal grande"], list.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Filters_matching_nothing_return_an_empty_list()
    {
        await context.CreateClarifying("Uno", InitiativeDepth.Small);

        Assert.Empty(await context.List(status: InitiativeStatus.Planning, depth: InitiativeDepth.Large));
    }

    [Fact]
    public async Task There_is_no_pagination()
    {
        for (var i = 0; i < 60; i++)
        {
            await context.CreateDraft($"Iniciativa {i}");
        }

        Assert.Equal(60, (await context.List()).Count);
    }

    [Fact]
    public async Task Detail_carries_every_field()
    {
        var id = await context.CreateClarifying("Portal", InitiativeDepth.Small);

        var details = await context.Get(id);

        Assert.NotNull(details);
        Assert.Equal(id, details.Id);
        Assert.Equal("Portal", details.Name);
        Assert.Equal(InitiativeStatus.Clarifying, details.Status);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Small, details.Depth);
        Assert.Null(details.DepthPendingText);
        Assert.True(details.UpdatedAt >= details.CreatedAt);
    }
}
