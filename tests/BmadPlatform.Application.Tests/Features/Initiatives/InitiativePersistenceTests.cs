using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

/// <summary>
/// Handlers must persist what they change: the repository double keeps its own copies, so a handler that
/// forgets to call it leaves the stored record untouched and these tests fail. Also covers rejected operations
/// leaving nothing half-applied.
/// </summary>
public sealed class InitiativePersistenceTests
{
    private readonly InitiativeTestContext context = new();

    [Fact]
    public async Task Creating_a_draft_stores_it()
    {
        var id = await context.SaveDetails(null, "App", "Texto", advance: true);

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal(id, stored.Id);
        Assert.Equal("App", stored.Name);
        Assert.Equal(CreationStep.Depth, stored.CreationStep);
    }

    [Fact]
    public async Task Saving_the_details_of_an_existing_draft_persists_the_change()
    {
        var id = await context.CreateDraft("App");
        var updatesBefore = context.Repository.UpdateCount;

        await context.SaveDetails(id, "App renombrada", "Texto", advance: true);

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal("App renombrada", stored.Name);
        Assert.Equal("Texto", stored.Description);
        Assert.Equal(CreationStep.Depth, stored.CreationStep);
        Assert.Equal(updatesBefore + 1, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Saving_the_depth_persists_mode_level_and_step()
    {
        var id = await context.CreateDraft("App");
        var updatesBefore = context.Repository.UpdateCount;

        await context.SaveDepth(id, DepthMode.Manual, InitiativeDepth.Large, advance: true);

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal(DepthMode.Manual, stored.DepthMode);
        Assert.Equal(InitiativeDepth.Large, stored.Depth);
        Assert.Equal(CreationStep.Review, stored.CreationStep);
        Assert.Equal(updatesBefore + 1, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Completing_persists_the_new_status()
    {
        var id = await context.CreateDraft("App");
        await context.SaveDepth(id, DepthMode.Automatic, null, advance: true);
        var updatesBefore = context.Repository.UpdateCount;

        await context.Complete(id);

        Assert.Equal(InitiativeStatus.Clarifying, Assert.Single(context.Repository.Stored).Status);
        Assert.Equal(updatesBefore + 1, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Updating_persists_name_description_and_depth()
    {
        var id = await context.CreateClarifying("Portal");
        var updatesBefore = context.Repository.UpdateCount;

        await context.Update(id, "Portal v2", "Nueva", DepthMode.Manual, InitiativeDepth.Large);

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal("Portal v2", stored.Name);
        Assert.Equal("Nueva", stored.Description);
        Assert.Equal(InitiativeDepth.Large, stored.Depth);
        Assert.Equal(updatesBefore + 1, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Deleting_persists_the_deletion_mark()
    {
        var id = await context.CreateDraft("Portal");
        var updatesBefore = context.Repository.UpdateCount;

        await context.Delete(id);

        Assert.NotNull(Assert.Single(context.Repository.Stored).DeletedAt);
        Assert.Equal(updatesBefore + 1, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Rejected_update_leaves_the_name_unchanged_too()
    {
        var id = await context.CreateClarifying("Portal");
        context.ForceStatus(id, InitiativeStatus.Planning);
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<DomainException>(
            () => context.Update(id, "Portal v2", "Otra", DepthMode.Manual, InitiativeDepth.Large));

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal("Portal", stored.Name);
        Assert.Null(stored.Description);
        Assert.Equal(InitiativeDepth.Standard, stored.Depth);
        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Saving_details_of_a_non_draft_is_rejected_and_nothing_changes()
    {
        var id = await context.CreateClarifying("Portal");
        var before = Assert.Single(context.Repository.Stored);
        var step = before.CreationStep;
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<DomainException>(() => context.SaveDetails(id, "Otra", "Texto", advance: true));

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal("Portal", stored.Name);
        Assert.Null(stored.Description);
        Assert.Equal(step, stored.CreationStep);
        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Saving_the_depth_of_a_non_draft_is_rejected_and_nothing_changes()
    {
        // Clarifying still allows a depth change, so the depth is applied before the wizard step is rejected.
        var id = await context.CreateClarifying("Portal", InitiativeDepth.Standard);
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<DomainException>(() => context.SaveDepth(id, DepthMode.Manual, InitiativeDepth.Large));

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal(InitiativeDepth.Standard, stored.Depth);
        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
    }

    [Theory]
    [InlineData(InitiativeStatus.Planning)]
    [InlineData(InitiativeStatus.ReadyToBuild)]
    public async Task Changing_the_level_is_rejected_once_planning_or_ready(InitiativeStatus status)
    {
        var id = await context.CreateClarifying("Portal", InitiativeDepth.Standard);
        context.ForceStatus(id, status);

        await Assert.ThrowsAsync<DomainException>(
            () => context.Update(id, "Portal", null, DepthMode.Manual, InitiativeDepth.Large));

        Assert.Equal(InitiativeDepth.Standard, Assert.Single(context.Repository.Stored).Depth);
    }

    [Fact]
    public async Task Changing_the_mode_is_rejected_in_planning_and_nothing_changes()
    {
        var id = await context.CreateClarifying("Portal", InitiativeDepth.Standard);
        context.ForceStatus(id, InitiativeStatus.Planning);

        await Assert.ThrowsAsync<DomainException>(
            () => context.Update(id, "Portal", null, DepthMode.Automatic, null));

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal(DepthMode.Manual, stored.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, stored.Depth);
    }

    [Fact]
    public async Task A_draft_can_change_its_depth_through_update()
    {
        var id = await context.CreateDraft("Portal");
        await context.SaveDepth(id, DepthMode.Manual, InitiativeDepth.Small);

        await context.Update(id, "Portal", null, DepthMode.Manual, InitiativeDepth.Large);

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal(InitiativeDepth.Large, stored.Depth);
        Assert.Equal(InitiativeStatus.Draft, stored.Status);
    }

    [Fact]
    public async Task A_stale_update_of_a_deleted_initiative_is_not_found_and_keeps_it_deleted()
    {
        var id = await context.CreateDraft("Portal");
        var stale = await context.Repository.GetAsync(id, InitiativeTestContext.UserA, default);
        await context.Delete(id);
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<NotFoundException>(() => context.Repository.UpdateAsync(stale!, default));

        Assert.NotNull(Assert.Single(context.Repository.Stored).DeletedAt);
        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Initiatives_with_the_same_modification_time_keep_a_stable_order()
    {
        // No clock advance between them, so every UpdatedAt is equal.
        var ids = new List<Guid>
        {
            await context.SaveDetails(null, "Uno"),
            await context.SaveDetails(null, "Dos"),
            await context.SaveDetails(null, "Tres"),
        };

        var list = await context.List();

        Assert.Equal(ids.OrderByDescending(id => id), list.Select(item => item.Id));
    }
}
