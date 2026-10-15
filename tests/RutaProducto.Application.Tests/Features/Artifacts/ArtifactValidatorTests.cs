using RutaProducto.Application.Features.Artifacts.ApproveArtifact;
using RutaProducto.Application.Features.Artifacts.GenerateArtifact;
using RutaProducto.Application.Features.Artifacts.RegenerateArtifact;
using RutaProducto.Application.Features.Artifacts.SaveArtifactContent;
using RutaProducto.Domain.Artifacts;

namespace RutaProducto.Application.Tests.Features.Artifacts;

public sealed class ArtifactValidatorTests
{
    private static readonly SaveArtifactContentCommandValidator Validator = new();

    private static string[] Errors(string content) =>
        [.. Validator.Validate(new SaveArtifactContentCommand(Guid.NewGuid(), ArtifactType.Prd, 1, content)).Errors.Select(e => e.ErrorMessage)];

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    [InlineData("\n\r\n \t")]
    public void Blank_content_is_rejected_with_the_empty_message(string content) =>
        Assert.Equal(["El documento no puede estar vacío."], Errors(content));

    [Fact]
    public void The_cap_is_exactly_20000_characters_measured_after_normalization()
    {
        var atCap = new string('x', 20000);

        Assert.Empty(Errors(atCap));
        Assert.Empty(Errors("\r\n\r\n" + atCap + "\r\n  \r\n"));
        Assert.Equal(["El documento no puede superar los 20000 caracteres."], Errors(atCap + "x"));
    }

    [Fact]
    public void Every_command_needs_an_initiative_a_known_type_and_a_positive_version()
    {
        var empty = Guid.Empty;
        const ArtifactType Unknown = (ArtifactType)99;

        Assert.Equal(2, new GenerateArtifactCommandValidator().Validate(new GenerateArtifactCommand(empty, Unknown)).Errors.Count);
        Assert.Equal(3, new SaveArtifactContentCommandValidator().Validate(new SaveArtifactContentCommand(empty, Unknown, 0, "x")).Errors.Count);
        Assert.Equal(3, new RegenerateArtifactCommandValidator().Validate(new RegenerateArtifactCommand(empty, Unknown, 0, true)).Errors.Count);
        Assert.Equal(3, new ApproveArtifactCommandValidator().Validate(new ApproveArtifactCommand(empty, Unknown, 0)).Errors.Count);
        Assert.True(new ApproveArtifactCommandValidator().Validate(new ApproveArtifactCommand(Guid.NewGuid(), ArtifactType.Spec, 1)).IsValid);
    }
}
