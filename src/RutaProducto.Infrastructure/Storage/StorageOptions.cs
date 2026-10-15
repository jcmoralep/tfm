using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Storage;

/// <summary>
/// Settings under <c>Storage:</c>, validated at startup. Only the local provider exists for now. Failure messages
/// name the option and never echo a configured value.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public const string DefaultLocalRootPath = "App_Data/attachments";

    public const int DefaultMaxFileSizeMb = 10;

    public const int MaxFileSizeMbLimit = 50;

    public string LocalRootPath { get; private set; } = DefaultLocalRootPath;

    public int MaxFileSizeMb { get; private set; } = DefaultMaxFileSizeMb;

    public long MaxFileBytes => MaxFileSizeMb * 1024L * 1024L;

    public static StorageOptions BindAndValidate(IConfiguration configuration)
    {
        var provider = configuration[Key("Provider")];

        if (!string.IsNullOrWhiteSpace(provider))
        {
            if (provider.Trim().Equals("S3", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("'Storage:Provider' is S3, which is not available yet; use Local.");
            }

            if (!provider.Trim().Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("'Storage:Provider' has an unknown value; only Local is available.");
            }
        }

        var options = new StorageOptions();

        var root = configuration[Key("LocalRootPath")];

        if (!string.IsNullOrWhiteSpace(root))
        {
            options.LocalRootPath = root.Trim();
        }

        var size = configuration[Key("MaxFileSizeMb")];

        if (!string.IsNullOrWhiteSpace(size))
        {
            options.MaxFileSizeMb = int.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var megabytes)
                && megabytes is >= 1 and <= MaxFileSizeMbLimit
                    ? megabytes
                    : throw new InvalidOperationException("'Storage:MaxFileSizeMb' must be an integer from 1 to 50.");
        }

        return options;
    }

    private static string Key(string name) => $"{SectionName}:{name}";
}
