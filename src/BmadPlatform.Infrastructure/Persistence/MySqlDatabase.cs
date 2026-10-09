using Microsoft.EntityFrameworkCore;

namespace BmadPlatform.Infrastructure.Persistence;

/// <summary>
/// MySQL settings shared by the runtime registration and the design-time factory.
/// The server version is explicit so neither startup nor migration authoring needs to query the server.
/// This is the only place that knows which database engine is used, so switching hosts
/// (local Docker, Railway MySQL) is a configuration change.
/// </summary>
public static class MySqlDatabase
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Configuration key for the MySQL server version (environment variable: Database__MySqlServerVersion).
    /// It must match the server the app connects to: the docker-compose.yml image locally,
    /// or the image version of the MySQL service on Railway.
    /// </summary>
    public const string ServerVersionKey = "Database:MySqlServerVersion";

    public const string DefaultServerVersion = "8.4.0";

    /// <summary>Explicit command timeout, so a stuck query fails in a known time instead of relying on the driver default.</summary>
    public const int CommandTimeoutSeconds = 30;

    public static ServerVersion ParseServerVersion(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? DefaultServerVersion : value.Trim();

        if (!Version.TryParse(text, out var version))
        {
            throw new InvalidOperationException(
                $"'{ServerVersionKey}' must be a MySQL version such as 8.0.36 or 8.4.0, but was '{text}'.");
        }

        return new MySqlServerVersion(version);
    }

    public static DbContextOptionsBuilder UseApplicationMySql(
        this DbContextOptionsBuilder builder,
        string connectionString,
        ServerVersion serverVersion) =>
        builder.UseMySql(
            connectionString,
            serverVersion,
            mySql =>
            {
                mySql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                mySql.CommandTimeout(CommandTimeoutSeconds);
            });
}
