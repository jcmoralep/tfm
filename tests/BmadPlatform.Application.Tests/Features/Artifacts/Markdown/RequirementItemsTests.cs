using BmadPlatform.Application.Features.Artifacts.Markdown;

namespace BmadPlatform.Application.Tests.Features.Artifacts.Markdown;

public sealed class RequirementItemsTests
{
    [Fact]
    public void Requirements_are_read_with_or_without_bold_and_the_first_occurrence_wins()
    {
        const string md = """
            # PRD

            ## 4. Funcionalidades

            - **FR-1**: Buscar pedidos
            - FR-3: Exportar
            - **NFR-2**: Responder en 2 s
            - **FR-1**: Repetido
            - fr3 Exportar de nuevo
            - FR3: sin guion
            - Ver FR-9 en el texto

            ## 9. Índice de suposiciones

            - **FR-8**: dentro del índice
            """;

        var items = RequirementItems.Parse(md);

        Assert.Equal(
            [("FR-1", "Buscar pedidos"), ("FR-3", "Exportar"), ("NFR-2", "Responder en 2 s")],
            items.Select(i => (i.Id, i.Text)));
    }

    [Fact]
    public void Fenced_code_and_headings_are_skipped()
    {
        const string md = "```\n- **FR-1**: en un bloque\n```\n### - **FR-2**: en un título\n- **FR-4**: real\n";

        Assert.Equal(["FR-4"], RequirementItems.Parse(md).Select(i => i.Id));
    }

    [Fact]
    public void Decisions_are_read_from_their_headings_outside_fences_and_the_index()
    {
        const string md = """
            ## Invariantes y reglas

            ### AD-1 — Una sola fuente
            ### AD-2 – Eventos
            ### AD-1 — Repetida
            ### AD3 — Sin guion
            ```
            ### AD-5 — en un bloque
            ```
            ## Índice de suposiciones

            ### AD-7 — dentro del índice
            """;

        Assert.Equal(
            [("AD-1", "Una sola fuente"), ("AD-2", "Eventos")],
            ArchitectureDecisionItems.Parse(md).Select(i => (i.Id, i.Title)));
    }
}
