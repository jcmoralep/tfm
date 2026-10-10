using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class RegenerateArtifactTests
{
    private readonly ArtifactTestContext context = new();

    private async Task<Guid> ReadySpec()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Approve(id, ArtifactType.Spec, 1);

        return id;
    }

    [Fact]
    public async Task Regenerating_replaces_manual_edits_with_a_fresh_draft_numbered_from_one()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, "- **FR-4**: a mano\n- **FR-7**: otra");

        var view = await context.Regenerate(id, ArtifactType.Spec, 2);

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((3, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Equal(ScriptedArtifactDrafter.Draft(context.Drafter.Requests[^1]), stored.Content);
        Assert.DoesNotContain("FR-7", stored.Content, StringComparison.Ordinal);
        Assert.Equal(3, view.Version);
        Assert.Equal(2, context.Drafter.Requests.Count);
    }

    [Fact]
    public async Task Without_confirmation_nothing_happens()
    {
        var id = await ReadySpec();

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Spec, 1, confirmed: false));

        Assert.Equal("Confirme que desea reemplazar el documento.", error.Message);
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((1, ArtifactStatus.Approved), (stored.Version, stored.Status));
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.Single(context.Drafter.Requests);
    }

    [Fact]
    public async Task Regenerating_an_approved_document_of_a_ready_initiative_returns_both_to_draft_and_planning()
    {
        var id = await ReadySpec();
        InitiativeStatus? statusAtSave = null;
        context.Artifacts.BeforeNextSave = async () => statusAtSave = await context.StatusOf(id);

        await context.Regenerate(id, ArtifactType.Spec, 1);

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Null(stored.ApprovedAt);
        Assert.Equal(InitiativeStatus.Planning, statusAtSave);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }

    [Fact]
    public async Task A_draft_that_fails_or_breaks_the_rules_leaves_the_ready_initiative_untouched()
    {
        var id = await ReadySpec();
        context.Drafter.FailuresRemaining = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Regenerate(id, ArtifactType.Spec, 1));
        context.Drafter.Output = "   ";
        await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Spec, 1));

        Assert.Equal(ArtifactStatus.Approved, context.StoredArtifact(id, ArtifactType.Spec).Status);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task Regenerating_a_document_not_generated_is_rejected()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Brief, 1));

        Assert.Equal("Todavía no se ha generado este documento.", error.Message);
        Assert.Empty(context.Artifacts.Stored);
    }

    [Fact]
    public async Task A_stale_version_is_rejected_before_the_drafter_runs()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, "# Editada");

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Regenerate(id, ArtifactType.Spec, 1));

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        Assert.Equal("# Editada", context.StoredArtifact(id, ArtifactType.Spec).Content);
        Assert.Single(context.Drafter.Requests);
    }

    [Fact]
    public async Task A_document_whose_topics_are_no_longer_covered_cannot_be_regenerated()
    {
        var id = await context.ClarifiedStandard();
        await context.Generate(id, ArtifactType.Brief);
        var view = await context.Assistant.Current(id);
        view = await context.Assistant.Undo(id, view.Version);
        await context.Assistant.Undo(id, view.Version);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Brief, 1));

        Assert.Equal(ArtifactTexts.TopicsUncovered, error.Message);
        Assert.Equal(1, context.StoredArtifact(id, ArtifactType.Brief).Version);
    }
}
