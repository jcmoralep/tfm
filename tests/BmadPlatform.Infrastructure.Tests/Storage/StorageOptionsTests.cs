using BmadPlatform.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace BmadPlatform.Infrastructure.Tests.Storage;

public sealed class StorageOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public void Local_is_the_default_and_is_read_case_insensitively(string? provider)
    {
        var options = StorageOptions.BindAndValidate(Config(("Storage:Provider", provider)));

        Assert.Equal("App_Data/attachments", options.LocalRootPath);
        Assert.Equal(10, options.MaxFileSizeMb);
        Assert.Equal(10L * 1024 * 1024, options.MaxFileBytes);
    }

    [Theory]
    [InlineData("S3", "not available yet")]
    [InlineData("s3", "not available yet")]
    [InlineData("Azure", "only Local")]
    public void S3_and_unknown_providers_fail_naming_the_setting_without_echoing_the_value(string provider, string reason)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => StorageOptions.BindAndValidate(Config(("Storage:Provider", provider))));

        Assert.Contains("Storage:Provider", error.Message);
        Assert.Contains(reason, error.Message);
        Assert.DoesNotContain("Azure", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("51")]
    [InlineData("abc")]
    [InlineData("")]
    public void An_invalid_max_size_fails_naming_the_setting(string size)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => StorageOptions.BindAndValidate(Config(("Storage:MaxFileSizeMb", size))));

        Assert.Contains("Storage:MaxFileSizeMb", error.Message);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("50", 50)]
    public void A_size_from_1_to_50_is_accepted(string size, int expected)
    {
        Assert.Equal(expected, StorageOptions.BindAndValidate(Config(("Storage:MaxFileSizeMb", size))).MaxFileSizeMb);
    }

    [Fact]
    public void The_local_root_can_be_overridden()
    {
        Assert.Equal("D:/files", StorageOptions.BindAndValidate(Config(("Storage:LocalRootPath", "D:/files"))).LocalRootPath);
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => KeyValuePair.Create(value.Key, value.Value)))
            .Build();
}
