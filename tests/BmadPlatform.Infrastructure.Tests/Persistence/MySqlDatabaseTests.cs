using BmadPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BmadPlatform.Infrastructure.Tests.Persistence;

public sealed class MySqlDatabaseTests
{
    [Fact]
    public void UseApplicationMySql_SetsAnExplicitCommandTimeout()
    {
        var builder = new DbContextOptionsBuilder()
            .UseApplicationMySql("Server=localhost;Database=test", new MySqlServerVersion(new Version(8, 4, 0)));

        var relational = builder.Options.Extensions.OfType<RelationalOptionsExtension>().Single();

        Assert.Equal(MySqlDatabase.CommandTimeoutSeconds, relational.CommandTimeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseServerVersion_WithoutValue_UsesTheDefault(string? value)
    {
        var version = Assert.IsType<MySqlServerVersion>(MySqlDatabase.ParseServerVersion(value));

        Assert.Equal(Version.Parse(MySqlDatabase.DefaultServerVersion), version.Version);
    }

    [Theory]
    [InlineData("8.0", 8, 0)]
    [InlineData("8.0.36", 8, 0)]
    [InlineData(" 8.4.0 ", 8, 4)]
    public void ParseServerVersion_WithValidVersion_ReturnsThatVersion(string value, int major, int minor)
    {
        var version = Assert.IsType<MySqlServerVersion>(MySqlDatabase.ParseServerVersion(value));

        Assert.Equal(major, version.Version.Major);
        Assert.Equal(minor, version.Version.Minor);
    }

    [Theory]
    [InlineData("8")]
    [InlineData("latest")]
    [InlineData("8.x")]
    public void ParseServerVersion_WithInvalidVersion_FailsWithAClearMessage(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MySqlDatabase.ParseServerVersion(value));

        Assert.Contains(MySqlDatabase.ServerVersionKey, exception.Message);
    }
}
