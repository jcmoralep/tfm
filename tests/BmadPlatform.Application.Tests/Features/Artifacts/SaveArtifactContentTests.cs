using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class SaveArtifactContentTests
{
    private readonly ArtifactTestContext context = new();

    /// <summary>A Small initiative whose Spec is generated and approved, so the initiative is Ready to build.</summary>
    private async Task<Guid> ReadySpec()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Approve(id, ArtifactType.Spec, 1);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));

        return id;
    }

    [Fact]
    public async Task An_edit_raises_the_version_and_stores_the_normalized_text()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        var view = await context.Save(id, ArtifactType.Spec, 1, "\r\n\r\n# Editado\r\nlinea\r\n\r\n");

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Equal("# Editado\nlinea", stored.Content);
        Assert.Equal(context.Clock.GetUtcNow(), stored.UpdatedAt);
        Assert.Equal(2, view.Version);
    }

    [Fact]
    public async Task Saving_the_same_text_changes_nothing_and_does_not_reopen_a_ready_initiative()
    {
        var id = await ReadySpec();
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        var (updatedAt, saves) = (stored.UpdatedAt, context.Artifacts.SaveCount);
        context.Clock.Advance(TimeSpan.FromHours(1));

        var view = await context.Save(id, ArtifactType.Spec, 1, stored.Content.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n");

        var after = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((1, ArtifactStatus.Approved, updatedAt, saves), (after.Version, after.Status, after.UpdatedAt, context.Artifacts.SaveCount));
        Assert.Equal(1, view.Version);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task Editing_an_approved_document_returns_it_to_draft_and_the_initiative_stays_planning()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Brief);
        await context.Approve(id, ArtifactType.Brief, 1);

        await context.Save(id, ArtifactType.Brief, 1, "# Brief cambiado");

        var stored = context.StoredArtifact(id, ArtifactType.Brief);
        Assert.Equal((2, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Null(stored.ApprovedAt);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task Editing_after_the_initiative_is_ready_reopens_planning_before_the_document_is_saved()
    {
        var id = await ReadySpec();
        InitiativeStatus? statusAtSave = null;
        context.Artifacts.BeforeNextSave = async () => statusAtSave = await context.StatusOf(id);

        var view = await context.Save(id, ArtifactType.Spec, 1, "# Spec cambiada");

        Assert.Equal(InitiativeStatus.Planning, statusAtSave);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.Equal((2, ArtifactStatus.Draft), (view.Version, view.Status));
    }

    [Fact]
    public async Task A_failed_save_after_the_reopen_leaves_planning_with_the_approved_document_and_a_repeated_approval_completes()
    {
        var id = await ReadySpec();
        context.Artifacts.BeforeNextSave = () => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Save(id, ArtifactType.Spec, 1, "# Spec cambiada"));

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((1, ArtifactStatus.Approved), (stored.Version, stored.Status));
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.True((await context.Get(id, ArtifactType.Spec))!.CompletionPending);

        var view = await context.Approve(id, ArtifactType.Spec, 1);

        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.False(view.CompletionPending);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("TOO_LONG")]
    public async Task Content_that_breaks_the_rules_changes_nothing_and_does_not_reopen_the_initiative(string content)
    {
        var id = await ReadySpec();
        var text = content == "TOO_LONG" ? new string('x', Artifact.ContentMaxLength + 1) : content;

        await Assert.ThrowsAsync<DomainException>(() => context.Save(id, ArtifactType.Spec, 1, text));

        Assert.Equal(ArtifactStatus.Approved, context.StoredArtifact(id, ArtifactType.Spec).Status);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
    }

    [Fact]
    public async Task A_brief_edit_while_clarifying_leaves_the_status()
    {
        var id = await context.ClarifiedStandard();
        await context.Generate(id, ArtifactType.Brief);

        var view = await context.Save(id, ArtifactType.Brief, 1, "# Brief a mano");

        Assert.Equal(2, view.Version);
        Assert.Equal(InitiativeStatus.Clarifying, await context.StatusOf(id));
    }

    [Fact]
    public async Task Editing_a_document_not_generated_is_rejected()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Save(id, ArtifactType.Prd, 1, "# PRD"));

        Assert.Equal("Todavía no se ha generado este documento.", error.Message);
        Assert.Empty(context.Artifacts.Stored);
    }

    [Fact]
    public async Task A_stale_save_is_rejected_and_the_first_save_stays()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, "# Primera pestaña");

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Save(id, ArtifactType.Spec, 1, "# Segunda pestaña"));

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        Assert.Equal("# Primera pestaña", context.StoredArtifact(id, ArtifactType.Spec).Content);
    }

    [Fact]
    public async Task Of_two_simultaneous_saves_exactly_one_succeeds()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        context.Artifacts.BeforeNextSave = async () => await context.Save(id, ArtifactType.Spec, 1, "# Ganadora");

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Save(id, ArtifactType.Spec, 1, "# Perdedora"));

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, "# Ganadora"), (stored.Version, stored.Content));
    }

    [Fact]
    public async Task Format_warnings_do_not_block_the_save_and_the_text_is_kept_as_typed()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        const string Broken = "# Sin secciones\nFR1 mal escrito\n- **FR-2**: uno\n- **FR-2**: repetido";

        var view = await context.Save(id, ArtifactType.Spec, 1, Broken);

        Assert.Equal(Broken, context.StoredArtifact(id, ArtifactType.Spec).Content);
        Assert.True(view.Warnings.Count >= 3);
    }
}
