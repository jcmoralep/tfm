using Microsoft.Extensions.Configuration;

namespace RutaProducto.Web.Tests.Security;

/// <summary>The committed settings files only carry placeholders: secrets come from user-secrets or the environment.</summary>
public sealed class AppSettingsSecretsTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void The_turnstile_secret_is_never_committed(string file)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(PathOf(file), optional: false).Build();

        Assert.True(string.IsNullOrEmpty(configuration["Turnstile:SecretKey"]));
    }

    private static string PathOf(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RutaProducto.slnx")))
            {
                return Path.Combine(directory.FullName, "src", "RutaProducto.Web", file);
            }
        }

        throw new FileNotFoundException("The solution root was not found above the test output.");
    }
}
