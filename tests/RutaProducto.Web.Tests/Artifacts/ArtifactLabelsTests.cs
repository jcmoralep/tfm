using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;
using RutaProducto.Web.Components.Artifacts;

namespace RutaProducto.Web.Tests.Artifacts;

public sealed class ArtifactLabelsTests
{
    [Theory]
    [InlineData("especificacion", true, ArtifactType.Spec)]
    [InlineData("brief", true, ArtifactType.Brief)]
    [InlineData("prd", true, ArtifactType.Prd)]
    [InlineData("PRD", false, default(ArtifactType))]
    [InlineData("arquitectura", true, ArtifactType.Architecture)]
    [InlineData("epicas-e-historias", true, ArtifactType.EpicsAndStories)]
    [InlineData("Arquitectura", false, default(ArtifactType))]
    [InlineData("", false, default(ArtifactType))]
    [InlineData(null, false, default(ArtifactType))]
    public void Slugs_parse_exactly(string? slug, bool expected, ArtifactType expectedType)
    {
        Assert.Equal(expected, ArtifactLabels.TryParseSlug(slug, out var type));
        Assert.Equal(expectedType, type);
    }

    [Theory]
    [InlineData(ArtifactType.Spec)]
    [InlineData(ArtifactType.Brief)]
    [InlineData(ArtifactType.Prd)]
    [InlineData(ArtifactType.Architecture)]
    [InlineData(ArtifactType.EpicsAndStories)]
    public void Every_slug_round_trips(ArtifactType type)
    {
        Assert.True(ArtifactLabels.TryParseSlug(ArtifactLabels.Slug(type), out var parsed));
        Assert.Equal(type, parsed);
    }

    private static ArtifactSlotView Slot(
        ArtifactType? type,
        ArtifactAvailability availability,
        bool generated = false,
        ArtifactStatus? status = null,
        int version = 0,
        string? reason = null) =>
        new("Documento", type, availability, generated, status, version, reason);

    [Fact]
    public void Generated_draft_and_approved_rows_show_version_and_offer_to_open()
    {
        var draft = ArtifactLabels.Row(Slot(ArtifactType.Brief, ArtifactAvailability.Available, true, ArtifactStatus.Draft, 2));
        var approved = ArtifactLabels.Row(Slot(ArtifactType.Prd, ArtifactAvailability.Available, true, ArtifactStatus.Approved, 3));

        Assert.Equal(("Borrador v2", ArtifactTone.InProgress, ArtifactAction.Open), (draft.State, draft.Tone, draft.Action));
        Assert.Equal(("Aprobado v3", ArtifactTone.Done, ArtifactAction.Open), (approved.State, approved.Tone, approved.Action));
    }

    [Fact]
    public void Available_row_offers_to_generate()
    {
        var row = ArtifactLabels.Row(Slot(ArtifactType.Spec, ArtifactAvailability.Available));

        Assert.Equal(("Disponible", ArtifactAction.Generate), (row.State, row.Action));
    }

    [Theory]
    [InlineData(ArtifactAvailability.WaitingForClarify)]
    [InlineData(ArtifactAvailability.WaitingForClosing)]
    [InlineData(ArtifactAvailability.InitiativeDraft)]
    public void Unavailable_row_keeps_its_reason_and_has_no_action(ArtifactAvailability availability)
    {
        var row = ArtifactLabels.Row(Slot(ArtifactType.Prd, availability, reason: "Primero termine las preguntas."));

        Assert.Equal("Aún no disponible", row.State);
        Assert.Equal("Primero termine las preguntas.", row.Reason);
        Assert.Null(row.Action);
    }

    [Fact]
    public void Later_step_slot_is_pending_without_action()
    {
        var row = ArtifactLabels.Row(Slot(null, ArtifactAvailability.NotYetSupported, reason: ArtifactTexts.ComingLater));

        Assert.Equal(("Aún no disponible", ArtifactTexts.ComingLater), (row.State, row.Reason));
        Assert.Null(row.Action);
    }

    [Theory]
    [InlineData(ArtifactStatus.Draft, InitiativeStatus.Planning, null)]
    [InlineData(ArtifactStatus.Approved, InitiativeStatus.Planning, "Si cambia algo y guarda, el documento vuelve a Borrador y hay que aprobarlo de nuevo.")]
    [InlineData(ArtifactStatus.Approved, InitiativeStatus.ReadyToBuild, "Si cambia algo y guarda, el documento vuelve a Borrador, hay que aprobarlo de nuevo y la iniciativa vuelve a Planificando.")]
    public void Edit_hint_warns_only_when_saving_undoes_an_approval(ArtifactStatus status, InitiativeStatus initiative, string? expected)
    {
        Assert.Equal(expected, ArtifactLabels.EditHint(status, initiative));
    }

    [Theory]
    [InlineData(ArtifactType.Spec, false)]
    [InlineData(ArtifactType.Brief, false)]
    [InlineData(ArtifactType.Prd, false)]
    [InlineData(ArtifactType.Architecture, true)]
    [InlineData(ArtifactType.EpicsAndStories, true)]
    public void Approval_copy_mentions_development_review_only_for_the_draft_documents(ArtifactType type, bool review)
    {
        var text = ArtifactLabels.ApproveConfirmText(type);

        Assert.Equal(review, text.Contains("desarrollo lo revisa", StringComparison.Ordinal));
        Assert.Equal(review, text.Contains("usted confirma que lo revisó", StringComparison.Ordinal));
        Assert.Equal(review, ArtifactLabels.NeedsDevelopmentReview(type));
    }

    [Theory]
    [InlineData(3, "Preguntas guardadas. Versión 3.")]
    [InlineData(12, "Preguntas guardadas. Versión 12.")]
    public void Saved_questions_notice_carries_the_version(int version, string expected)
    {
        Assert.Equal(expected, ArtifactLabels.QuestionsSaved(version));
    }

    [Fact]
    public void Approve_blocked_note_lists_the_ids_and_points_to_the_panel()
    {
        var text = ArtifactLabels.ApproveBlocked(["Q-1", "Q-3"]);

        Assert.StartsWith(ArtifactTexts.UnassignedQuestions(["Q-1", "Q-3"]), text);
        Assert.Contains("Preguntas pendientes", text);
    }

    [Theory]
    [InlineData("https://sitio.example/iniciativas/ab12/artefactos/brief", "https://sitio.example/iniciativas/ab12/artefactos/brief#questions-title")]
    [InlineData("https://sitio.example/iniciativas/ab12/artefactos/brief?generado=1", "https://sitio.example/iniciativas/ab12/artefactos/brief?generado=1#questions-title")]
    [InlineData("http://localhost:5080/iniciativas/ab12/artefactos/prd#otra", "http://localhost:5080/iniciativas/ab12/artefactos/prd#questions-title")]
    public void The_in_page_link_keeps_the_current_page_and_replaces_any_fragment(string current, string expected)
    {
        Assert.Equal(expected, ArtifactLabels.SamePageAnchor(current, "questions-title"));
    }
}
