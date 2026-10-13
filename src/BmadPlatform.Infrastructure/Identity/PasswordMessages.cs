using BmadPlatform.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace BmadPlatform.Infrastructure.Identity;

/// <summary>Spanish messages for the Identity password rules (the platform has no localization).</summary>
internal static class PasswordMessages
{
    public static IReadOnlyList<string> From(IEnumerable<IdentityError> errors) =>
        errors.Select(error => error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) => $"La contraseña debe tener al menos {PasswordPolicy.MinLength} caracteres.",
            nameof(IdentityErrorDescriber.PasswordRequiresUpper) => "La contraseña debe incluir una letra mayúscula.",
            nameof(IdentityErrorDescriber.PasswordRequiresLower) => "La contraseña debe incluir una letra minúscula.",
            nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "La contraseña debe incluir un número.",
            nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) => "La contraseña debe incluir un símbolo, por ejemplo ! o #.",
            nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) => "La contraseña debe usar más caracteres distintos.",
            _ => "La contraseña no cumple los requisitos.",
        })
        .Distinct()
        .ToList();
}
