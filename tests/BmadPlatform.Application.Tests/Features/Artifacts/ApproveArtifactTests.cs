using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class ApproveArtifactTests
{
    private readonly ArtifactTestContext context = new();

    [Fact]
    public async Task Approving_keeps_the_version_and_works_even_with_format_warnings()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Prd);
        await context.Save(id, ArtifactType.Prd, 1, "# Sin secciones\nFR1 mal escrito");
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        var view = await context.Approve(id, ArtifactType.Prd, 2);

        var stored = context.StoredArtifact(id, ArtifactType.Prd);
        Assert.Equal((2, ArtifactStatus.Approved), (stored.Version, stored.Status));
        Assert.Equal(context.Clock.GetUtcNow(), stored.ApprovedAt);
        Assert.NotEmpty(view.Warnings);
    }

    [Fact]
    public async Task Small_is_ready_to_build_as_soon_as_the_spec_is_approved()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);

        var view = await context.Approve(id, ArtifactType.Spec, 1);

        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.False(view.CompletionPending);
    }

    [Theory]
    [InlineData(ArtifactType.Brief, ArtifactType.Prd)]
    [InlineData(ArtifactType.Prd, ArtifactType.Brief)]
    public async Task Standard_is_ready_to_build_only_after_the_second_approval_in_either_order(ArtifactType first, ArtifactType second)
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Brief);
        await context.Generate(id, ArtifactType.Prd);

        await context.Approve(id, first, 1);

        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));

        await context.Approve(id, second, 1);

        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
    }

    [Fact]
    public async Task A_partial_set_does_not_complete_while_clarifying_or_planning()
    {
        var clarifying = await context.ClarifiedStandard("Aclarando");
        await context.Generate(clarifying, ArtifactType.Brief);
        var planning = await context.AtClosing(InitiativeDepth.Standard, "Planificando");
        await context.Generate(planning, ArtifactType.Brief);

        await context.Approve(clarifying, ArtifactType.Brief, 1);
        await context.Approve(planning, ArtifactType.Brief, 1);

        Assert.Equal(InitiativeStatus.Clarifying, await context.StatusOf(clarifying));
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(planning));
        Assert.DoesNotContain(context.Sender.Sent, request => request is MarkReadyToBuildCommand);
    }

    [Fact]
    public async Task Large_never_completes_because_its_later_documents_cannot_exist()
    {
        var id = await context.AtClosing(InitiativeDepth.Large);
        await context.Generate(id, ArtifactType.Prd);

        await context.Approve(id, ArtifactType.Prd, 1);

        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }

    [Fact]
    public async Task Approving_again_at_the_same_version_succeeds_without_changes()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Approve(id, ArtifactType.Spec, 1);
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        var (updatedAt, saves) = (stored.UpdatedAt, context.Artifacts.SaveCount);
        context.Clock.Advance(TimeSpan.FromHours(1));

        var view = await context.Approve(id, ArtifactType.Spec, 1);

        var after = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((updatedAt, saves), (after.UpdatedAt, context.Artifacts.SaveCount));
        Assert.Equal(ArtifactStatus.Approved, view.Status);
        Assert.Single(context.Sender.Sent.OfType<MarkReadyToBuildCommand>());
    }

    [Fact]
    public async Task A_failed_ready_transition_keeps_the_approval_and_the_retry_completes()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        var failures = 1;
        context.Sender.BeforeHandle = request =>
        {
            if (request is MarkReadyToBuildCommand && failures-- > 0)
            {
                throw new InvalidOperationException("boom");
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Approve(id, ArtifactType.Spec, 1));

        Assert.Equal(ArtifactStatus.Approved, context.StoredArtifact(id, ArtifactType.Spec).Status);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));

        var view = await context.Approve(id, ArtifactType.Spec, 1);

        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.Equal(ArtifactStatus.Approved, view.Status);
        Assert.Equal(1, context.StoredArtifact(id, ArtifactType.Spec).Version);
    }

    [Fact]
    public async Task Approving_a_document_not_generated_is_rejected()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, ArtifactType.Prd, 1));

        Assert.Equal("Todavía no se ha generado este documento.", error.Message);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }

    [Fact]
    public async Task A_stale_approval_is_rejected_and_the_document_stays_a_draft()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, "# Editada");

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Approve(id, ArtifactType.Spec, 1));

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        Assert.Equal(ArtifactStatus.Draft, context.StoredArtifact(id, ArtifactType.Spec).Status);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }

    [Fact]
    public async Task An_edit_that_lands_between_the_check_and_the_approval_wins_and_the_approval_conflicts()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        context.Artifacts.BeforeNextSave = async () => await context.Save(id, ArtifactType.Spec, 1, "# Editada a la vez");

        await Assert.ThrowsAsync<ConflictException>(() => context.Approve(id, ArtifactType.Spec, 1));

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }
}
