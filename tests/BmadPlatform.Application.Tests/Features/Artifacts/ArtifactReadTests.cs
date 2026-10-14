using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Initiatives.MarkReadyToBuild;
using BmadPlatform.Application.Tests.Features.Assistant;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

/// <summary>The overview and single-document queries.</summary>
public sealed class ArtifactReadTests
{
    private readonly ArtifactTestContext context = new();

    [Fact]
    public async Task A_draft_initiative_lists_nothing_and_says_why()
    {
        var id = await context.Initiatives.CreateDraft("Borrador");

        var overview = await context.Overview(id);

        Assert.NotNull(overview);
        Assert.Empty(overview.Slots);
        Assert.Equal("Termine de crear la iniciativa para generar documentos.", overview.Notice);
    }

    [Fact]
    public async Task The_list_shows_the_state_of_each_required_type_and_no_others()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Brief);
        await context.Save(id, ArtifactType.Brief, 1, "# Brief editado");

        var overview = await context.Overview(id);

        Assert.NotNull(overview);
        Assert.Equal(["Brief", "PRD"], overview.Slots.Select(s => s.Label));
        var brief = overview.Slots[0];
        Assert.True(brief.Generated);
        Assert.Equal((ArtifactStatus.Draft, 2), (brief.Status, brief.Version));
        var prd = overview.Slots[1];
        Assert.False(prd.Generated);
        Assert.Equal(ArtifactAvailability.Available, prd.Availability);
        Assert.Null(prd.Reason);
        Assert.False(overview.CompletionPending);
    }

    [Fact]
    public async Task Large_lists_its_full_set_with_each_document_waiting_for_the_one_before()
    {
        var id = await context.AtClosing(InitiativeDepth.Large);

        var overview = await context.Overview(id);

        Assert.NotNull(overview);
        Assert.Equal(["PRD", "Arquitectura", "Épicas e historias"], overview.Slots.Select(s => s.Label));
        Assert.Equal(ArtifactAvailability.Available, overview.Slots[0].Availability);
        Assert.Equal((ArtifactType.Architecture, ArtifactAvailability.WaitingForPrd), (overview.Slots[1].Type, overview.Slots[1].Availability));
        Assert.Equal(ArtifactTexts.ReasonFor(ArtifactAvailability.WaitingForPrd), overview.Slots[1].Reason);
        Assert.Equal((ArtifactType.EpicsAndStories, ArtifactAvailability.WaitingForPrd), (overview.Slots[2].Type, overview.Slots[2].Availability));
        Assert.Equal(ArtifactTexts.ReasonFor(ArtifactAvailability.WaitingForPrd), overview.Slots[2].Reason);
    }

    [Fact]
    public async Task A_type_waiting_for_the_assistant_carries_its_reason()
    {
        var id = await context.ClarifiedStandard();

        var overview = await context.Overview(id);

        Assert.NotNull(overview);
        Assert.Equal(ArtifactAvailability.Available, overview.Slots[0].Availability);
        Assert.Equal(ArtifactAvailability.WaitingForClosing, overview.Slots[1].Availability);
        Assert.Equal(ArtifactTexts.TopicsUncovered, overview.Slots[1].Reason);
    }

    [Fact]
    public async Task An_orphan_document_is_hidden_but_kept()
    {
        var id = await context.ClarifiedStandard();
        await context.Generate(id, ArtifactType.Brief);

        await context.Initiatives.SetInitiativeDepth(id, InitiativeDepth.Small);
        var overview = await context.Overview(id);

        Assert.NotNull(overview);
        Assert.Equal(["Especificación"], overview.Slots.Select(s => s.Label));
        Assert.Equal(ArtifactType.Brief, context.Artifacts.Stored.Single().Type);
    }

    [Fact]
    public async Task Reading_never_generates_or_changes_anything()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);

        await context.Overview(id);
        var notGenerated = await context.Get(id, ArtifactType.Spec);
        await context.Overview(id);

        Assert.NotNull(notGenerated);
        Assert.False(notGenerated.Generated);
        Assert.True(notGenerated.CanGenerate);
        Assert.Equal(string.Empty, notGenerated.Content);
        Assert.Equal(0, context.Artifacts.AddCount);
        Assert.Empty(context.Drafter.Requests);

        await context.Generate(id, ArtifactType.Spec);
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        var (version, status, updatedAt, saves) = (stored.Version, stored.Status, stored.UpdatedAt, context.Artifacts.SaveCount);
        context.Clock.Advance(TimeSpan.FromHours(1));

        var first = await context.Get(id, ArtifactType.Spec);
        var second = await context.Get(id, ArtifactType.Spec);

        Assert.Equal(first!.Content, second!.Content);
        var after = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((version, status, updatedAt, saves), (after.Version, after.Status, after.UpdatedAt, context.Artifacts.SaveCount));
    }

    [Fact]
    public async Task Export_hands_the_stored_document_of_the_owner_to_the_exporter()
    {
        var id = await context.AtClosing(InitiativeDepth.Small, "Portal");
        await context.Generate(id, ArtifactType.Spec);
        var stored = context.StoredArtifact(id, ArtifactType.Spec);

        var export = await context.Export(id, ArtifactType.Spec);

        Assert.NotNull(export);
        Assert.Equal("Spec-v1.md", export.FileName);
        Assert.Equal($"Portal|Draft|{stored.Content}", export.Content);
    }

    [Fact]
    public async Task Export_is_null_for_a_document_that_was_not_generated_and_for_foreign_or_deleted_initiatives()
    {
        var notGenerated = await context.AtClosing(InitiativeDepth.Small, "Sin documento");
        var foreign = await context.AtClosing(InitiativeDepth.Small, "Ajena");
        await context.Generate(foreign, ArtifactType.Spec);
        var deleted = await context.AtClosing(InitiativeDepth.Small, "Borrada");
        await context.Generate(deleted, ArtifactType.Spec);
        await context.Initiatives.Delete(deleted);

        Assert.Null(await context.Export(notGenerated, ArtifactType.Spec));
        Assert.Null(await context.Export(deleted, ArtifactType.Spec));
        Assert.Null(await context.Export(Guid.NewGuid(), ArtifactType.Spec));

        context.User.UserId = AssistantTestContext.UserB;
        Assert.Null(await context.Export(foreign, ArtifactType.Spec));
    }

    [Fact]
    public async Task The_list_reports_a_pending_completion_only_on_a_planning_initiative_with_every_document_approved()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        context.Sender.BeforeHandle = request =>
        {
            if (request is MarkReadyToBuildCommand)
            {
                throw new InvalidOperationException("boom");
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Approve(id, ArtifactType.Spec, 1));

        var overview = await context.Overview(id);
        var view = await context.Get(id, ArtifactType.Spec);

        Assert.True(overview!.CompletionPending);
        Assert.True(view!.CompletionPending);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }
}
