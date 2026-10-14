using BmadPlatform.Application.Features.Artifacts.ExportArtifact;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Infrastructure.Export;

namespace BmadPlatform.Infrastructure.Tests.Export;

public sealed class MarkdownArtifactExporterTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly MarkdownArtifactExporter exporter = new();

    private ArtifactExport Export(
        ArtifactType type = ArtifactType.Brief,
        ArtifactStatus status = ArtifactStatus.Draft,
        string name = "Portal de clientes",
        string content = "# Brief: Portal de clientes\n\n## El problema\n\n- Se pierde tiempo\n") =>
        exporter.Export(new ArtifactExportSource(name, type, status, 3, content, Created, Updated));

    [Fact]
    public void The_file_has_the_bmad_frontmatter_then_the_usage_note_then_the_document_unchanged()
    {
        var export = Export();

        Assert.StartsWith(
            "---\ntitle: \"Portal de clientes\"\nstatus: draft\ncreated: 2026-10-12\nupdated: 2026-10-13\n---\n\n> **Nota de uso**",
            export.Content);
        Assert.EndsWith("\n\n# Brief: Portal de clientes\n\n## El problema\n\n- Se pierde tiempo\n", export.Content);
        Assert.Equal("text/markdown; charset=utf-8", export.ContentType);
    }

    [Theory]
    [InlineData(ArtifactType.Brief, "brief-portal-de-clientes-2026-10-13.md", "bmad-prd")]
    [InlineData(ArtifactType.Prd, "prd-portal-de-clientes-2026-10-13.md", "bmad-architecture")]
    [InlineData(ArtifactType.Spec, "spec-portal-de-clientes-2026-10-13.md", "bmad-build")]
    [InlineData(ArtifactType.Architecture, "architecture-portal-de-clientes-2026-10-13.md", "bmad-create-epics-and-stories")]
    [InlineData(ArtifactType.EpicsAndStories, "epics-portal-de-clientes-2026-10-13.md", "bmad-build")]
    public void Each_type_gets_its_file_name_and_names_the_bmad_skill_that_takes_it(ArtifactType type, string fileName, string skill)
    {
        var export = Export(type);

        Assert.Equal(fileName, export.FileName);
        Assert.Contains($"`{skill}`", export.Content);
    }

    [Fact]
    public void The_architecture_declares_its_kind_names_the_spine_file_and_asks_for_the_development_review()
    {
        var architecture = Export(ArtifactType.Architecture, ArtifactStatus.Approved);

        Assert.StartsWith(
            "---\ntitle: \"Portal de clientes\"\ntype: architecture-spine\nstatus: approved\ncreated: 2026-10-12\nupdated: 2026-10-13\n---\n\n> **Nota de uso**",
            architecture.Content);
        Assert.Contains("`planning-artifacts/architecture/architecture-<proyecto>-<fecha>/ARCHITECTURE-SPINE.md`", architecture.Content);
        Assert.Contains("para revisión del equipo de desarrollo", architecture.Content);
        Assert.Contains("«Preguntas para desarrollo»", architecture.Content);
        Assert.All(
            new[] { ArtifactType.Spec, ArtifactType.Brief, ArtifactType.Prd },
            type => Assert.DoesNotContain("\ntype:", Export(type).Content));
    }

    [Fact]
    public void The_epics_name_their_inputs_in_the_frontmatter_the_target_file_and_ask_for_the_development_review()
    {
        var epics = Export(ArtifactType.EpicsAndStories, ArtifactStatus.Approved);

        Assert.StartsWith(
            "---\ntitle: \"Portal de clientes\"\nstepsCompleted: ['step-01-validate-prerequisites']\ninputDocuments: [prd.md, ARCHITECTURE-SPINE.md]\nstatus: approved\ncreated: 2026-10-12\nupdated: 2026-10-13\n---\n\n> **Nota de uso**",
            epics.Content);
        Assert.Contains("`planning-artifacts/epics.md`", epics.Content);
        Assert.Contains("para revisión del equipo de desarrollo", epics.Content);
        Assert.Contains("«Preguntas para desarrollo»", epics.Content);
        Assert.All(
            new[] { ArtifactType.Spec, ArtifactType.Brief, ArtifactType.Prd, ArtifactType.Architecture },
            type => Assert.DoesNotContain("inputDocuments", Export(type).Content));
    }

    [Fact]
    public void A_draft_says_so_and_an_approved_document_does_not()
    {
        var draft = Export(status: ArtifactStatus.Draft);
        var approved = Export(status: ArtifactStatus.Approved);

        Assert.Contains("status: draft", draft.Content);
        Assert.Contains("todavía no lo aprobó", draft.Content);
        Assert.Contains("status: approved", approved.Content);
        Assert.DoesNotContain("todavía no lo aprobó", approved.Content);
    }

    [Theory]
    [InlineData("Módulo de Facturación Ñandú", "modulo-de-facturacion-nandu")]
    [InlineData("  ¡¿Qué?!  ", "que")]
    [InlineData("???", "iniciativa")]
    [InlineData("a/b\\c:d*e", "a-b-c-d-e")]
    public void The_file_name_uses_a_safe_ascii_slug_of_the_initiative_name(string name, string expectedSlug)
    {
        Assert.Equal(expectedSlug, MarkdownArtifactExporter.Slug(name));
    }

    [Fact]
    public void A_long_name_is_cut_without_leaving_a_trailing_hyphen()
    {
        var slug = MarkdownArtifactExporter.Slug("Portal de autogestion para clientes corporativos y sucursales");

        Assert.True(slug.Length <= 40);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void Quotes_backslashes_and_line_breaks_in_the_name_cannot_break_the_frontmatter()
    {
        var export = Export(name: "El \"nuevo\" portal\\v2\r\nsegunda línea");

        Assert.Contains("title: \"El \\\"nuevo\\\" portal\\\\v2  segunda línea\"\n", export.Content);
    }

    [Fact]
    public void ToString_does_not_print_the_document()
    {
        var source = new ArtifactExportSource("X", ArtifactType.Brief, ArtifactStatus.Draft, 1, "texto privado", Created, Updated);

        Assert.DoesNotContain("texto privado", source.ToString());
        Assert.DoesNotContain("texto privado", Export(content: "texto privado").ToString());
    }
}
