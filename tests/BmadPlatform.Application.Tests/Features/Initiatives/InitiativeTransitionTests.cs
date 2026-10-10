using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

public sealed class InitiativeTransitionTests
{
    private readonly InitiativeTestContext context = new();

    [Fact]
    public async Task StartPlanning_moves_the_owned_clarifying_initiative_to_planning()
    {
        var id = await context.CreateClarifying("App", InitiativeDepth.Standard);

        await context.StartPlanning(id);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(InitiativeStatus.Planning, details.Status);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, details.Depth);
        Assert.Equal(context.Clock.GetUtcNow(), details.UpdatedAt);
    }

    [Fact]
    public async Task StartPlanning_repeated_succeeds_without_refreshing_the_last_modified_time()
    {
        var id = await context.CreateClarifying("App");
        await context.StartPlanning(id);
        var updatedAt = (await context.Get(id))!.UpdatedAt;
        context.Clock.Advance(TimeSpan.FromHours(1));

        await context.StartPlanning(id);

        var details = await context.Get(id);
        Assert.Equal(InitiativeStatus.Planning, details!.Status);
        Assert.Equal(updatedAt, details.UpdatedAt);
    }

    [Fact]
    public async Task StartPlanning_is_rejected_for_a_draft_and_for_ready_to_build()
    {
        var draft = await context.CreateDraft("Borrador");
        var ready = await context.CreateClarifying("Lista");
        context.ForceStatus(ready, InitiativeStatus.ReadyToBuild);

        var draftError = await Assert.ThrowsAsync<DomainException>(() => context.StartPlanning(draft));
        var readyError = await Assert.ThrowsAsync<DomainException>(() => context.StartPlanning(ready));

        Assert.Equal("La iniciativa debe estar en Aclarando para pasar a Planificando.", draftError.Message);
        Assert.Equal("La iniciativa debe estar en Aclarando para pasar a Planificando.", readyError.Message);
        Assert.Equal(InitiativeStatus.Draft, (await context.Get(draft))!.Status);
        Assert.Equal(InitiativeStatus.ReadyToBuild, (await context.Get(ready))!.Status);
    }

    [Fact]
    public async Task StartPlanning_with_an_empty_depth_is_rejected_and_status_stays_clarifying()
    {
        var id = await context.CreateDraft("App");
        await context.SaveDepth(id, DepthMode.Automatic, null, advance: true);
        await context.Complete(id);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.StartPlanning(id));

        Assert.Equal("Elija un nivel de profundidad antes de pasar a Planificando.", error.Message);
        Assert.Equal(InitiativeStatus.Clarifying, (await context.Get(id))!.Status);
    }

    [Fact]
    public async Task StartPlanning_and_SetInitiativeDepth_are_not_found_for_other_users_and_deleted_initiatives()
    {
        var foreign = await context.CreateClarifying("De A");
        var deleted = await context.CreateClarifying("Borrada");
        await context.Delete(deleted);
        var updatesBefore = context.Repository.UpdateCount;

        await Assert.ThrowsAsync<NotFoundException>(() => context.StartPlanning(deleted));
        await Assert.ThrowsAsync<NotFoundException>(() => context.SetInitiativeDepth(deleted, InitiativeDepth.Large));
        await Assert.ThrowsAsync<NotFoundException>(() => context.StartPlanning(Guid.NewGuid()));

        context.User.UserId = InitiativeTestContext.UserB;
        await Assert.ThrowsAsync<NotFoundException>(() => context.StartPlanning(foreign));
        await Assert.ThrowsAsync<NotFoundException>(() => context.SetInitiativeDepth(foreign, InitiativeDepth.Large));

        Assert.Equal(updatesBefore, context.Repository.UpdateCount);
        Assert.Equal(InitiativeStatus.Clarifying, context.Repository.Stored.Single(i => i.Id == foreign).Status);
    }

    [Fact]
    public void Transition_contracts_expose_only_the_initiative_id_and_no_status()
    {
        var startPlanning = typeof(StartPlanningCommand).GetProperties().Select(p => p.Name).ToList();
        var setDepth = typeof(SetInitiativeDepthCommand).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal([nameof(StartPlanningCommand.Id)], startPlanning);
        Assert.Equal([nameof(SetInitiativeDepthCommand.Id), nameof(SetInitiativeDepthCommand.Depth)], setDepth);
        Assert.DoesNotContain(startPlanning.Concat(setDepth), name => name.Contains("Status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetInitiativeDepth_turns_automatic_with_an_empty_depth_into_manual_with_the_level()
    {
        var id = await context.CreateDraft("App");
        await context.SaveDepth(id, DepthMode.Automatic, null, advance: true);
        await context.Complete(id);

        await context.SetInitiativeDepth(id, InitiativeDepth.Standard);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, details.Depth);
        Assert.Equal(InitiativeStatus.Clarifying, details.Status);
    }

    [Fact]
    public async Task SetInitiativeDepth_replaces_an_existing_level_and_keeps_name_and_description()
    {
        var id = await context.CreateClarifying("App", InitiativeDepth.Small);
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        await context.SetInitiativeDepth(id, InitiativeDepth.Large);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(InitiativeDepth.Large, details.Depth);
        Assert.Equal(DepthMode.Manual, details.DepthMode);
        Assert.Equal("App", details.Name);
        Assert.Equal(context.Clock.GetUtcNow(), details.UpdatedAt);
    }

    [Theory]
    [InlineData(InitiativeStatus.Planning)]
    [InlineData(InitiativeStatus.ReadyToBuild)]
    public async Task SetInitiativeDepth_is_rejected_in_locked_statuses(InitiativeStatus status)
    {
        var id = await context.CreateClarifying("App", InitiativeDepth.Small);
        context.ForceStatus(id, status);

        await Assert.ThrowsAsync<DomainException>(() => context.SetInitiativeDepth(id, InitiativeDepth.Large));

        var details = await context.Get(id);
        Assert.Equal(DepthMode.Manual, details!.DepthMode);
        Assert.Equal(InitiativeDepth.Small, details.Depth);
    }

    [Fact]
    public void SetInitiativeDepth_validator_rejects_an_undefined_level()
    {
        var validator = new SetInitiativeDepthCommandValidator();

        var invalid = validator.Validate(new SetInitiativeDepthCommand(Guid.NewGuid(), (InitiativeDepth)99));
        var valid = validator.Validate(new SetInitiativeDepthCommand(Guid.NewGuid(), InitiativeDepth.Large));

        Assert.Equal("El nivel de profundidad no es válido.", Assert.Single(invalid.Errors).ErrorMessage);
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task Editing_the_level_in_clarifying_changes_only_the_depth()
    {
        var id = await context.CreateClarifying("App", InitiativeDepth.Standard);

        await context.Update(id, "App", null, DepthMode.Manual, InitiativeDepth.Large);

        var details = await context.Get(id);
        Assert.NotNull(details);
        Assert.Equal(InitiativeDepth.Large, details.Depth);
        Assert.Equal(InitiativeStatus.Clarifying, details.Status);
    }

    [Fact]
    public async Task Only_wizard_completion_and_start_planning_change_the_status()
    {
        var id = await context.CreateDraft("App");
        Assert.Equal(InitiativeStatus.Draft, (await context.Get(id))!.Status);

        await context.SaveDetails(id, "App 2", "Texto", advance: true);
        await context.SaveDepth(id, DepthMode.Manual, InitiativeDepth.Standard, advance: true);
        await context.Update(id, "App 3", null, DepthMode.Manual, InitiativeDepth.Large);
        Assert.Equal(InitiativeStatus.Draft, (await context.Get(id))!.Status);

        await context.Complete(id);
        Assert.Equal(InitiativeStatus.Clarifying, (await context.Get(id))!.Status);

        await context.SetInitiativeDepth(id, InitiativeDepth.Small);
        await context.Update(id, "App 4", null, DepthMode.Manual, InitiativeDepth.Standard);
        Assert.Equal(InitiativeStatus.Clarifying, (await context.Get(id))!.Status);

        await context.StartPlanning(id);
        Assert.Equal(InitiativeStatus.Planning, (await context.Get(id))!.Status);
    }
}
