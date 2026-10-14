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

        await AssertGolden(file, markdown);
        Assert.Empty(ArtifactFormatChecker.Check(type, markdown));
        Assert.Equal(["Q-1"], OpenQuestions.Unassigned(markdown));
    }

    // The Architecture reads the approved PRD golden and no answers; its questions are pre-assigned to development.
    [Fact]
    public async Task The_architecture_equals_its_golden_and_passes_every_check()
    {
        var prd = await File.ReadAllTextAsync(GoldenPath("prd-large.md"));
        var request = Request(ArtifactType.Architecture, InitiativeDepth.Large, []) with { PrdMarkdown = prd };
        var markdown = await Draft(request);

        await AssertGolden("architecture-large.md", markdown);
        Assert.Equal(markdown, await Draft(request));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.Architecture, markdown));
        Assert.Empty(OpenQuestions.Unassigned(markdown));
        Assert.All(OpenQuestions.Parse(markdown), question => Assert.Equal("Equipo de desarrollo", question.Owner));
        Assert.DoesNotMatch(@"\b(TBD|TODO|FIXME|XXX)\b", markdown);
        Assert.Equal(
            Enumerable.Range(1, ArchitectureDecisionItems.Parse(markdown).Count).Select(n => $"AD-{n}"),
            ArchitectureDecisionItems.Parse(markdown).Select(decision => decision.Id));
    }

    // The Epics read the approved PRD and Architecture goldens and no answers; every requirement is covered or asked about.
    [Fact]
    public async Task The_epics_equal_their_golden_and_pass_every_check()
    {
        var request = Request(ArtifactType.EpicsAndStories, InitiativeDepth.Large, []) with
        {
            PrdMarkdown = await File.ReadAllTextAsync(GoldenPath("prd-large.md")),
            ArchitectureMarkdown = await File.ReadAllTextAsync(GoldenPath("architecture-large.md")),
        };
        var markdown = await Draft(request);

        await AssertGolden("epics-large.md", markdown);
        Assert.Equal(markdown, await Draft(request));
        Assert.Empty(ArtifactFormatChecker.Check(ArtifactType.EpicsAndStories, markdown));
        Assert.Empty(OpenQuestions.Unassigned(markdown));
        Assert.DoesNotMatch(@"\b(TBD|TODO|FIXME|XXX)\b", markdown);
        Assert.Equal(
            ["FR-1", "FR-2", "FR-3", "FR-4", "FR-5", "NFR-1", "NFR-2"],
            RequirementItems.Parse(markdown).Select(item => item.Id));
    }

    private static async Task AssertGolden(string file, string markdown)
    {
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
    }
}
