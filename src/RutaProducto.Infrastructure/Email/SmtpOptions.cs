using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Email;

/// <summary>
/// SMTP settings under <c>Smtp:</c>, validated at startup when <c>Email:Provider</c> is Smtp. Failure messages name the
/// key or the environment variable and never echo a configured value (the password least of all).
/// </summary>
public sealed class SmtpOptions
{
    public const string DefaultFromName = "Ruta de Producto";

    public string Host { get; private set; } = string.Empty;

    public int Port { get; private set; } = 587;

    public bool EnableSsl { get; private set; } = true;

    public string? Username { get; private set; }

    public string? Password { get; private set; }

    public string FromAddress { get; private set; } = string.Empty;

    public string FromName { get; private set; } = DefaultFromName;

    public int TimeoutSeconds { get; private set; } = 30;

    /// <summary>Whether the host is the machine itself, where a plain-text connection never leaves it (local mail sinks).</summary>
    internal static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1";

    public static SmtpOptions BindAndValidate(IConfiguration configuration)
    {
        var options = new SmtpOptions { Host = (configuration["Smtp:Host"] ?? string.Empty).Trim() };

        if (options.Host.Length == 0)
        {
            throw new InvalidOperationException("Email:Provider is Smtp but the host is not configured. Set Smtp__Host.");
        }

        options.Port = ReadInteger(configuration["Smtp:Port"], "Smtp:Port", defaultValue: 587, min: 1, max: 65535);

        if (options.Port == 465)
        {
            throw new InvalidOperationException(
                "'Smtp:Port' is 465: implicit TLS is not supported by System.Net.Mail; use 587 (STARTTLS).");
        }

        options.EnableSsl = ReadBoolean(configuration["Smtp:EnableSsl"], "Smtp:EnableSsl", defaultValue: true);

        if (!options.EnableSsl && !IsLocalHost(options.Host))
        {
            throw new InvalidOperationException(
                "'Smtp:EnableSsl' is false but 'Smtp:Host' is not localhost or 127.0.0.1; " +
                "plain-text SMTP is only allowed for a local mail sink.");
        }

        options.Username = NullIfBlank(configuration["Smtp:Username"]);
        options.Password = NullIfBlank(configuration["Smtp:Password"]);

        if (options.Username is not null && options.Password is null)
        {
            throw new InvalidOperationException(
                "'Smtp:Username' is set but the password is not configured. " +
                "Set Smtp__Password (or the user-secret Smtp:Password).");
        }

        options.FromAddress = ReadFromAddress(configuration["Smtp:From"]);
        options.FromName = NullIfBlank(configuration["Smtp:FromName"]) ?? DefaultFromName;
        options.TimeoutSeconds = ReadInteger(configuration["Smtp:TimeoutSeconds"], "Smtp:TimeoutSeconds", defaultValue: 30, min: 1, max: 120);

        return options;
    }

    private static string ReadFromAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Email:Provider is Smtp but the sender address is not configured. Set Smtp__From.");
        }

        try
        {
            return new MailAddress(value.Trim()).Address;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("'Smtp:From' (Smtp__From) is not a valid email address.");
        }
    }

    internal static int ReadInteger(string? value, string key, int defaultValue, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (int.TryParse(value.Trim(), out var number) && number >= min && number <= max)
        {
            return number;
        }

        throw new InvalidOperationException($"'{key}' must be an integer from {min} to {max}.");
    }

    internal static bool ReadBoolean(string? value, string key, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value.Trim(), out var flag))
        {
            return flag;
        }

        throw new InvalidOperationException($"'{key}' must be true or false.");
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
