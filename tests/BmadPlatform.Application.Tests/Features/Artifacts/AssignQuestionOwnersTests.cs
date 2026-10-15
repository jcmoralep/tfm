using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Initiatives.ReopenPlanning;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Common;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class AssignQuestionOwnersTests
{
    private const string Default = " (Responsable: por asignar; Revisar: por definir)";
    private const string Questions = "# Spec\n\n- **FR-1**: algo\n\n## Preguntas abiertas\n\n- **Q-1**: uno" + Default + "\n- **Q-2**: dos" + Default + "\n- **Q-3**: tres" + Default;

    private static readonly QuestionAssignment[] All =
        [new("Q-1", "Legal", "mañana"), new("Q-2", "TI", "luego"), new("Q-3", "Finanzas", "antes")];

    private readonly ArtifactTestContext context = new();

    private async Task<Guid> WithDocument(InitiativeDepth depth, ArtifactType type, string content = Questions)
    {
        var id = await context.AtClosing(depth);
        context.Drafter.Output = content;
        await context.Generate(id, type);

        return id;
    }

    /// <summary>A Small initiative whose Spec has all its questions assigned and is approved: Ready to build, Spec v2.</summary>
    private async Task<Guid> ReadySpec(string content = Questions)
    {
        var id = await WithDocument(InitiativeDepth.Small, ArtifactType.Spec, content);
        await context.Assign(id, ArtifactType.Spec, 1, All);
        await context.Approve(id, ArtifactType.Spec, 2);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));

        return id;
    }

    [Fact]
    public async Task Only_the_listed_question_is_rewritten_and_the_document_becomes_draft_with_one_more_version()
    {
        var id = await WithDocument(InitiativeDepth.Small, ArtifactType.Spec);
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        var view = await context.Assign(id, ArtifactType.Spec, 1, new QuestionAssignment("Q-1", "Legal", "antes de la arquitectura"));

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal(Questions.Replace("uno" + Default, "uno (Responsable: Legal; Revisar: antes de la arquitectura)", StringComparison.Ordinal), stored.Content);
        Assert.Equal((2, ArtifactStatus.Draft, context.Clock.GetUtcNow()), (stored.Version, stored.Status, stored.UpdatedAt));
        Assert.Equal(["Q-2", "Q-3"], view.UnassignedQuestions);
    }

    [Fact]
    public async Task Pre_assigned_development_questions_of_an_architecture_can_be_reassigned()
    {
        var id = await context.LargeApprovedThrough(ArtifactType.Prd);
        context.Drafter.Output = "# Arquitectura\n\n## Preguntas para desarrollo\n\n- **Q-1**: ¿Qué tecnología usará la solución?" + OpenQuestions.DevelopmentSuffix;
        await context.Generate(id, ArtifactType.Architecture);

        var view = await context.Assign(id, ArtifactType.Architecture, 1, new QuestionAssignment("Q-1", "TI", "esta semana"));

        var stored = context.StoredArtifact(id, ArtifactType.Architecture);
        Assert.EndsWith("(Responsable: TI; Revisar: esta semana)", stored.Content, StringComparison.Ordinal);
        Assert.Equal((2, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Empty(view.UnassignedQuestions);
    }

    [Fact]
    public async Task Several_questions_are_stored_in_one_save()
    {
        var id = await WithDocument(InitiativeDepth.Small, ArtifactType.Spec);

        var view = await context.Assign(id, ArtifactType.Spec, 1, All);

        Assert.Equal(2, view.Version);
        Assert.Empty(view.UnassignedQuestions);
        Assert.True(view.CanApprove);
    }

    [Fact]
    public async Task A_blank_field_writes_the_default_and_the_question_stays_unassigned()
    {
        var id = await WithDocument(InitiativeDepth.Small, ArtifactType.Spec);

        var view = await context.Assign(id, ArtifactType.Spec, 1, new QuestionAssignment("Q-1", "TI", "  "));

        Assert.Contains("uno (Responsable: TI; Revisar: por definir)", view.Content, StringComparison.Ordinal);
        Assert.Contains("Q-1", view.UnassignedQuestions);
    }

    [Fact]
    public async Task The_same_values_again_change_nothing_and_do_not_reopen_a_ready_initiative()
    {
        var id = await ReadySpec();
        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        var (updatedAt, saves) = (stored.UpdatedAt, context.Artifacts.SaveCount);
        context.Clock.Advance(TimeSpan.FromHours(1));

        var view = await context.Assign(id, ArtifactType.Spec, 2, All);

        var after = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, ArtifactStatus.Approved, updatedAt, saves), (after.Version, after.Status, after.UpdatedAt, context.Artifacts.SaveCount));
        Assert.Equal(2, view.Version);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task Assigning_on_an_approved_document_returns_it_to_draft_and_the_initiative_stays_planning()
    {
        var id = await WithDocument(InitiativeDepth.Standard, ArtifactType.Brief);
        await context.Assign(id, ArtifactType.Brief, 1, All);
        await context.Approve(id, ArtifactType.Brief, 2);

        await context.Assign(id, ArtifactType.Brief, 2, new QuestionAssignment("Q-1", "TI", "otra vez"));

        var stored = context.StoredArtifact(id, ArtifactType.Brief);
        Assert.Equal((3, ArtifactStatus.Draft), (stored.Version, stored.Status));
        Assert.Null(stored.ApprovedAt);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task A_ready_initiative_is_reopened_before_the_document_is_saved()
    {
        var id = await ReadySpec();
        InitiativeStatus? statusAtSave = null;
        context.Artifacts.BeforeNextSave = async () => statusAtSave = await context.StatusOf(id);

        var view = await context.Assign(id, ArtifactType.Spec, 2, new QuestionAssignment("Q-1", "TI", "otra vez"));

        Assert.Equal(InitiativeStatus.Planning, statusAtSave);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.Equal((3, ArtifactStatus.Draft), (view.Version, view.Status));
    }

    [Fact]
    public async Task A_failed_save_after_the_reopen_leaves_planning_with_the_approved_document_and_a_repeated_approval_completes()
    {
        var id = await ReadySpec();
        context.Artifacts.BeforeNextSave = () => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Assign(id, ArtifactType.Spec, 2, new QuestionAssignment("Q-1", "TI", "otra vez")));

        var stored = context.StoredArtifact(id, ArtifactType.Spec);
        Assert.Equal((2, ArtifactStatus.Approved), (stored.Version, stored.Status));
        Assert.Contains("Responsable: Legal", stored.Content, StringComparison.Ordinal);
        Assert.Equal(InitiativeStatus.Planning, await context.StatusOf(id));
        Assert.True((await context.Get(id, ArtifactType.Spec))!.CompletionPending);

        await context.Approve(id, ArtifactType.Spec, 2);

        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));
    }

    [Fact]
    public async Task An_unknown_id_is_rejected_before_the_initiative_is_reopened_and_nothing_is_stored()
    {
        var id = await ReadySpec();
        var saves = context.Artifacts.SaveCount;

        var error = await Assert.ThrowsAsync<DomainException>(
            () => context.Assign(id, ArtifactType.Spec, 2, new QuestionAssignment("Q-1", "TI", "x"), new QuestionAssignment("Q-9", "TI", "x")));

        Assert.Equal("La pregunta Q-9 no existe en este documento.", error.Message);
        Assert.Equal((saves, 2, InitiativeStatus.ReadyToBuild), (context.Artifacts.SaveCount, context.StoredArtifact(id, ArtifactType.Spec).Version, await context.StatusOf(id)));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task A_result_over_the_content_cap_is_rejected_before_the_initiative_is_reopened()
    {
        const string Tail = "\n- **Q-1**: t (Responsable: TI; Revisar: x)";
        var filler = new string('x', Artifact.ContentMaxLength - "# S\n".Length - Tail.Length);
        var id = await WithDocument(InitiativeDepth.Small, ArtifactType.Spec, "# S\n" + filler + Tail);
        await context.Approve(id, ArtifactType.Spec, 1);
        Assert.Equal(InitiativeStatus.ReadyToBuild, await context.StatusOf(id));

        var error = await Assert.ThrowsAsync<DomainException>(
            () => context.Assign(id, ArtifactType.Spec, 1, new QuestionAssignment("Q-1", new string('a', 60), new string('b', 80))));

        Assert.Equal(Artifact.TooLongMessage, error.Message);
        Assert.Equal((1, ArtifactStatus.Approved, InitiativeStatus.ReadyToBuild), (context.StoredArtifact(id, ArtifactType.Spec).Version, context.StoredArtifact(id, ArtifactType.Spec).Status, await context.StatusOf(id)));
        Assert.DoesNotContain(context.Sender.Sent, request => request is ReopenPlanningCommand);
    }

    [Fact]
    public async Task A_document_not_generated_is_rejected_and_nothing_is_persisted()
    {
        var id = await context.AtClosing(InitiativeDepth.Standard);

        var error = await Assert.ThrowsAsync<DomainException>(() => context.Assign(id, ArtifactType.Prd, 1, All));

        Assert.Equal(ArtifactTexts.NotGenerated, error.Message);
        Assert.Empty(context.Artifacts.Stored);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("regenerate")]
    [InlineData("assign")]
    public async Task A_stale_version_is_rejected_for_every_command_and_nothing_changes_even_in_the_initiative(string command)
    {
        var id = await ReadySpec();
        var saves = context.Artifacts.SaveCount;

        var error = await Assert.ThrowsAsync<ConflictException>(() => command switch
        {
            "approve" => context.Approve(id, ArtifactType.Spec, 1),
            "regenerate" => context.Regenerate(id, ArtifactType.Spec, 1),
            _ => context.Assign(id, ArtifactType.Spec, 1, new QuestionAssignment("Q-1", "TI", "otra vez")),
        });

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        Assert.Equal((saves, 2, InitiativeStatus.ReadyToBuild), (context.Artifacts.SaveCount, context.StoredArtifact(id, ArtifactType.Spec).Version, await context.StatusOf(id)));
    }
}
