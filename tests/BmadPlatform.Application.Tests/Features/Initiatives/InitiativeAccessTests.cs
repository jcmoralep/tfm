using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

/// <summary>Owner isolation and soft delete: another user's or a deleted initiative reads as not found.</summary>
public sealed class InitiativeAccessTests
{
    private readonly InitiativeTestContext context = new();

    [Fact]
    public async Task Other_users_detail_is_the_same_null_as_an_unknown_id()
    {
        var id = await context.CreateClarifying("Portal");
        context.User.UserId = InitiativeTestContext.UserB;

        Assert.Null(await context.Get(id));
        Assert.Null(await context.Get(Guid.NewGuid()));
    }

    [Fact]
    public async Task Other_user_cannot_update_save_a_step_finish_or_delete()
    {
        var id = await context.CreateDraft("Portal");
        context.User.UserId = InitiativeTestContext.UserB;

        await Assert.ThrowsAsync<NotFoundException>(() => context.Update(id, "Hackeada", null, DepthMode.Automatic, null));
        await Assert.ThrowsAsync<NotFoundException>(() => context.SaveDetails(id, "Hackeada"));
        await Assert.ThrowsAsync<NotFoundException>(() => context.SaveDepth(id, DepthMode.Automatic, null));
        await Assert.ThrowsAsync<NotFoundException>(() => context.Complete(id));
        await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(id));

        var stored = Assert.Single(context.Repository.Stored);
        Assert.Equal("Portal", stored.Name);
        Assert.Null(stored.DeletedAt);
        Assert.Equal(0, context.Repository.UpdateCount);
    }

    [Fact]
    public async Task Deleting_hides_the_initiative_but_retains_the_record()
    {
        var id = await context.CreateClarifying("Portal");

        await context.Delete(id);

        Assert.Null(await context.Get(id));
        Assert.Empty(await context.List());
        Assert.Empty(await context.List(search: "Portal", status: InitiativeStatus.Clarifying, depth: InitiativeDepth.Standard));
        var stored = Assert.Single(context.Repository.Stored);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(context.Clock.GetUtcNow(), stored.UpdatedAt);
    }

    [Fact]
    public async Task Deleted_initiative_cannot_be_updated_stepped_or_finished()
    {
        var id = await context.CreateDraft("Portal");
        await context.Delete(id);
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<NotFoundException>(() => context.Update(id, "Otra", null, DepthMode.Automatic, null));
        await Assert.ThrowsAsync<NotFoundException>(() => context.SaveDetails(id, "Otra"));
        await Assert.ThrowsAsync<NotFoundException>(() => context.SaveDepth(id, DepthMode.Automatic, null));
        await Assert.ThrowsAsync<NotFoundException>(() => context.Complete(id));

        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
        Assert.Equal("Portal", Assert.Single(context.Repository.Stored).Name);
    }

    [Fact]
    public async Task Deleting_twice_is_not_found()
    {
        var id = await context.CreateDraft("Portal");
        await context.Delete(id);

        await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(id));
    }

    [Fact]
    public async Task Deleting_an_unknown_id_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => context.Delete(Guid.NewGuid()));
    }
}
