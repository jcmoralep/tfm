using System.Reflection;
using System.Xml.Linq;
using BmadPlatform.Domain;

namespace BmadPlatform.ArchitectureTests;

/// <summary>
/// Enforces the dependency rules of AGENTS.md §3:
/// Web -> Application -> Domain, Infrastructure -> Application -> Domain, and Domain depends on nothing.
/// Project references are read from the .csproj files (declared dependencies) and assembly references
/// from the compiled output (dependencies actually used, including NuGet packages).
/// </summary>
public sealed class LayerDependencyTests
{
    private const string Domain = "BmadPlatform.Domain";
    private const string Application = "BmadPlatform.Application";
    private const string Infrastructure = "BmadPlatform.Infrastructure";
    private const string Web = "BmadPlatform.Web";

    [Fact]
    public void Domain_has_no_project_references()
    {
        Assert.Empty(ProjectReferencesOf(Domain));
    }

    [Fact]
    public void Domain_has_no_package_references()
    {
        Assert.Empty(PackageReferencesOf(Domain));
    }

    [Fact]
    public void Domain_assembly_only_references_the_base_class_library()
    {
        var references = typeof(DomainAssembly).Assembly.GetReferencedAssemblies().Select(name => name.Name!);

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name is "netstandard" or "mscorlib",
            $"Domain must not reference '{name}'."));
    }

    [Fact]
    public void Application_references_only_the_domain()
    {
        Assert.Equal([Domain], ProjectReferencesOf(Application));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Serilog")]
    public void Application_assembly_does_not_use_infrastructure_frameworks(string forbiddenPrefix)
    {
        var references = ApplicationAssembly.GetReferencedAssemblies().Select(name => name.Name!);

        Assert.DoesNotContain(references, name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_references_only_the_application()
    {
        Assert.Equal([Application], ProjectReferencesOf(Infrastructure));
    }

    [Theory]
    [InlineData(Domain)]
    [InlineData(Application)]
    [InlineData(Infrastructure)]
    public void No_layer_references_the_web_project(string project)
    {
        Assert.DoesNotContain(Web, ProjectReferencesOf(project));
    }

    private static Assembly ApplicationAssembly => typeof(BmadPlatform.Application.DependencyInjection).Assembly;

    private static string[] ProjectReferencesOf(string project) =>
        LoadProject(project)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] PackageReferencesOf(string project) =>
        LoadProject(project)
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")!.Value)
            .ToArray();

    private static XDocument LoadProject(string project) =>
        XDocument.Load(Path.Combine(RepositoryRoot.Value, "src", project, $"{project}.csproj"));

    private static readonly Lazy<string> RepositoryRoot = new(() =>
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BmadPlatform.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (BmadPlatform.slnx) not found.");
    });
}
