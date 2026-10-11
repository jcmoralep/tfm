using BmadPlatform.Application.Features.Artifacts.Markdown;

namespace BmadPlatform.Web.Components.Artifacts;

/// <summary>What the panel sends on Guardar: every row, against the version the rows were copied from.</summary>
public sealed record QuestionOwnersSubmission(int BaseVersion, IReadOnlyList<QuestionAssignment> Assignments);

/// <summary>One editable row of the panel. The defaults ("por asignar", "por definir") show as empty fields.</summary>
public sealed class QuestionRow
{
    public required string Id { get; init; }

    public required string Text { get; init; }

    public required bool Assigned { get; init; }

    public string Owner { get; set; } = "";

    public string Revisit { get; set; } = "";
}

/// <summary>Pure state of the panel, so the rules for the rows can be tested without a browser.</summary>
public static class QuestionRows
{
    /// <summary>One row per distinct id (the first definition wins), in document order.</summary>
    public static List<QuestionRow> From(IEnumerable<OpenQuestion> questions) =>
        questions
            .GroupBy(question => question.Id)
            .Select(group => group.First())
            .Select(question => new QuestionRow
            {
                Id = question.Id,
                Text = question.Text,
                Assigned = question.IsAssigned,
                Owner = Visible(question.Owner, OpenQuestions.OwnerPlaceholder),
                Revisit = Visible(question.Revisit, OpenQuestions.RevisitPlaceholder),
            })
            .ToList();

    public static QuestionOwnersSubmission ToSubmission(int baseVersion, IEnumerable<QuestionRow> rows) =>
        new(baseVersion, rows.Select(row => new QuestionAssignment(row.Id, row.Owner, row.Revisit)).ToList());

    private static string Visible(string? value, string placeholder) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), placeholder, StringComparison.OrdinalIgnoreCase)
            ? ""
            : value.Trim();
}
