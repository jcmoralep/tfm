using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;
using RutaProducto.Infrastructure.Artifacts;

namespace RutaProducto.Infrastructure.Tests.Artifacts;

public sealed class DraftComposerTests
{
    // The model cannot write the numbering the code owns: the leading identifier is removed, the rest is kept.
    [Theory]
    [InlineData("FR-9: Exportar", "Exportar")]
    [InlineData("AD-4 Persistencia", "Persistencia")]
    [InlineData("Epic 9: Pagos", "Pagos")]
    [InlineData("Story 3.7 Cobro", "Cobro")]
    [InlineData("Épica 2: Pagos", "Pagos")]
    [InlineData("Historia 3.1: Cobro", "Cobro")]
    [InlineData("Pagos de Epic 9", "Pagos de Epic 9")]
    public void Leading_identifiers_and_epic_numbering_are_stripped_from_model_text(string raw, string expected)
    {
        Assert.Equal(expected, DraftComposer.ModelText(raw));
    }

    [Theory]
    [InlineData(false, " (Responsable: por asignar; Revisar: por definir)")]
    [InlineData(true, " (Responsable: Equipo de desarrollo; Revisar: antes de construir)")]
    public void Questions_carry_the_default_suffix_unless_one_is_given(bool development, string suffix)
    {
        var composer = new DraftComposer(new DraftRequest(ArtifactType.Spec, "Pedidos", null, InitiativeDepth.Small, []), 1000);
        composer.OpenQuestion("¿Quién aprueba?");

        composer.OpenQuestions("Preguntas abiertas", "Ninguna.", development ? OpenQuestions.DevelopmentSuffix : null);

        var question = Assert.Single(OpenQuestions.Parse(composer.Finish()));
        Assert.Equal(development, question.IsAssigned);
        Assert.EndsWith(suffix, composer.Finish(), StringComparison.Ordinal);
    }
}
