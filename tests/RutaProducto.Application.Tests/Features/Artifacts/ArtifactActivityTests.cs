using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Initiatives;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Application.Tests.Features.Artifacts;

/// <summary>Each document use case notes activity on its initiative after it succeeded, and only then.</summary>
public sealed class ArtifactActivityTests
{
    private const string Questions = "# Spec\n\n- **FR-1**: algo\n\n## Preguntas abiertas\n\n- **Q-1**: uno (Responsable: por asignar; Revisar: por definir)";

    private readonly ArtifactTestContext context = new();

    private int Recorded() => context.Sender.Sent.OfType<RecordInitiativeActivityCommand>().Count();

    /// <summary>Remembers whether <paramref name="effectStored"/> already held when the activity was requested.</summary>
    private Func<bool> WatchOrder(Func<bool> effectStored)
    {
        var storedFirst = false;
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                storedFirst = effectStored();
            }
        };

        return () => storedFirst;
    }

    private async Task<Guid> SmallWithSpec(string? content = null)
    {
        var id = await context.AtClosing(InitiativeDepth.Small);

        if (content is not null)
        {
            context.Drafter.Output = content;
        }

        await context.Generate(id, ArtifactType.Spec);

        return id;
    }

    [Fact]
    public async Task Generating_notes_activity_after_the_document_is_stored_and_not_when_it_is_refused()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        var storedFirst = WatchOrder(() => context.Artifacts.Stored.Any(a => a.InitiativeId == id));
        var before = Recorded();

        await context.Generate(id, ArtifactType.Spec);

        Assert.Equal(before + 1, Recorded());
        Assert.Equal(id, context.Sender.Sent.OfType<RecordInitiativeActivityCommand>().Last().InitiativeId);
        Assert.True(storedFirst());

        before = Recorded();
        await Assert.ThrowsAsync<DomainException>(() => context.Generate(id, ArtifactType.Spec)); // already exists
        Assert.Equal(before, Recorded());
    }

    [Fact]
    public async Task Regenerating_notes_activity_after_the_replacement_is_stored_and_not_when_it_is_refused()
    {
        var id = await SmallWithSpec();
        var storedFirst = WatchOrder(() => context.StoredArtifact(id, ArtifactType.Spec).Version == 2);
        var before = Recorded();

        await Assert.ThrowsAsync<DomainException>(() => context.Regenerate(id, ArtifactType.Spec, 1, confirmed: false));
        Assert.Equal(before, Recorded());

        await context.Regenerate(id, ArtifactType.Spec, 1);

        Assert.Equal(before + 1, Recorded());
        Assert.True(storedFirst());
    }

    [Fact]
    public async Task Saving_notes_activity_after_the_edit_is_stored_but_not_for_the_same_text_or_a_stale_version()
    {
        var id = await SmallWithSpec();
        var storedFirst = WatchOrder(() => context.StoredArtifact(id, ArtifactType.Spec).Version == 2);
        var before = Recorded();

        await Assert.ThrowsAsync<ConflictException>(() => context.Save(id, ArtifactType.Spec, 7, "# otro"));
        await context.Save(id, ArtifactType.Spec, 1, context.StoredArtifact(id, ArtifactType.Spec).Content); // same text
        Assert.Equal(before, Recorded());

        await context.Save(id, ArtifactType.Spec, 1, "# Editado");

        Assert.Equal(before + 1, Recorded());
        Assert.True(storedFirst());
    }

    [Fact]
    public async Task Assigning_owners_notes_activity_after_the_edit_is_stored_and_not_for_a_stale_version()
    {
        var id = await SmallWithSpec(Questions);
        var storedFirst = WatchOrder(() => context.StoredArtifact(id, ArtifactType.Spec).Version == 2);
        var before = Recorded();
        var assignment = new QuestionAssignment("Q-1", "Legal", "mañana");

        await Assert.ThrowsAsync<ConflictException>(() => context.Assign(id, ArtifactType.Spec, 7, assignment));
        Assert.Equal(before, Recorded());

        await context.Assign(id, ArtifactType.Spec, 1, assignment);

        Assert.Equal(before + 1, Recorded());
        Assert.True(storedFirst());
    }

    [Fact]
    public async Task Approving_notes_activity_after_the_approval_is_stored_and_not_for_a_stale_version()
    {
        var id = await SmallWithSpec();
        var storedFirst = WatchOrder(() => context.StoredArtifact(id, ArtifactType.Spec).Status == ArtifactStatus.Approved);
        var before = Recorded();

        await Assert.ThrowsAsync<ConflictException>(() => context.Approve(id, ArtifactType.Spec, 7));
        Assert.Equal(before, Recorded());

        await context.Approve(id, ArtifactType.Spec, 1);

        Assert.Equal(before + 1, Recorded());
        Assert.True(storedFirst());
    }

    [Fact]
    public async Task A_failure_while_noting_activity_never_fails_the_document_action_and_is_logged_without_content()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                throw new InvalidOperationException("detalle-privado");
            }
        };

        var view = await context.Generate(id, ArtifactType.Spec);

        Assert.True(view.Generated);
        var warning = Assert.Single(context.Assistant.Logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain("detalle-privado", warning.AllText, StringComparison.Ordinal);
        Assert.Contains(id.ToString(), warning.AllText, StringComparison.Ordinal);
    }
}
