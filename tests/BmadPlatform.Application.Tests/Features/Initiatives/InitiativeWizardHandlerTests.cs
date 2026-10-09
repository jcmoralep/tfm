using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

public sealed class InitiativeWizardHandlerTests
{
    private readonly InitiativeTestContext context = new();

    [Fact]
    public async Task Saving_only_a_name_creates_a_draft_owned_by_the_current_user()
    {
        var id = await context.SaveDetails(null, "App de pagos");

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(InitiativeStatus.Draft, details.Status);
        Assert.Null(details.Description);
        Assert.Null(details.DepthMode);
        Assert.Null(details.Depth);
        Assert.Equal(CreationStep.Details, details.CreationStep);
        Assert.Equal(context.Clock.GetUtcNow(), details.CreatedAt);
        Assert.Equal(TimeSpan.Zero, details.CreatedAt.Offset);
        Assert.Equal(InitiativeTestContext.UserA, Assert.Single(context.Repository.Stored).CreatedByUserId);
    }

    [Fact]
    public async Task Duplicate_names_are_allowed_for_the_same_and_for_other_users()
    {
        var first = await context.SaveDetails(null, "Portal");
        var second = await context.SaveDetails(null, "Portal");
        context.User.UserId = InitiativeTestContext.UserB;
        var third = await context.SaveDetails(null, "Portal");

        Assert.Equal(3, new[] { first, second, third }.Distinct().Count());
        Assert.Equal(3, context.Repository.Stored.Count);
    }

    [Fact]
    public async Task Each_step_persists_selections_and_the_step_to_reopen()
    {
        var id = await context.SaveDetails(null, "App", "Descripción", advance: true);
        await context.SaveDepth(id, DepthMode.Manual, InitiativeDepth.Large, advance: true);

        var details = await context.Get(id);

        Assert.NotNull(details);
        Assert.Equal("Descripción", details.Description);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Large, details.Depth);
        Assert.Equal(CreationStep.Review, details.CreationStep);
    }

    [Fact]
    public async Task Resume_returns_the_saved_step_description_and_mode()
    {
        var id = await context.SaveDetails(null, "App", "Descripción", advance: true);
        await context.SaveDepth(id, DepthMode.Automatic, null, advance: false);

        var details = await context.Get(id);

        Assert.NotNull(details);
        Assert.Equal(CreationStep.Depth, details.CreationStep);
        Assert.Equal("Descripción", details.Description);
        Assert.Equal(DepthMode.Automatic, details.DepthMode);
    }

    [Fact]
    public async Task Saving_without_advancing_keeps_the_user_on_the_same_step()
    {
        var id = await context.SaveDetails(null, "App", advance: true);

        await context.SaveDetails(id, "App renombrada", advance: false);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal("App renombrada", details.Name);
        Assert.Equal(CreationStep.Details, details.CreationStep);
    }

    [Fact]
    public async Task Finishing_a_valid_draft_moves_it_to_clarifying()
    {
        var id = await context.CreateDraft("App");
        await context.SaveDepth(id, DepthMode.Manual, InitiativeDepth.Standard, advance: true);

        await context.Complete(id);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(InitiativeStatus.Clarifying, details.Status);
    }

    [Fact]
    public async Task Automatic_mode_leaves_depth_empty()
    {
        var id = await context.CreateDraft("App");
        await context.SaveDepth(id, DepthMode.Automatic, null, advance: true);
        await context.Complete(id);

        var details = await context.Get(id);

        Assert.NotNull(details);
        Assert.Equal(DepthMode.Automatic, details.DepthMode);
        Assert.Null(details.Depth);
    }

    [Fact]
    public async Task Manual_mode_without_a_level_cannot_finish_and_stays_draft()
    {
        var id = await context.CreateDraft("App");
        await context.SaveDepth(id, DepthMode.Manual, null, advance: true);

        await Assert.ThrowsAsync<DomainException>(() => context.Complete(id));

        var details = await context.Get(id);
        Assert.Equal(InitiativeStatus.Draft, details!.Status);
    }

    [Fact]
    public async Task Finishing_a_non_draft_is_rejected_and_status_is_unchanged()
    {
        var id = await context.CreateClarifying("App");

        await Assert.ThrowsAsync<DomainException>(() => context.Complete(id));

        var details = await context.Get(id);
        Assert.Equal(InitiativeStatus.Clarifying, details!.Status);
    }

    [Fact]
    public async Task Handlers_fail_without_a_current_user_and_persist_nothing()
    {
        context.User.UserId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveDetails(null, "App"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Get(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.List());
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Delete(Guid.NewGuid()));

        Assert.Empty(context.Repository.Stored);
        Assert.Equal(0, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Update_save_depth_and_complete_also_fail_without_a_current_user_and_persist_nothing()
    {
        var id = await context.CreateDraft("App");
        var updatesBefore = context.Repository.UpdateCount;
        context.User.UserId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Update(id, "Otra", null, DepthMode.Automatic, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveDepth(id, DepthMode.Automatic, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Complete(id));

        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal("App", stored.Name);
        Assert.Null(stored.DepthMode);
        Assert.Equal(InitiativeStatus.Draft, stored.Status);
    }

    [Fact]
    public async Task GetRequiredIdAsync_returns_the_signed_in_user_id()
    {
        Assert.Equal(InitiativeTestContext.UserA, await context.User.GetRequiredIdAsync());
    }
}
