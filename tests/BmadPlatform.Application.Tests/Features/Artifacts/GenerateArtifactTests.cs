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
        Assert.Null(request.BriefMarkdown);
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
    [InlineData(AssistantUnavailableReason.Quota)]
    [InlineData(AssistantUnavailableReason.Timeout)]
    [InlineData(AssistantUnavailableReason.Blocked)]
    [InlineData(AssistantUnavailableReason.Malformed)]
    public async Task A_drafter_failure_with_a_reason_stores_nothing_and_returns_the_document_message(AssistantUnavailableReason reason)
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Drafter.FailuresRemaining = 1;
        context.Drafter.Failure = new AssistantUnavailableException(ArtifactTexts.DraftUnavailableFor(reason), reason);

        var error = await Assert.ThrowsAsync<AssistantUnavailableException>(() => context.Generate(id, ArtifactType.Spec));

        Assert.Equal((reason, ArtifactTexts.DraftUnavailableFor(reason)), (error.Reason, error.Message));
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

    // Brief states: approved, left as a draft, never generated.
    [Theory]
    [InlineData(InitiativeDepth.Standard, "approved", true)]
    [InlineData(InitiativeDepth.Standard, "draft", false)]
    [InlineData(InitiativeDepth.Standard, "none", false)]
    [InlineData(InitiativeDepth.Large, "none", false)]
    public async Task The_prd_request_carries_the_brief_only_when_it_is_standard_and_the_brief_is_approved(InitiativeDepth depth, string brief, bool carried)
    {
        var id = await context.AtClosing(depth);

        if (brief != "none")
        {
            await context.Generate(id, ArtifactType.Brief);
        }

        if (brief == "approved")
        {
            await context.Approve(id, ArtifactType.Brief, 1);
        }

        await context.Generate(id, ArtifactType.Prd);

        var request = context.Drafter.Requests[^1];
        Assert.Equal(ArtifactType.Prd, request.Type);
        Assert.Equal(carried ? context.StoredArtifact(id, ArtifactType.Brief).Content : null, request.BriefMarkdown);
        Assert.True(context.StoredArtifact(id, ArtifactType.Prd).Version == 1);
    }

    [Fact]
    public async Task Another_users_approved_brief_is_never_read()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        var foreign = Artifact.Create(id, "someone-else", ArtifactType.Brief, "# Brief ajeno", context.Clock.GetUtcNow());
        foreign.Approve(context.Clock.GetUtcNow());
        await context.Artifacts.AddAsync(foreign, default);

        await context.Generate(id, ArtifactType.Prd);

        Assert.Null(context.Drafter.Requests[^1].BriefMarkdown);
    }

    [Fact]
    public async Task The_architecture_waits_for_an_approved_prd_then_is_drafted_from_its_text_alone()
    {
        var id = await context.AtClosing(InitiativeDepth.Large);
        await context.Generate(id, ArtifactType.Prd);

        var waiting = await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, ArtifactType.Architecture));

        Assert.Equal(ArtifactTexts.ReasonFor(ArtifactAvailability.WaitingForPrd), waiting.Message);

        await context.Approve(id, ArtifactType.Prd, 1);
        await context.Generate(id, ArtifactType.Architecture);

        var request = context.Drafter.Requests[^1];
        Assert.Equal(ArtifactType.Architecture, request.Type);
        Assert.Equal(context.StoredArtifact(id, ArtifactType.Prd).Content, request.PrdMarkdown);
        Assert.Null(request.ArchitectureMarkdown);
        Assert.Empty(request.Answers);
    }

    [Theory]
    [InlineData(ArtifactType.Architecture, true, false, true, false, false)]
    [InlineData(ArtifactType.Architecture, false, false, false, false, false)]
    [InlineData(ArtifactType.EpicsAndStories, true, true, true, true, false)]
    [InlineData(ArtifactType.EpicsAndStories, true, false, true, false, false)]
    [InlineData(ArtifactType.EpicsAndStories, false, true, false, true, false)]
    [InlineData(ArtifactType.Prd, true, true, false, false, true)]
    public async Task Upstream_text_travels_only_while_that_document_is_approved_and_the_chain_carries_no_answers(
        ArtifactType type,
        bool prdApproved,
        bool architectureApproved,
        bool expectPrd,
        bool expectArchitecture,
        bool expectAnswers)
    {
        var id = await context.AtClosing(InitiativeDepth.Large);
        await Store(id, ArtifactType.Prd, "# PRD", prdApproved);
        await Store(id, ArtifactType.Architecture, "# Arquitectura", architectureApproved);

        var request = await ArtifactGuards.DraftRequestAsync(
            context.Artifacts,
            await context.Assistant.Details(id),
            AssistantTestContext.UserA,
            type,
            (await context.Assistant.Answers(id))!,
            default);

        Assert.Equal(expectPrd ? "# PRD" : null, request.PrdMarkdown);
        Assert.Equal(expectArchitecture ? "# Arquitectura" : null, request.ArchitectureMarkdown);
        Assert.Equal(expectAnswers, request.Answers.Count > 0);
    }

    private async Task Store(Guid id, ArtifactType type, string content, bool approved)
    {
        var artifact = Artifact.Create(id, AssistantTestContext.UserA, type, content, context.Clock.GetUtcNow());

        if (approved)
        {
            artifact.Approve(context.Clock.GetUtcNow());
        }

        await context.Artifacts.AddAsync(artifact, default);
    }

    private void AssertNothingStored()
    {
        Assert.Empty(context.Artifacts.Stored);
        Assert.Equal(0, context.Artifacts.AddCount);
    }
}
