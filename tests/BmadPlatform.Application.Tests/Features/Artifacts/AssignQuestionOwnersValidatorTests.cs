using BmadPlatform.Application.Features.Artifacts.AssignQuestionOwners;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Tests.Features.Artifacts;

public sealed class AssignQuestionOwnersValidatorTests
{
    private static readonly AssignQuestionOwnersCommandValidator Validator = new();

    private const string OwnerForbidden = "El responsable no puede contener paréntesis, corchetes, comillas invertidas, ni los signos ; * | \\ < >, ni caracteres de control o saltos de línea.";

    private const string RevisitForbidden = "La condición de revisión no puede contener paréntesis, corchetes, comillas invertidas, ni los signos ; * | \\ < >, ni caracteres de control o saltos de línea.";

    private static string[] Errors(params QuestionAssignment[] assignments) =>
        [.. Validator.Validate(new AssignQuestionOwnersCommand(Guid.NewGuid(), ArtifactType.Prd, 1, assignments)).Errors.Select(e => e.ErrorMessage)];

    [Fact]
    public void An_empty_list_is_rejected()
    {
        Assert.Equal(["No hay preguntas para guardar."], Errors());
    }

    [Fact]
    public void More_than_200_rows_or_a_repeated_id_are_rejected()
    {
        QuestionAssignment[] many = [.. Enumerable.Range(1, 201).Select(n => new QuestionAssignment($"Q-{n}", "TI", "x"))];

        Assert.Single(Errors(many));
        Assert.Equal(["Hay preguntas repetidas en el envío."], Errors(new("Q-1", "TI", "x"), new("Q-1", "TI", "y")));
        Assert.Empty(Errors(many[..200]));
    }

    [Theory]
    [InlineData("Q-0")]
    [InlineData("Q1")]
    [InlineData("q-1")]
    [InlineData("Q-01")]
    [InlineData("Q-1234567")]
    [InlineData("Q-1\n")]
    [InlineData("")]
    public void An_id_that_is_not_a_valid_hyphenated_number_is_rejected(string id) =>
        Assert.Single(Errors(new QuestionAssignment(id, "TI", "x")));

    [Fact]
    public void An_id_the_reader_can_count_is_accepted() =>
        Assert.Empty(Errors(new QuestionAssignment("Q-999999", "TI", "x")));

    [Theory]
    [InlineData("Legal (TI)")]
    [InlineData("a;b")]
    [InlineData("a|b")]
    [InlineData("*x*")]
    [InlineData("a\nb")]
    [InlineData("a]b")]
    [InlineData("a<b")]
    [InlineData("a`b")]
    [InlineData("ab\x202E")]
    public void An_owner_with_forbidden_characters_is_rejected_with_its_row_id(string owner) =>
        Assert.Equal(["Q-2: " + OwnerForbidden], Errors(new QuestionAssignment("Q-2", owner, "x")));

    [Theory]
    [InlineData("antes (de)")]
    [InlineData("a;b")]
    [InlineData("a\r\nb")]
    [InlineData("ab\x200B")]
    [InlineData("a\0b")]
    public void A_revisit_text_with_forbidden_characters_is_rejected(string revisit) =>
        Assert.Equal(["Q-2: " + RevisitForbidden], Errors(new QuestionAssignment("Q-2", "TI", revisit)));

    [Fact]
    public void The_limits_are_60_and_80_characters_after_trimming_and_exact_lengths_are_accepted()
    {
        Assert.Empty(Errors(new QuestionAssignment("Q-1", new string('a', 60), new string('b', 80))));
        Assert.Empty(Errors(new QuestionAssignment("Q-1", "  " + new string('a', 60) + "  ", null)));
        Assert.Equal(["Q-1: El responsable no puede superar los 60 caracteres."], Errors(new QuestionAssignment("Q-1", new string('a', 61), "x")));
        Assert.Equal(["Q-1: La condición de revisión no puede superar los 80 caracteres."], Errors(new QuestionAssignment("Q-1", "TI", new string('b', 81))));
    }
}
