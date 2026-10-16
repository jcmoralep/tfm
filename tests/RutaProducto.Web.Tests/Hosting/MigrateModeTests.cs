using RutaProducto.Web.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace RutaProducto.Web.Tests.Hosting;

/// <remarks>
/// Covers argument detection and the exit-code mapping. The migration itself (<c>MigrateDatabaseAsync</c>) needs a MySQL
/// server and is left to the runtime verification.
/// </remarks>
public sealed class MigrateModeTests
{
    [Theory]
    [InlineData(true, "--migrate")]
    [InlineData(true, "--urls", "http://localhost:1", "--migrate")]
    [InlineData(false)]
    [InlineData(false, "--Migrate")]
    [InlineData(false, "migrate")]
    [InlineData(false, "--migrate-now")]
    public void The_mode_is_requested_only_by_the_exact_argument(bool expected, params string[] args)
    {
        Assert.Equal(expected, MigrateMode.IsRequested(args));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Success_exits_with_zero_whether_or_not_migrations_were_pending(int applied)
    {
        var migrations = Enumerable.Range(0, applied).Select(index => $"M{index}").ToList();

        var exitCode = await MigrateMode.RunAsync(() => Task.FromResult<IReadOnlyList<string>>(migrations), NullLogger.Instance);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Any_failure_exits_non_zero_instead_of_throwing()
    {
        var exitCode = await MigrateMode.RunAsync(
            () => throw new InvalidOperationException("database unreachable"),
            NullLogger.Instance);

        Assert.Equal(1, exitCode);
    }
}
