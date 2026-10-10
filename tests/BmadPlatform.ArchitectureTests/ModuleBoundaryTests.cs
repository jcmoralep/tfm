using System.Text.RegularExpressions;

namespace BmadPlatform.ArchitectureTests;

/// <summary>
/// Enforces the module isolation of AGENTS.md §4.1: a module does not use the entities, repositories, tables or
/// infrastructure of another one. Modules talk through MediatR requests, DTOs, the shared texts and enums.
/// Web is excluded because it composes the modules.
/// </summary>
public sealed class ModuleBoundaryTests
{
    // The entity type, not a member that merely carries the name (a DTO property "Initiative" or "request.Initiative.Depth").
    private const string InitiativeEntityToken = @"(?<!\.)\bInitiative\b(?!\s*[,;)])";

    private static readonly Module[] Modules =
    [
        new(
            "Initiatives",
            ["Domain/Initiatives", "Application/Features/Initiatives", "Infrastructure/Initiatives"],
            [InitiativeEntityToken, @"\bIInitiativeRepository\b", @"Infrastructure\.Initiatives", @"\bini_"]),
        new(
            "Assistant",
            ["Domain/Assistant", "Application/Features/Assistant", "Infrastructure/Assistant"],
            [@"\bConversation\b", @"\bIConversationRepository\b", @"Infrastructure\.Assistant", @"\basi_"]),
    ];

    public static TheoryData<string, string> ModulePairs()
    {
        var pairs = new TheoryData<string, string>();

        foreach (var module in Modules)
        {
            foreach (var other in Modules.Where(candidate => candidate != module))
            {
                pairs.Add(module.Name, other.Name);
            }
        }

        return pairs;
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void A_module_does_not_use_the_private_surface_of_another(string moduleName, string otherName)
    {
        var module = Modules.Single(candidate => candidate.Name == moduleName);
        var other = Modules.Single(candidate => candidate.Name == otherName);

        var violations = FindViolations(SourcesOf(module), other.PrivateTokens);

        Assert.True(violations.Count == 0, $"{module.Name} must not reference {other.Name} internals:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Theory]
    [InlineData("Initiatives")]
    [InlineData("Assistant")]
    public void Every_module_has_sources_in_each_layer_so_the_scan_cannot_pass_vacuously(string moduleName)
    {
        var module = Modules.Single(candidate => candidate.Name == moduleName);

        Assert.All(module.Folders, folder => Assert.NotEmpty(SourcesOf(module with { Folders = [folder] })));
    }

    [Fact]
    public void A_forbidden_token_in_code_is_reported_but_the_same_text_in_a_comment_is_not()
    {
        var sources = new[]
        {
            new Source("Bad.cs", "var x = new Conversation();"),
            new Source("Comment.cs", "// uses Conversation here\n/// <see cref=\"Conversation\"/>\nvar y = 1;"),
            new Source("Fine.cs", "var z = new ConversationView();"),
        };

        var violations = FindViolations(sources, [@"\bConversation\b"]);

        var violation = Assert.Single(violations);
        Assert.Contains("Bad.cs", violation);
    }

    [Fact]
    public void The_initiative_token_matches_the_entity_type_but_not_a_dto_property_named_after_it()
    {
        var sources = new[]
        {
            new Source("Entity.cs", "var x = Initiative.CreateDraft();"),
            new Source("Param.cs", "void Do(Initiative initiative) { }"),
            new Source("Dto.cs", "record Request(InitiativeSnapshot Initiative, int Level);\nvar d = request.Initiative.Depth;"),
        };

        var violations = FindViolations(sources, [InitiativeEntityToken]);

        Assert.Equal(["Entity.cs", "Param.cs"], violations.Select(violation => violation.Split(' ')[0]));
    }

    private static List<string> FindViolations(IEnumerable<Source> sources, IReadOnlyList<string> forbiddenTokens)
    {
        var violations = new List<string>();

        foreach (var source in sources)
        {
            var code = Regex.Replace(source.Content, @"//[^\r\n]*", string.Empty);

            foreach (var token in forbiddenTokens.Where(token => Regex.IsMatch(code, token)))
            {
                violations.Add($"{source.Path} matches {token}");
            }
        }

        return violations;
    }

    private static List<Source> SourcesOf(Module module) =>
        module.Folders
            .Select(folder => Path.Combine(RepositoryRoot.FullName, "src", $"BmadPlatform.{folder.Split('/')[0]}", string.Join(Path.DirectorySeparatorChar, folder.Split('/')[1..])))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Select(file => new Source(file, File.ReadAllText(file)))
            .ToList();

    private sealed record Module(string Name, string[] Folders, string[] PrivateTokens);

    private sealed record Source(string Path, string Content);
}
