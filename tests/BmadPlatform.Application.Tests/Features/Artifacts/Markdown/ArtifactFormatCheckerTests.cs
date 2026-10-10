using System.Text;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Tests.Features.Artifacts.Markdown;

public sealed class ArtifactFormatCheckerTests
{
    // A clean Spec built from the outline: one item per list section, one assumption with its index entry.
    private static string CleanSpec(Func<string, string>? edit = null)
    {
        var builder = new StringBuilder("# Pedidos\n\n");

        foreach (var section in ArtifactOutline.Sections(ArtifactType.Spec))
        {
            builder.Append(section.Heading).Append("\n\n");

            switch (section.Title)
            {
                case "Capacidades":
                    builder.Append("- **FR-1**: Buscar un pedido\n- **FR-2**: Ver su estado\n");
                    break;
                case "Restricciones":
                    builder.Append("[ASSUMPTION: A-1] Sin límites conocidos.\n");
                    break;
                case "Señal de éxito":
                    builder.Append("- **SM-1**: Bajan las quejas\n");
                    break;
                case "Índice de suposiciones":
                    builder.Append("- **A-1**: Sin límites conocidos.\n");
                    break;
                case "Preguntas abiertas":
                    builder.Append("- **Q-1**: ¿Quién aprueba?\n");
                    break;
                default:
                    builder.Append("Texto.\n");
                    break;
            }

            builder.Append('\n');
        }

        var markdown = builder.ToString();

        return edit is null ? markdown : edit(markdown);
    }

    private static IReadOnlyList<FormatWarning> CheckSpec(string markdown) => ArtifactFormatChecker.Check(ArtifactType.Spec, markdown);

    [Fact]
    public void A_clean_document_has_no_warnings()
    {
        Assert.Empty(CheckSpec(CleanSpec()));
    }

    [Fact]
    public void Missing_section_is_named_and_the_text_is_untouched()
    {
        var markdown = CleanSpec(m => m.Replace("## Restricciones", "Restricciones sin título", StringComparison.Ordinal));
        var snapshot = markdown.ToCharArray();

        var warnings = CheckSpec(markdown);

        var warning = Assert.Single(warnings);
        Assert.Null(warning.Line);
        Assert.Equal("Falta la sección «Restricciones».", warning.Message);
        Assert.Equal(new string(snapshot), markdown);
    }

    [Fact]
    public void Prd_missing_glossary_is_reported()
    {
        var prd = string.Join("\n\n", ArtifactOutline.Sections(ArtifactType.Prd)
            .Where(s => s.Title != "Glosario")
            .Select(s => s.Heading));

        var warnings = ArtifactFormatChecker.Check(ArtifactType.Prd, prd);

        Assert.Equal("Falta la sección «Glosario».", Assert.Single(warnings).Message);
    }

    [Fact]
    public void Heading_match_ignores_case_numbers_and_trailing_hashes()
    {
        var markdown = CleanSpec(m => m.Replace("## Por qué", "## 1. por QUÉ ##", StringComparison.Ordinal));

        Assert.Empty(CheckSpec(markdown));
    }

    [Theory]
    [InlineData("- **FR-2**: Ver su estado", "FR3 Exportar", "FR3", "FR")]
    [InlineData("- **FR-2**: Ver su estado", "- FR -3 Exportar", "FR -3", "FR")]
    [InlineData("- **FR-2**: Ver su estado", "- fr-3 Exportar", "fr-3", "FR")]
    [InlineData("- **FR-2**: Ver su estado", "- **FR-**: Exportar", "FR-", "FR")]
    [InlineData("- **FR-2**: Ver su estado", "- **FR-03**: Exportar", "FR-03", "FR")]
    [InlineData("- **FR-2**: Ver su estado", "- **NFR-x**: Exportar", "NFR-x", "NFR")]
    public void Malformed_identifier_is_named(string anchor, string line, string token, string prefix)
    {
        var markdown = CleanSpec(m => m.Replace(anchor, anchor + "\n" + line, StringComparison.Ordinal));
        var expectedLine = markdown.Split('\n').ToList().FindIndex(l => l == line) + 1;

        var warnings = CheckSpec(markdown);

        var warning = Assert.Single(warnings);
        Assert.Equal(expectedLine, warning.Line);
        Assert.Equal($"Línea {expectedLine}: «{token}» no sigue el formato {prefix}-n.", warning.Message);
    }

    [Fact]
    public void Ordinary_words_are_not_flagged_as_identifiers()
    {
        var markdown = CleanSpec(m => m.Replace("Texto.", "A la vez, sin cambios.\nQuiero una cosa.\nA-priori no hay datos.\nsm es mínimo.", StringComparison.Ordinal));

        Assert.Empty(CheckSpec(markdown));
    }

    [Fact]
    public void Duplicate_identifier_is_reported_once_with_both_lines()
    {
        var markdown = CleanSpec(m => m.Replace("- **FR-2**: Ver su estado", "- **FR-2**: Ver su estado\n- **FR-2**: Otra vez", StringComparison.Ordinal));
        var lines = markdown.Split('\n').ToList();
        var first = lines.FindIndex(l => l == "- **FR-2**: Ver su estado") + 1;

        var warning = Assert.Single(CheckSpec(markdown));

        Assert.Equal(first + 1, warning.Line);
        Assert.Equal($"Línea {first + 1}: FR-2 ya está definido en la línea {first}.", warning.Message);
    }

