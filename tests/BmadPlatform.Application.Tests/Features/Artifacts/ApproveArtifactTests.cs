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

    private Task<Guid> LargeApprovedThrough(ArtifactType last) => context.LargeApprovedThrough(last);

    [Theory]
    [InlineData(ArtifactType.Prd, InitiativeStatus.Planning)]
    [InlineData(ArtifactType.Architecture, InitiativeStatus.Planning)]
    [InlineData(ArtifactType.EpicsAndStories, InitiativeStatus.ReadyToBuild)]
    public async Task Large_is_ready_to_build_only_when_the_prd_the_architecture_and_the_epics_are_approved(ArtifactType last, InitiativeStatus expected)
    {
        var id = await LargeApprovedThrough(last);

        Assert.Equal(expected, await context.StatusOf(id));
    }

    [Theory]
    [InlineData(ArtifactType.Prd)]
    [InlineData(ArtifactType.Architecture)]
    [InlineData(ArtifactType.EpicsAndStories)]
    public async Task Editing_any_of_the_three_documents_reopens_a_ready_large_initiative(ArtifactType edited)
    {
        var id = await LargeApprovedThrough(ArtifactType.EpicsAndStories);

        await context.Save(id, edited, 1, "# Editado\n\n- **FR-1**: cambiado");

        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.Equal(ArtifactStatus.Draft, context.StoredArtifact(id, edited).Status);
    }

    [Fact]
    public async Task Approving_the_prd_again_after_an_edit_completes_with_the_dependents_untouched_even_if_stale()
    {
        var id = await LargeApprovedThrough(ArtifactType.EpicsAndStories);
        await context.Save(id, ArtifactType.Prd, 1, "# Editado\n\n- **FR-1**: cambiado");
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        var view = await context.Approve(id, ArtifactType.Prd, 2);

        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.All(
            new[] { ArtifactType.Architecture, ArtifactType.EpicsAndStories },
            type => Assert.Equal((1, ArtifactStatus.Approved), (context.StoredArtifact(id, type).Version, context.StoredArtifact(id, type).Status)));
        Assert.Equal(ArtifactStatus.Approved, view.Status);
    }

    [Fact]
    public async Task Regenerating_the_prd_leaves_the_approved_dependents_alone()
    {
        var id = await LargeApprovedThrough(ArtifactType.EpicsAndStories);

        await context.Regenerate(id, ArtifactType.Prd, 1);

        Assert.Equal((2, ArtifactStatus.Draft), (context.StoredArtifact(id, ArtifactType.Prd).Version, context.StoredArtifact(id, ArtifactType.Prd).Status));
        Assert.Equal(ArtifactStatus.Approved, context.StoredArtifact(id, ArtifactType.Architecture).Status);
        Assert.Equal(ArtifactStatus.Approved, context.StoredArtifact(id, ArtifactType.EpicsAndStories).Status);
    }

    // Proves the handler passes the stored documents to the availability rule: without them the approval would go through.
    [Theory]
    [InlineData(ArtifactType.Architecture, ArtifactType.Prd, ArtifactTexts.WaitingForPrd)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactType.Prd, ArtifactTexts.WaitingForPrd)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactType.Architecture, ArtifactTexts.WaitingForArchitecture)]
    public async Task Approving_a_dependent_while_its_upstream_is_a_draft_is_rejected(ArtifactType dependent, ArtifactType upstream, string message)
    {
        var id = await LargeApprovedThrough(ArtifactType.EpicsAndStories);
        await context.Save(id, upstream, 1, "# Editado\n\n- **FR-1**: cambiado");
        await context.Save(id, dependent, 1, "# Editado\n\n- **FR-1**: cambiado");
        var sent = context.Sender.Sent.Count;

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, dependent, 2));

        Assert.Equal(message, error.Message);
        Assert.Equal(ArtifactStatus.Draft, context.StoredArtifact(id, dependent).Status);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent.Skip(sent), request => request is MarkReadyToBuildCommand);
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

    [Fact]
    public async Task Undoing_a_plan_answer_takes_the_approval_back_until_the_topic_is_answered_again()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Prd);
        var closing = await context.Assistant.Current(id);
        await context.Assistant.Undo(id, closing.Version);

        var view = await context.Get(id, ArtifactType.Prd);
        var error = await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, ArtifactType.Prd, 1));

        Assert.False(view!.CanApprove);
        Assert.Equal(ArtifactTexts.TopicsUncovered, error.Message);
        Assert.Equal(ArtifactStatus.Draft, context.StoredArtifact(id, ArtifactType.Prd).Status);

        await context.Assistant.Answer(id, "Primero consultar");

        Assert.Equal(ArtifactStatus.Approved, (await context.Approve(id, ArtifactType.Prd, 1)).Status);
    }

    [Fact]
    public async Task The_retry_after_a_failed_transition_waits_while_a_topic_is_uncovered()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);
        await context.Generate(id, ArtifactType.Brief);
        await context.Generate(id, ArtifactType.Prd);
        await context.Approve(id, ArtifactType.Brief, 1);
        context.Sender.BeforeHandle = request =>
        {
            if (request is MarkReadyToBuildCommand)
            {
                throw new InvalidOperationException("boom");
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Approve(id, ArtifactType.Prd, 1));
        context.Sender.BeforeHandle = null;
        Assert.True((await context.Get(id, ArtifactType.Prd))!.CompletionPending);

        var closing = await context.Assistant.Current(id);
        await context.Assistant.Undo(id, closing.Version);

        Assert.False((await context.Get(id, ArtifactType.Prd))!.CompletionPending);
        await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, ArtifactType.Prd, 1));
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
    }

    private const string Assigned = " (Responsable: TI; Revisar: antes de la arquitectura)";

    private static string WithQuestions(params string[] lines) =>
        "# Documento\n\n## Preguntas abiertas\n\n" + string.Join('\n', lines.Select((line, i) => $"- **Q-{i + 1}**: {line}")) + "\n";

    private async Task<Guid> SpecWith(string content)
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await context.Save(id, ArtifactType.Spec, 1, content);

        return id;
    }

    [Theory]
    [InlineData("Q-1, Q-3", "uno|dos" + Assigned + "|tres")]
    [InlineData("Q-1", "uno (Responsable: TI; Revisar: por definir)")]
    [InlineData("Q-2", "uno" + Assigned + "|dos (Responsable: por asignar; Revisar: algún día)")]
    [InlineData(null, "uno" + Assigned + "|dos" + Assigned)]
    [InlineData(null, "")]
    public async Task Approving_needs_every_open_question_assigned(string? blocked, string questions)
    {
        var id = await SpecWith(WithQuestions(questions.Length == 0 ? [] : questions.Split('|')));

        if (blocked is null)
        {
            Assert.Equal(ArtifactStatus.Approved, (await context.Approve(id, ArtifactType.Spec, 2)).Status);
            Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));

            return;
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, ArtifactType.Spec, 2));

        Assert.Equal(ArtifactTexts.UnassignedQuestions(blocked.Split(", ")), error.Message);
        Assert.Equal((2, ArtifactStatus.Draft), (context.StoredArtifact(id, ArtifactType.Spec).Version, context.StoredArtifact(id, ArtifactType.Spec).Status));
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is MarkReadyToBuildCommand);
    }

    [Fact]
    public async Task Format_warnings_do_not_trigger_the_gate_and_a_conflict_is_not_masked_by_it()
    {
        var id = await SpecWith(WithQuestions("uno" + Assigned) + "FR1 mal escrito\n");

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => context.Approve(id, ArtifactType.Spec, 1));
        var view = await context.Approve(id, ArtifactType.Spec, 2);

        Assert.Equal(ArtifactTexts.Conflict, conflict.Message);
        Assert.NotEmpty(view.Warnings);
        Assert.Equal(ArtifactStatus.Approved, view.Status);
    }

    [Fact]
    public async Task Documents_approved_before_the_gate_keep_working_and_an_edit_brings_the_gate_back()
    {
        var id = await SpecWith(WithQuestions("uno"));
        context.StoredArtifact(id, ArtifactType.Spec).Approve(context.Clock.GetUtcNow());
        var saves = context.Artifacts.SaveCount;

        // The retry of a failed ready transition completes the initiative even though the question is unassigned.
        var view = await context.Approve(id, ArtifactType.Spec, 2);

        Assert.Equal(saves, context.Artifacts.SaveCount);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.Equal(ArtifactStatus.Approved, view.Status);

        // Approving again is idempotent; an edit returns the document to Draft and the gate applies at its next approval.
        await context.Approve(id, ArtifactType.Spec, 2);
        await context.Save(id, ArtifactType.Spec, 2, WithQuestions("uno cambiado"));

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Approve(id, ArtifactType.Spec, 3));
        Assert.Equal(ArtifactTexts.UnassignedQuestions(["Q-1"]), error.Message);
    }
}
