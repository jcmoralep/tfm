namespace BmadPlatform.Application.Abstractions.Authentication;

/// <summary>
/// The password rules people are told about. Identity enforces them (see the options set in Infrastructure); the
/// registration page lists them and checks them live as the person types. Changing a rule means changing all three:
/// the Identity options, the Spanish messages and the list on the page. A test keeps the options and this class together.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>Minimum length. The page also asks for an upper-case letter, a lower-case letter, a digit and a symbol.</summary>
    public const int MinLength = 6;
}
