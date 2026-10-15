using Microsoft.Extensions.Configuration;

namespace RutaProducto.Infrastructure.Registration;

/// <summary>
/// Setting under <c>Registration:</c>, validated at startup. An empty or missing code means the feature is off.
/// Failure messages name the key and never echo the value.
/// </summary>
public sealed class InviteCodeOptions
{
    public const string InviteCodeKey = "Registration:InviteCode";

    public const int MinLength = 12;

    public string? InviteCode { get; private set; }

    public static InviteCodeOptions BindAndValidate(IConfiguration configuration)
    {
        var value = configuration[InviteCodeKey]?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return new InviteCodeOptions();
        }

        if (value.Length < MinLength)
        {
            throw new InvalidOperationException(
                $"'{InviteCodeKey}' is too short: it needs at least {MinLength} characters. " +
                "Set Registration__InviteCode (or the user-secret Registration:InviteCode), or leave it empty to turn it off.");
        }

        return new InviteCodeOptions { InviteCode = value };
    }
}
