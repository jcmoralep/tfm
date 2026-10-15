using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Domain.Artifacts;

namespace RutaProducto.Application.Tests.Features.Artifacts.Markdown;

public sealed class ArtifactOutlineTests
{
    [Fact]
    public void Brief_sections_and_order()
    {
        Assert.Equal(
            [
                "Resumen ejecutivo", "El problema", "La solución", "Qué lo hace distinto", "A quién sirve",
                "Criterios de éxito", "Alcance", "Visión", "Índice de suposiciones", "Preguntas pendientes",
            ],
            ArtifactOutline.Sections(ArtifactType.Brief).Select(s => s.Title));
    }

    [Fact]
    public void Prd_sections_and_order()
    {
        var sections = ArtifactOutline.Sections(ArtifactType.Prd);

        Assert.Equal(
            [
                "0 Propósito del documento", "1 Visión", "2 Usuario objetivo", "2.1 Trabajos por hacer", "2.2 No usuarios",
                "2.3 Recorridos clave", "3 Glosario", "4 Funcionalidades", "5 No objetivos", "6 Alcance del MVP",
                "6.1 Dentro", "6.2 Fuera", "7 Métricas de éxito", "8 Preguntas abiertas", "9 Índice de suposiciones",
            ],
            sections.Select(s => s.Number + " " + s.Title));
        Assert.All(sections.Where(s => s.Number.Contains('.')), s => Assert.Equal(3, s.Level));
        Assert.All(sections.Where(s => !s.Number.Contains('.')), s => Assert.Equal(2, s.Level));
    }

    [Fact]
    public void Spec_sections_and_order()
    {
        Assert.Equal(
            ["Por qué", "Capacidades", "Restricciones", "No objetivos", "Señal de éxito", "Índice de suposiciones", "Preguntas abiertas"],
            ArtifactOutline.Sections(ArtifactType.Spec).Select(s => s.Title));
    }

    [Fact]
    public void Headings_are_rendered_with_their_level_and_number()
    {
        var prd = ArtifactOutline.Sections(ArtifactType.Prd);

        Assert.Equal("## 0 Propósito del documento", prd[0].Heading);
        Assert.Equal("### 2.1 Trabajos por hacer", prd[3].Heading);
        Assert.Equal("## Resumen ejecutivo", ArtifactOutline.Sections(ArtifactType.Brief)[0].Heading);
    }

    [Fact]
    public void Item_and_marker_lines_use_the_canonical_form()
    {
        Assert.Equal("- **FR-1**: texto", ArtifactOutline.Item("FR-1", "texto"));
        Assert.Equal("[ASSUMPTION: A-1]", ArtifactOutline.AssumptionMarker("A-1"));
    }
}
