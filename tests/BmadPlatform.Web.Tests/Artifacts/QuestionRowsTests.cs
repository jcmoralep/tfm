using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Web.Components.Artifacts;

namespace BmadPlatform.Web.Tests.Artifacts;

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
    public void One_row_per_distinct_id_and_the_submission_sends_all_of_them()
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
        Assert.Equal(["Q-1", "Q-2"], submission.Assignments.Select(a => a.Id));
        Assert.Equal(["Legal", "TI"], submission.Assignments.Select(a => a.Owner));
    }
}
