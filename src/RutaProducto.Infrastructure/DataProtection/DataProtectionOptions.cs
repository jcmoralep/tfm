using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.DataProtection;

/// <summary>Where the Data Protection keys live (<c>DataProtection:Provider</c>).</summary>
public enum KeyStoreProvider
{
    Default,
    Database,
}

/// <summary>
/// Setting under <c>DataProtection:</c>, validated at startup. Empty means <see cref="KeyStoreProvider.Default"/>
/// (the framework's own key ring: the container file system). The failure message lists the valid values and never echoes the value.
/// </summary>
public sealed class DataProtectionOptions
{
    public const string ProviderKey = "DataProtection:Provider";

    public KeyStoreProvider Provider { get; private set; } = KeyStoreProvider.Default;

    public static DataProtectionOptions BindAndValidate(IConfiguration configuration)
    {
        var value = configuration[ProviderKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            return new DataProtectionOptions();
        }

        if (Enum.TryParse<KeyStoreProvider>(value.Trim(), ignoreCase: true, out var provider) && Enum.IsDefined(provider))
        {
            return new DataProtectionOptions { Provider = provider };
        }

        throw new InvalidOperationException(
            $"'{ProviderKey}' has an unknown value. Valid values are: Default, Database. Set DataProtection__Provider or leave it empty.");
    }
}
