using BmadPlatform.Application.Features.Artifacts.Markdown;

namespace BmadPlatform.Web.Components.Artifacts;

/// <summary>What the panel sends on Guardar: the changed rows, against the version the rows were copied from.</summary>
public sealed record QuestionOwnersSubmission(int BaseVersion, IReadOnlyList<QuestionAssignment> Assignments)
{
    // The synthesized ToString would print the values typed by the user.
    public override string ToString() => $"{nameof(QuestionOwnersSubmission)} {{ BaseVersion = {BaseVersion}, Assignments = {Assignments.Count} [redacted] }}";
}

/// <summary>One editable row of the panel. The defaults ("por asignar", "por definir") show as empty fields.</summary>
public sealed class QuestionRow
{
    public required string Id { get; init; }

    public required string Text { get; init; }

    public required bool Assigned { get; init; }

    public string Owner { get; set; } = "";

    public string Revisit { get; set; } = "";

    /// <summary>The values the row was loaded with, to send only what the person changed.</summary>
    public string LoadedOwner { get; init; } = "";

    public string LoadedRevisit { get; init; } = "";

    public bool Changed => Owner != LoadedOwner || Revisit != LoadedRevisit;
}

/// <summary>Pure state of the panel, so the rules for the rows can be tested without a browser.</summary>
public static class QuestionRows
{
    /// <summary>One row per distinct id (the first definition wins), in document order.</summary>
    public static List<QuestionRow> From(IEnumerable<OpenQuestion> questions) =>
        questions
            .GroupBy(question => question.Id)
            .Select(group => group.First())
            .Select(question =>
            {
                var owner = Visible(question.Owner, OpenQuestions.OwnerPlaceholder);
                var revisit = Visible(question.Revisit, OpenQuestions.RevisitPlaceholder);

                return new QuestionRow
                {
                    Id = question.Id,
                    Text = question.Text,
                    Assigned = question.IsAssigned,
                    Owner = owner,
                    Revisit = revisit,
                    LoadedOwner = owner,
                    LoadedRevisit = revisit,
                };
            })
            .ToList();

    /// <summary>Only the rows whose values differ from the loaded ones, so a long list stays under the command cap.</summary>
    public static QuestionOwnersSubmission ToSubmission(int baseVersion, IEnumerable<QuestionRow> rows) =>
        new(baseVersion, rows.Where(row => row.Changed).Select(row => new QuestionAssignment(row.Id, row.Owner, row.Revisit)).ToList());

    private static string Visible(string? value, string placeholder) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), placeholder, StringComparison.OrdinalIgnoreCase)
            ? ""
            : value.Trim();
}
