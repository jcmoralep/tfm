namespace RutaProducto.Web.Hosting;

/// <summary>
/// <c>dotnet RutaProducto.Web.dll --migrate</c>: applies the pending EF Core migrations and exits, for the platform's pre-deploy
/// command. It never starts Kestrel and never seeds users. Normal startup does not migrate.
/// </summary>
internal static class MigrateMode
{
    public const string Argument = "--migrate";

    public const int Success = 0;

    public const int Failure = 1;

    public static bool IsRequested(IEnumerable<string> args) =>
        args.Any(argument => string.Equals(argument, Argument, StringComparison.Ordinal));

    /// <returns>The process exit code: 0 when the migrations were applied (or none were pending), 1 otherwise.</returns>
    public static async Task<int> RunAsync(Func<Task<IReadOnlyList<string>>> migrate, ILogger logger)
    {
        try
        {
            var applied = await migrate();

            logger.LogInformation("Migration mode finished: {AppliedCount} migration(s) applied", applied.Count);

            return Success;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Migration mode failed; the database was not fully migrated");

            return Failure;
        }
    }
}
