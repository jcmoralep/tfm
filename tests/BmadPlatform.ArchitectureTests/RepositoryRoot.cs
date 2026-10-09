namespace BmadPlatform.ArchitectureTests;

/// <summary>Finds the repository folder from the test output, so tests can read project files and sources.</summary>
internal static class RepositoryRoot
{
    private static readonly Lazy<string> Located = new(() =>
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

    public static string FullName => Located.Value;
}
