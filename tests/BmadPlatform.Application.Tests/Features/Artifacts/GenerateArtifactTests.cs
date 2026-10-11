using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Tests.Features.Assistant;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class GenerateArtifactTests
{
    private readonly ArtifactTestContext context = new();

    [Fact]
    public async Task The_first_generation_stores_version_1_as_a_draft_for_the_current_user()
    {
        var id = await context.AtClosing(InitiativeDepth.Small, "Mi app");

        var view = await context.Generate(id, ArtifactType.Spec);

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((1, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Equal(AssistantTestContext.UserA, stored.OwnerId);
        Assert.Equal(context.Clock.GetUtcNow(), stored.CreatedAt);
        Assert.Equal(context.Clock.GetUtcNow(), stored.UpdatedAt);
        Assert.Equal(view.Content, stored.Content);
        Assert.True(view.Generated);
        Assert.False(view.CanGenerate);
        Assert.True(view.CanApprove);

        var request = Assert.Single(context.Drafter.Requests);
        Assert.Equal((ArtifactType.Spec, "Mi app", InitiativeDepth.Small), (request.Type, request.InitiativeName, request.Depth));
        Assert.Equal(6, request.Answers.Count);
    }

    [Fact]
    public async Task A_draft_initiative_is_rejected()
    {
        var id = await context.Initiatives.CreateDraft("Borrador");

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, ArtifactType.Spec));

        Assert.Equal("Termine de crear la iniciativa para generar documentos.", error.Message);
        AssertNothingStored();
    }

    [Theory]
    [InlineData(InitiativeDepth.Small, ArtifactType.Brief, "Este documento no corresponde al nivel de la iniciativa.")]
    [InlineData(InitiativeDepth.Standard, ArtifactType.Spec, "Este documento no corresponde al nivel de la iniciativa.")]
    public async Task A_type_outside_the_level_is_rejected(InitiativeDepth depth, ArtifactType type, string message)
    {
        var id = await context.AtClosing(depth);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, type));

        Assert.Equal(message, error.Message);
        AssertNothingStored();
    }

    [Fact]
    public async Task An_initiative_without_a_level_yet_is_rejected()
    {
        var id = await context.Assistant.CreateAutomatic("Automática");

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, ArtifactType.Spec));

        Assert.Equal(ArtifactTexts.NotForLevel, error.Message);
        AssertNothingStored();
    }

    [Fact]
    public async Task Documents_wait_for_the_topics_of_their_moment()
    {
        // Aclarar still has an uncovered topic.
        var partial = await context.Initiatives.CreateClarifying("Parcial");
        await context.Assistant.AnswerUntilDecision(partial, stopBeforeKey: AssistantScript.Keys.Users);
        // Aclarar is covered, Planificar is not.
        var clarified = await context.ClarifiedStandard("Aclarada");

        var brief = await Assert.ThrowsAsync<DomainException>(() => context.Generate(partial, ArtifactType.Brief));
        var prd = await Assert.ThrowsAsync<DomainException>(() => context.Generate(clarified, ArtifactType.Prd));

        Assert.Equal(ArtifactTexts.TopicsUncovered, brief.Message);
        Assert.Equal(ArtifactTexts.TopicsUncovered, prd.Message);
        AssertNothingStored();
    }

    [Fact]
    public async Task The_brief_can_be_generated_while_clarifying_and_leaves_the_status_and_the_script_alone()
    {
        var id = await context.ClarifiedStandard();
        var before = await context.Assistant.Current(id);

        var view = await context.Generate(id, ArtifactType.Brief);

        var after = await context.Assistant.Current(id);
        Assert.True(view.Generated);
        Assert.Equal(InitiativeStatus.Clarifying, await context.StatusOf(id));
        Assert.Equal(before.Journey.NextTopic!.Key, after.Journey.NextTopic!.Key);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Messages.Count, after.Messages.Count);
    }

    [Fact]
    public async Task Generating_twice_is_rejected_and_the_stored_document_is_unchanged()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        var content = context.StoredArtifact(id, ArtifactType.Spec).Content;

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, ArtifactType.Spec));

        Assert.Equal("El documento ya existe. Use Regenerar para reemplazarlo.", error.Message);
        Assert.Equal(content, context.StoredArtifact(id, ArtifactType.Spec).Content);
        Assert.Single(context.Drafter.Requests);
    }

    [Fact]
    public async Task Two_tabs_generating_together_store_one_document_and_the_slower_gets_the_conflict()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Artifacts.BeforeNextAdd = async () => await context.Generate(id, ArtifactType.Spec);

        var error = await Assert.ThrowsAsync<ConflictException>(() => context.Generate(id, ArtifactType.Spec));

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        var stored = Assert.Single(context.Artifacts.Stored);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task An_unavailable_drafter_reaches_the_caller_unchanged_and_stores_nothing()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Drafter.FailuresRemaining = 1;
        context.Drafter.Failure = new AssistantUnavailableException();

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Generate(id, ArtifactType.Spec));

        Assert.Equal(AssistantUnavailableException.DefaultMessage, error.Message);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        AssertNothingStored();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("TOO_LONG")]
    public async Task A_draft_that_breaks_the_content_rules_stores_nothing(string output)
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Drafter.Output = output == "TOO_LONG" ? new string('x', Artifact.ContentMaxLength + 1) : output;

        await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, ArtifactType.Spec));

        AssertNothingStored();
    }

    private void AssertNothingStored()
    {
        Assert.Empty(context.Artifacts.Stored);
        Assert.Equal(0, context.Artifacts.AddCount);
    }
}
