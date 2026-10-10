using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Domain;

public sealed class InitiativeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);

    private static Initiative NewDraft(string name = "App de pagos") =>
        Initiative.CreateDraft("user-a", name, null, T0);

    private static Initiative InStatus(InitiativeStatus status)
    {
        var initiative = NewDraft();
        initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Standard, T0);
        initiative.Complete(T0);

        if (status == InitiativeStatus.Clarifying)
        {
            return initiative;
        }

        // ReadyToBuild has no transition yet (Planning does, but this helper keeps one path), so reflection reaches both.
        typeof(Initiative).GetProperty(nameof(Initiative.Status))!.SetValue(initiative, status);

        return initiative;
    }

    [Fact]
    public void Draft_has_expected_defaults()
    {
        var initiative = Initiative.CreateDraft("user-a", "  App de pagos  ", null, T0);

        Assert.NotEqual(Guid.Empty, initiative.Id);
        Assert.Equal("user-a", initiative.CreatedByUserId);
        Assert.Equal("App de pagos", initiative.Name);
        Assert.Null(initiative.Description);
        Assert.Equal(InitiativeStatus.Draft, initiative.Status);
        Assert.Equal(CreationStep.Details, initiative.CreationStep);
        Assert.Null(initiative.DepthMode);
        Assert.Null(initiative.Depth);
        Assert.Equal(T0, initiative.CreatedAt);
        Assert.Equal(T0, initiative.UpdatedAt);
        Assert.Null(initiative.DeletedAt);
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        var error = Assert.Throws<DomainException>(() => NewDraft("   "));

        Assert.Equal("El nombre es obligatorio.", error.Message);
    }

    [Fact]
    public void Name_over_limit_is_rejected_but_trimmed_limit_is_accepted()
    {
        var accepted = NewDraft($" {new string('a', Initiative.NameMaxLength)} ");
        var error = Assert.Throws<DomainException>(() => NewDraft(new string('a', Initiative.NameMaxLength + 1)));

        Assert.Equal(Initiative.NameMaxLength, accepted.Name.Length);
        Assert.Equal("El nombre no puede superar los 120 caracteres.", error.Message);
    }

    [Fact]
    public void Description_over_limit_is_rejected()
    {
        var initiative = NewDraft();

        initiative.Rename("App", new string('d', Initiative.DescriptionMaxLength), T1);
        var error = Assert.Throws<DomainException>(
            () => initiative.Rename("App", new string('d', Initiative.DescriptionMaxLength + 1), T1));

        Assert.Equal("La descripción no puede superar los 1000 caracteres.", error.Message);
    }

    [Fact]
    public void Manual_mode_stores_the_chosen_level()
    {
        var initiative = NewDraft();

        initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Standard, T1);

        Assert.Equal(DepthMode.Manual, initiative.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, initiative.Depth);
    }

    [Fact]
    public void Switching_to_automatic_clears_the_depth()
    {
        var initiative = NewDraft();
        initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Large, T0);

        initiative.SetDepth(DepthMode.Automatic, InitiativeDepth.Large, T1);

        Assert.Equal(DepthMode.Automatic, initiative.DepthMode);
        Assert.Null(initiative.Depth);
    }

    [Fact]
    public void Depth_without_a_mode_is_rejected()
    {
        var initiative = NewDraft();

        Assert.Throws<DomainException>(() => initiative.SetDepth(null, InitiativeDepth.Small, T1));
    }

    [Fact]
    public void Clarifying_manual_mode_requires_a_level()
    {
        var initiative = InStatus(InitiativeStatus.Clarifying);

        Assert.Throws<DomainException>(() => initiative.SetDepth(DepthMode.Manual, null, T1));
        Assert.Equal(InitiativeDepth.Standard, initiative.Depth);
    }

    [Theory]
    [InlineData(InitiativeStatus.Draft)]
    [InlineData(InitiativeStatus.Clarifying)]
    public void Depth_can_change_in_draft_and_clarifying(InitiativeStatus status)
    {
        var initiative = status == InitiativeStatus.Draft ? NewDraft() : InStatus(status);

        initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Large, T1);

        Assert.Equal(InitiativeDepth.Large, initiative.Depth);
    }

    [Theory]
    [InlineData(InitiativeStatus.Planning)]
    [InlineData(InitiativeStatus.ReadyToBuild)]
    public void Depth_change_is_rejected_in_planning_and_ready_to_build(InitiativeStatus status)
    {
        var initiative = InStatus(status);

        var error = Assert.Throws<DomainException>(
            () => initiative.SetDepth(DepthMode.Automatic, null, T1));

        Assert.Equal("El modo y la profundidad solo se pueden cambiar en Borrador o Aclarando.", error.Message);
        Assert.Equal(DepthMode.Manual, initiative.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, initiative.Depth);
        Assert.Equal(T0, initiative.UpdatedAt);
    }

    [Fact]
    public void Name_can_be_edited_outside_draft_without_touching_mode_or_depth()
    {
        var initiative = InStatus(InitiativeStatus.Planning);

        initiative.Rename("Nuevo nombre", "Notas", T1);

        Assert.Equal("Nuevo nombre", initiative.Name);
        Assert.Equal(InitiativeStatus.Planning, initiative.Status);
        Assert.Equal(DepthMode.Manual, initiative.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, initiative.Depth);
    }

    [Fact]
    public void Wizard_step_can_only_move_in_draft()
    {
        var draft = NewDraft();
        var clarifying = InStatus(InitiativeStatus.Clarifying);

        draft.MoveToStep(CreationStep.Review, T1);

        Assert.Equal(CreationStep.Review, draft.CreationStep);
        Assert.Throws<DomainException>(() => clarifying.MoveToStep(CreationStep.Details, T1));
    }

    [Fact]
    public void Complete_moves_a_manual_draft_with_a_level_to_clarifying()
    {
        var initiative = NewDraft();
        initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Small, T0);

        initiative.Complete(T1);

        Assert.Equal(InitiativeStatus.Clarifying, initiative.Status);
        Assert.Equal(T1, initiative.UpdatedAt);
    }

    [Fact]
    public void Complete_accepts_automatic_mode_and_leaves_depth_empty()
    {
        var initiative = NewDraft();
        initiative.SetDepth(DepthMode.Automatic, null, T0);

        initiative.Complete(T1);

        Assert.Equal(InitiativeStatus.Clarifying, initiative.Status);
        Assert.Null(initiative.Depth);
    }

    [Fact]
    public void Complete_without_a_mode_is_rejected_and_status_stays_draft()
    {
        var initiative = NewDraft();

        Assert.Throws<DomainException>(() => initiative.Complete(T1));
        Assert.Equal(InitiativeStatus.Draft, initiative.Status);
    }

    [Fact]
    public void Complete_in_manual_mode_without_a_level_is_rejected_and_status_stays_draft()
    {
        var initiative = NewDraft();
        initiative.SetDepth(DepthMode.Manual, null, T0);

        Assert.Throws<DomainException>(() => initiative.Complete(T1));
        Assert.Equal(InitiativeStatus.Draft, initiative.Status);
    }

    [Fact]
    public void Complete_outside_draft_is_rejected()
    {
        var initiative = InStatus(InitiativeStatus.Clarifying);

        Assert.Throws<DomainException>(() => initiative.Complete(T1));
        Assert.Equal(InitiativeStatus.Clarifying, initiative.Status);
    }

    [Fact]
    public void StartPlanning_moves_a_clarifying_initiative_with_depth_to_planning()
    {
        var initiative = InStatus(InitiativeStatus.Clarifying);

        initiative.StartPlanning(T1);

        Assert.Equal(InitiativeStatus.Planning, initiative.Status);
        Assert.Equal(DepthMode.Manual, initiative.DepthMode);
        Assert.Equal(InitiativeDepth.Standard, initiative.Depth);
        Assert.Equal(T1, initiative.UpdatedAt);
    }

    [Fact]
    public void StartPlanning_is_idempotent_and_does_not_refresh_updated_at()
    {
        var initiative = InStatus(InitiativeStatus.Clarifying);
        initiative.StartPlanning(T1);

        initiative.StartPlanning(T1.AddHours(1));

        Assert.Equal(InitiativeStatus.Planning, initiative.Status);
        Assert.Equal(T1, initiative.UpdatedAt);
    }

    [Theory]
    [InlineData(InitiativeStatus.Draft)]
    [InlineData(InitiativeStatus.ReadyToBuild)]
    public void StartPlanning_outside_clarifying_is_rejected_and_leaves_the_initiative_unchanged(InitiativeStatus status)
    {
        var initiative = status == InitiativeStatus.Draft ? NewDraft() : InStatus(status);
        var updatedAt = initiative.UpdatedAt;

        var error = Assert.Throws<DomainException>(() => initiative.StartPlanning(T1));

        Assert.Equal("La iniciativa debe estar en Aclarando para pasar a Planificando.", error.Message);
        Assert.Equal(status, initiative.Status);
        Assert.Equal(updatedAt, initiative.UpdatedAt);
    }

    [Fact]
    public void StartPlanning_without_a_depth_is_rejected_and_status_stays_clarifying()
    {
        var initiative = NewDraft();
        initiative.SetDepth(DepthMode.Automatic, null, T0);
        initiative.Complete(T0);

        var error = Assert.Throws<DomainException>(() => initiative.StartPlanning(T1));

        Assert.Equal("Elija un nivel de profundidad antes de pasar a Planificando.", error.Message);
        Assert.Equal(InitiativeStatus.Clarifying, initiative.Status);
        Assert.Equal(T0, initiative.UpdatedAt);
    }

    [Fact]
    public void StartPlanning_locks_mode_and_depth()
    {
        var initiative = InStatus(InitiativeStatus.Clarifying);
        initiative.StartPlanning(T1);

        Assert.Throws<DomainException>(() => initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Large, T1));
        Assert.Throws<DomainException>(() => initiative.SetDepth(DepthMode.Automatic, null, T1));
        Assert.Equal(InitiativeDepth.Standard, initiative.Depth);
    }

    [Fact]
    public void Every_mutator_refreshes_updated_at()
    {
        var initiative = NewDraft();

        initiative.Rename("Otro", null, T1);
        Assert.Equal(T1, initiative.UpdatedAt);

        var later = T1.AddMinutes(1);
        initiative.SetDepth(DepthMode.Manual, InitiativeDepth.Small, later);
        Assert.Equal(later, initiative.UpdatedAt);

        later = later.AddMinutes(1);
        initiative.MoveToStep(CreationStep.Depth, later);
        Assert.Equal(later, initiative.UpdatedAt);

        later = later.AddMinutes(1);
        initiative.Complete(later);
        Assert.Equal(later, initiative.UpdatedAt);

        later = later.AddMinutes(1);
        initiative.Delete(later);
        Assert.Equal(later, initiative.UpdatedAt);
    }

    [Fact]
    public void Delete_marks_the_record_and_is_idempotent()
    {
        var initiative = NewDraft();

        initiative.Delete(T1);
        initiative.Delete(T1.AddHours(1));

        Assert.Equal(T1, initiative.DeletedAt);
        Assert.Equal(T1, initiative.UpdatedAt);
    }
}
