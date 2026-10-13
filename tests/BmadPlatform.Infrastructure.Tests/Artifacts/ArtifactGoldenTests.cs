using System.Runtime.CompilerServices;
using System.Text;
using BmadPlatform.Application.Features.Artifacts.Markdown;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Domain.Initiatives;
using static BmadPlatform.Infrastructure.Tests.Artifacts.DraftSamples;

namespace BmadPlatform.Infrastructure.Tests.Artifacts;

/// <summary>
/// Byte-exact outputs of the fake drafter for fixed conversations. Set <c>UPDATE_GOLDENS=1</c> to rewrite the
/// files after an intended change, then review the diff. The files use LF (see .gitattributes).
/// </summary>
public sealed class ArtifactGoldenTests
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private static string GoldenPath(string file, [CallerFilePath] string testFile = "") =>
        Path.Combine(Path.GetDirectoryName(testFile)!, "Goldens", file);

    private static Dictionary<string, string> Texts() =>
        new()
        {
            [AssistantScript.Keys.Idea] = "Que el equipo de ventas consulte el estado de un pedido sin llamar a bodega.",
            [AssistantScript.Keys.Users] = "Las vendedoras de las tiendas\nEl personal de bodega",
            [AssistantScript.Keys.Problem] = "Hoy cada consulta toma diez minutos por teléfono y los clientes se quejan de la demora.",
            [AssistantScript.Keys.Success] = "Que ninguna vendedora tenga que llamar a bodega\nQue bajen las quejas por demoras",
            [AssistantScript.Keys.OutOfScope] = "No incluye pagos ni devoluciones.",
            [AssistantScript.Keys.Capabilities] = "Buscar un pedido por su número\n- Ver en qué estado está\n3. Recibir un aviso cuando cambie",
            [AssistantScript.Keys.Constraints] = "Debe funcionar desde el celular\nNo puede mostrar datos de otros clientes",
            [AssistantScript.Keys.Priorities] = "Primero consultar el estado y, después, recibir avisos.",
            [AssistantScript.Keys.Integrations] = "El sistema de inventario\nEl equipo de logística",
            [AssistantScript.Keys.Qualities] = "Debe responder en pocos segundos y pedir inicio de sesión.",
        };

    public static TheoryData<string, ArtifactType, InitiativeDepth, string> Goldens() => new()
    {
        { "spec-small.md", ArtifactType.Spec, InitiativeDepth.Small, AssistantScript.Keys.OutOfScope },
        { "brief-standard.md", ArtifactType.Brief, InitiativeDepth.Standard, AssistantScript.Keys.Success },
        { "prd-standard.md", ArtifactType.Prd, InitiativeDepth.Standard, AssistantScript.Keys.Constraints },
        { "prd-large.md", ArtifactType.Prd, InitiativeDepth.Large, AssistantScript.Keys.Qualities },
    };

    [Theory]
    [MemberData(nameof(Goldens))]
    public async Task The_output_equals_its_golden(string file, ArtifactType type, InitiativeDepth depth, string unknownKey)
    {
        var answers = Answers(depth, Texts(), [unknownKey]);
        var markdown = await Draft(Request(type, depth, answers));
        var expected = Utf8.GetBytes(markdown + "\n");
        var path = GoldenPath(file);

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, expected);
        }

        var stored = await File.ReadAllBytesAsync(path);

        Assert.DoesNotContain((byte)'\r', stored);
        Assert.Equal(Utf8.GetString(expected), Utf8.GetString(stored));
        Assert.Empty(ArtifactFormatChecker.Check(type, markdown));
        Assert.Equal(["Q-1"], OpenQuestions.Unassigned(markdown));
    }
}
