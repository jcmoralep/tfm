using System.Globalization;

namespace RutaProducto.Web.Hosting;

/// <summary>
/// Railway injects the port to listen on as <c>PORT</c>. This turns it into a Kestrel URL, unless the host already has
/// explicit URLs (<c>ASPNETCORE_URLS</c> or <c>urls</c>), which always win. Failure messages name the variable and never echo the value.
/// </summary>
internal static class PortSetting
{
    public const string VariableName = "PORT";

    /// <returns>The URL to listen on, or <c>null</c> when <c>PORT</c> is absent or the host already has URLs.</returns>
    public static string? ResolveUrl(string? port, string? configuredUrls)
    {
        if (!string.IsNullOrWhiteSpace(configuredUrls) || string.IsNullOrWhiteSpace(port))
        {
            return null;
        }

        if (!int.TryParse(port.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535)
        {
            throw new InvalidOperationException($"'{VariableName}' must be a port number between 1 and 65535.");
        }

        return $"http://0.0.0.0:{number.ToString(CultureInfo.InvariantCulture)}";
    }
}
