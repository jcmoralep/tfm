using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BmadPlatform.Application.Features.Artifacts.ExportArtifact;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Infrastructure.Export;

/// <summary>
/// Delivers the stored Markdown as a <c>.md</c> file for developers (RF-21, RF-39). The document body is copied as it
/// is; the exporter only adds the BMAD frontmatter (title, status, created, updated; RF-36) and a short usage note
/// that says where the file goes and which BMAD skill picks it up. Dates are the server's local ones, like the rest
/// of the platform.
/// </summary>
internal sealed partial class MarkdownArtifactExporter : IArtifactExporter
{
    private const int MaxSlugLength = 40;

    private const string ContentType = "text/markdown; charset=utf-8";

    public ArtifactExport Export(ArtifactExportSource source)
    {
        var date = source.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var slug = Slug(source.InitiativeName);

        var text = new StringBuilder()
            .Append("---\n")
            .Append("title: ").Append(Quote(source.InitiativeName)).Append('\n')
            .Append("status: ").Append(source.Status == ArtifactStatus.Approved ? "approved" : "draft").Append('\n')
            .Append("created: ").Append(source.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\n')
            .Append("updated: ").Append(date).Append('\n')
            .Append("---\n\n")
            .Append(UsageNote(source)).Append("\n\n")
            .Append(source.Content.TrimEnd()).Append('\n')
            .ToString();

        return new ArtifactExport($"{FilePrefix(source.Type)}-{slug}-{date}.md", ContentType, text);
    }

    private static string FilePrefix(ArtifactType type) => type switch
    {
        ArtifactType.Spec => "spec",
        ArtifactType.Brief => "brief",
        ArtifactType.Prd => "prd",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    // The folder and skill names come from docs/bmad-referencia.md (BMAD 6.12.1). The note is a blockquote so it is
    // easy to see and easy to delete; the document below it is untouched.
    private static string UsageNote(ArtifactExportSource source)
    {
        var where = source.Type switch
        {
            ArtifactType.Brief =>
                "Este es el Brief de la iniciativa, el punto de partida del PRD en BMAD. Para usarlo, guárdelo en su repositorio de desarrollo como `planning-artifacts/briefs/brief-<proyecto>-<fecha>/brief.md` y pida a `bmad-prd` que lo tome como entrada.",
            ArtifactType.Prd =>
                "Este es el PRD de la iniciativa. Para usarlo, guárdelo en su repositorio de desarrollo como `planning-artifacts/prds/prd-<proyecto>-<fecha>/prd.md`; los pasos siguientes de BMAD son la arquitectura (`bmad-architecture`) y las épicas e historias (`bmad-create-epics-and-stories`).",
            ArtifactType.Spec =>
                "Esta es la especificación de la iniciativa. Para construirla, guárdela en su repositorio de desarrollo como `specs/spec-<proyecto>/SPEC.md` y pase esa carpeta a `bmad-build`.",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.Type, null),
        };

        var status = source.Status == ArtifactStatus.Approved
            ? string.Empty
            : " El documento está en borrador: la persona de producto todavía no lo aprobó.";

        return $"> **Nota de uso** (la agrega la plataforma; bórrela si no la necesita). {where}{status}";
    }

    /// <summary>Lower-case ASCII words joined by hyphens, so the name is a safe file name; at most 40 characters.</summary>
    internal static string Slug(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder();

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(character);
            }
        }

        var slug = NotAlphanumeric().Replace(plain.ToString().ToLowerInvariant(), "-").Trim('-');

        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength].TrimEnd('-');
        }

        return slug.Length == 0 ? "iniciativa" : slug;
    }

    // A YAML double-quoted scalar: backslash and quote are escaped, and any control character becomes a space.
    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                _ when char.IsControl(character) => " ",
                _ => character.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex NotAlphanumeric();
}
