using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BmadPlatform.Infrastructure.Persistence;

/// <summary>
/// Used by the EF Core tools (<c>dotnet ef</c>). Because the server version is explicit,
/// adding a migration never opens a connection. Commands that do reach the database
/// (for example <c>dotnet ef database update</c>) read the same environment variable as the app.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__" + MySqlDatabase.ConnectionStringName;
    private const string ServerVersionVariable = "Database__MySqlServerVersion";

    // Not a credential: only used to build the model when no connection string is configured.
    private const string PlaceholderConnectionString = "Server=localhost;Port=3306;Database=bmadplatform;User=design-time";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        var serverVersion = MySqlDatabase.ParseServerVersion(Environment.GetEnvironmentVariable(ServerVersionVariable));

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseApplicationMySql(
            string.IsNullOrWhiteSpace(connectionString) ? PlaceholderConnectionString : connectionString,
            serverVersion);

        return new ApplicationDbContext(builder.Options);
    }
}
