using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Web.Components.Artifacts;

namespace RutaProducto.Web.Tests.Artifacts;

public sealed class QuestionRowsTests
{
    [Theory]
    [InlineData("por asignar", "por definir", "", "", false)]
    [InlineData(null, null, "", "", false)]
    [InlineData("Legal", "por definir", "Legal", "", false)]
    [InlineData("Legal", "antes de la arquitectura", "Legal", "antes de la arquitectura", true)]
    public void Defaults_show_as_empty_fields(string? owner, string? revisit, string shownOwner, string shownRevisit, bool assigned)
    {
        var rows = QuestionRows.From([new OpenQuestion("Q-1", 3, "¿Quién aprueba?", owner, revisit)]);

        var row = Assert.Single(rows);
        Assert.Equal((shownOwner, shownRevisit, assigned), (row.Owner, row.Revisit, row.Assigned));
    }

    [Fact]
    public void One_row_per_distinct_id_and_the_submission_sends_only_the_changed_ones()
    {
        var rows = QuestionRows.From(
        [
            new OpenQuestion("Q-1", 3, "Primera", null, null),
            new OpenQuestion("Q-2", 4, "Segunda", "TI", "mañana"),
            new OpenQuestion("Q-1", 9, "Repetida", null, null),
        ]);
        rows[0].Owner = "Legal";

        var submission = QuestionRows.ToSubmission(5, rows);

        Assert.Equal(5, submission.BaseVersion);
        var changed = Assert.Single(submission.Assignments);
        Assert.Equal(("Q-1", "Legal"), (changed.Id, changed.Owner));
    }

    [Fact]
    public void Nothing_edited_sends_nothing_so_a_long_list_stays_under_the_cap()
    {
        var rows = QuestionRows.From(Enumerable.Range(1, 250).Select(n => new OpenQuestion($"Q-{n}", n, "x", null, null)));

        Assert.Empty(QuestionRows.ToSubmission(1, rows).Assignments);
    }

    [Fact]
    public void There_is_something_to_save_only_when_a_field_differs_from_what_was_loaded()
    {
        var rows = QuestionRows.From([new OpenQuestion("Q-1", 3, "Primera", "TI", "mañana"), new OpenQuestion("Q-2", 4, "Segunda", null, null)]);

        Assert.False(QuestionRows.HasChanges(rows));

        rows[1].Revisit = "antes de la arquitectura";
        Assert.True(QuestionRows.HasChanges(rows));

        rows[1].Revisit = rows[1].LoadedRevisit;
        Assert.False(QuestionRows.HasChanges(rows));
    }
}
