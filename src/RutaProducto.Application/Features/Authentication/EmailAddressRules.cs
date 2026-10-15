using FluentValidation;

namespace RutaProducto.Application.Features.Authentication;

public static class EmailAddressRules
{
    public const string AsciiOnlyMessage = "El correo electrónico solo puede llevar letras sin acentos, números y los símbolos habituales.";

    /// <summary>
    /// Rejects any non-ASCII character. MySQL's default collation is accent-insensitive, so an accent variant of an
    /// address would otherwise match another person's account. The message never says whether an account exists.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeAsciiAddress<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(email => email is null || email.All(character => character <= '\u007F')).WithMessage(AsciiOnlyMessage);
}
