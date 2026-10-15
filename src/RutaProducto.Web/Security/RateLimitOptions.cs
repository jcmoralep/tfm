using System.Globalization;

namespace RutaProducto.Web.Security;

/// <summary>
/// Settings under <c>RateLimiting:</c>, validated at startup. The limiter is off by default. The numbers are checked
/// only when it is enabled. Failure messages name the key and never echo a configured value.
/// </summary>
internal sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public const int DefaultPermitLimit = 10;

    public const int MaxPermitLimit = 1000;

    public const int DefaultWindowSeconds = 60;

    public const int MaxWindowSeconds = 3600;

    public bool Enabled { get; private set; }

    public int PermitLimit { get; private set; } = DefaultPermitLimit;

    public int WindowSeconds { get; private set; } = DefaultWindowSeconds;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    public static RateLimitOptions BindAndValidate(IConfiguration configuration)
    {
        var options = new RateLimitOptions();

        var enabled = configuration[Key("Enabled")];

        if (!string.IsNullOrWhiteSpace(enabled))
        {
            options.Enabled = bool.TryParse(enabled.Trim(), out var value)
                ? value
                : throw new InvalidOperationException("'RateLimiting:Enabled' must be true or false.");
        }

        if (!options.Enabled)
        {
            return options;
        }

        options.PermitLimit = ReadInteger(configuration, "PermitLimit", DefaultPermitLimit, MaxPermitLimit);
        options.WindowSeconds = ReadInteger(configuration, "WindowSeconds", DefaultWindowSeconds, MaxWindowSeconds);

        return options;
    }

    private static int ReadInteger(IConfiguration configuration, string name, int defaultValue, int max)
    {
        var raw = configuration[Key(name)];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 1 && value <= max
            ? value
            : throw new InvalidOperationException($"'{Key(name)}' must be an integer from 1 to {max}.");
    }

    private static string Key(string name) => $"{SectionName}:{name}";
}
