using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

public sealed class InitiativeUpdateTests
{
    private readonly InitiativeTestContext context = new();

    [Fact]
    public async Task Editing_the_description_keeps_the_status()
    {
        var id = await context.CreateClarifying("Portal");

        await context.Update(id, "Portal", "Nueva descripción", DepthMode.Manual, InitiativeDepth.Standard);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(InitiativeStatus.Clarifying, details.Status);
        Assert.Equal("Nueva descripción", details.Description);
    }

    [Fact]
    public async Task Depth_can_change_while_clarifying()
    {
        var id = await context.CreateClarifying("Portal");

        await context.Update(id, "Portal", null, DepthMode.Manual, InitiativeDepth.Large);

        Assert.Equal(InitiativeDepth.Large, (await context.Get(id))!.Depth);
    }

    [Fact]
    public async Task Switching_to_automatic_while_clarifying_clears_the_depth()
    {
        var id = await context.CreateClarifying("Portal");

        await context.Update(id, "Portal", null, DepthMode.Automatic, null);

        var details = await context.Get(id);
        Assert.Equal(DepthMode.Automatic, details!.DepthMode);
        Assert.Null(details.Depth);
    }

    [Fact]
    public async Task Name_edit_is_allowed_in_planning_while_unchanged_mode_and_depth_pass()
    {
        var id = await context.CreateClarifying("Portal");
        SetStatus(context.Repository.Stored.Single(), InitiativeStatus.Planning);

        await context.Update(id, "Portal v2", null, DepthMode.Manual, InitiativeDepth.Standard);

        var details = await context.Get(id);
        Assert.Equal("Portal v2", details!.Name);
        Assert.Equal(InitiativeDepth.Standard, details.Depth);
    }

    [Fact]
    public async Task Depth_change_in_planning_is_rejected_and_nothing_changes()
    {
        var id = await context.CreateClarifying("Portal");
        SetStatus(context.Repository.Stored.Single(), InitiativeStatus.Planning);
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<DomainException>(
            () => context.Update(id, "Portal v2", null, DepthMode.Manual, InitiativeDepth.Large));

        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
        Assert.Equal(InitiativeDepth.Standard, (await context.Get(id))!.Depth);
    }

    [Fact]
    public async Task Update_moves_the_initiative_to_the_top_of_the_list()
    {
        var oldest = await context.CreateDraft("Primera");
        await context.CreateDraft("Segunda");
        await context.CreateDraft("Tercera");

        await context.Update(oldest, "Primera editada", null, DepthMode.Automatic, null);

        var list = await context.List();
        Assert.Equal("Primera editada", list[0].Name);
    }

    [Fact]
    public async Task Wizard_steps_and_finish_refresh_the_last_modified_time()
    {
        var id = await context.CreateDraft("App");

        await context.SaveDepth(id, DepthMode.Automatic, null, advance: true);
        Assert.Equal(context.Clock.GetUtcNow(), (await context.Get(id))!.UpdatedAt);

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.Complete(id);
        Assert.Equal(context.Clock.GetUtcNow(), (await context.Get(id))!.UpdatedAt);
    }

    private static void SetStatus(Initiative initiative, InitiativeStatus status) =>
        typeof(Initiative).GetProperty(nameof(Initiative.Status))!.SetValue(initiative, status);
}