    [Fact]
    public void Assumption_marker_without_index_entry_is_reported()
    {
        var markdown = CleanSpec(m => m.Replace("Sin límites conocidos.\n\n##", "Sin límites conocidos. [ASSUMPTION: A-5] Otra.\n\n##", StringComparison.Ordinal));
        var line = markdown.Split('\n').ToList().FindIndex(l => l.Contains("A-5", StringComparison.Ordinal)) + 1;

        var warning = Assert.Single(CheckSpec(markdown));

        Assert.Equal(line, warning.Line);
        Assert.Equal($"Línea {line}: el supuesto A-5 no aparece en el Índice de suposiciones.", warning.Message);
    }

    [Fact]
    public void Index_entry_without_marker_is_reported()
    {
        var markdown = CleanSpec(m => m.Replace("- **A-1**: Sin límites conocidos.\n", "- **A-1**: Sin límites conocidos.\n- **A-2**: Huérfano\n", StringComparison.Ordinal));
        var line = markdown.Split('\n').ToList().FindIndex(l => l.StartsWith("- **A-2**", StringComparison.Ordinal)) + 1;

        var warning = Assert.Single(CheckSpec(markdown));

        Assert.Equal(line, warning.Line);
        Assert.Contains("A-2", warning.Message, StringComparison.Ordinal);
        Assert.Contains("[ASSUMPTION: A-2]", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[ASSUMPTION]")]
    [InlineData("[ASSUMPTION: A1]")]
    [InlineData("[ASSUMPTION: a-1]")]
    [InlineData("[ASSUMPTION: A-]")]
    [InlineData("[ASSUMPTION A-1")]
    public void Marker_without_a_valid_identifier_is_reported(string marker)
    {
        var markdown = CleanSpec(m => m.Replace("Sin límites conocidos.\n\n##", $"Sin límites conocidos. {marker}\n\n##", StringComparison.Ordinal));

        var warning = Assert.Single(CheckSpec(markdown));

        Assert.Contains("[ASSUMPTION]", warning.Message, StringComparison.Ordinal);
        Assert.Contains("A-n válido", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("- **Q-0**: Pregunta", "Q-0")]
    [InlineData("Texto [ASSUMPTION: A-0] nuevo.", "A-0")]
    public void Zero_numbered_identifiers_are_reported(string line, string id)
    {
        var markdown = CleanSpec(m => m.Replace("- **Q-1**: ¿Quién aprueba?", "- **Q-1**: ¿Quién aprueba?\n" + line, StringComparison.Ordinal));

        var warning = Assert.Single(CheckSpec(markdown));

        Assert.Contains($"«{id}» no es válido", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaps_and_retired_numbers_are_not_warnings()
    {
        var markdown = CleanSpec(m => m
            .Replace("- **FR-2**: Ver su estado", "- **FR-2**: (retirado)\n- **FR-4**: Exportar", StringComparison.Ordinal)
            .Replace("- **SM-1**:", "- **SM-3**:", StringComparison.Ordinal));

        Assert.Empty(CheckSpec(markdown));
    }

    [Fact]
    public void Mentions_inside_a_sentence_and_fenced_code_are_ignored()
    {
        var markdown = CleanSpec(m => m.Replace(
            "Texto.",
            "Ver FR1 y FR-9 en otro lugar.\n```\nFR1 sin guion\n## Glosario\n```",
            StringComparison.Ordinal));

        Assert.Empty(CheckSpec(markdown));
    }

    [Fact]
    public void Warnings_come_in_document_order_with_whole_document_findings_first()
    {
        var markdown = CleanSpec(m => m
            .Replace("## Restricciones", "Sin título", StringComparison.Ordinal)
            .Replace("- **FR-1**: Buscar un pedido", "FR1 Buscar\n- **SM-1**: uno", StringComparison.Ordinal)
            .Replace("- **Q-1**: ¿Quién aprueba?", "- **Q-1**: ¿Quién aprueba?\n- **Q-1**: otra", StringComparison.Ordinal));

        var warnings = CheckSpec(markdown);

        Assert.Equal(4, warnings.Count);
        Assert.Null(warnings[0].Line);
        Assert.Equal(warnings.Skip(1).Select(w => w.Line).OrderBy(l => l), warnings.Skip(1).Select(w => w.Line));
    }

    [Fact]
    public void Warnings_do_not_block_and_line_endings_do_not_matter()
    {
        var markdown = CleanSpec(m => m.Replace("- **FR-2**:", "FR2", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));

        var warnings = CheckSpec(markdown);

        Assert.Single(warnings);
    }

    [Fact]
    public void Without_an_index_only_the_missing_heading_is_reported()
    {
        var markdown = CleanSpec(m => m.Replace("## Índice de suposiciones", "## Otra cosa", StringComparison.Ordinal));

        var warning = Assert.Single(CheckSpec(markdown));

        Assert.Equal("Falta la sección «Índice de suposiciones».", warning.Message);
    }
}
