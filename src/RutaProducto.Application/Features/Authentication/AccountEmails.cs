using RutaProducto.Application.Abstractions.Email;

namespace RutaProducto.Application.Features.Authentication;

/// <summary>Plain-text Spanish builders for the account emails. Links are passed in already built from the configured public base URL.</summary>
public static class AccountEmails
{
    public const string ConfirmEmailSubject = "Confirma tu correo en Ruta de Producto";

    public const string AccountExistsSubject = "Ya tienes una cuenta en Ruta de Producto";

    public const string ResetPasswordSubject = "Restablece tu contraseña de Ruta de Producto";

    public static EmailMessage ConfirmEmail(string to, Uri confirmationLink) =>
        new(
            to,
            EmailKind.ConfirmEmail,
            ConfirmEmailSubject,
            $"""
            Hola:

            Creaste una cuenta en Ruta de Producto. Para confirmar tu correo abre este enlace (vale 24 horas):

            {confirmationLink.AbsoluteUri}

            Si no fuiste tú, ignora este correo.
            """);

    public static EmailMessage AccountExists(string to, Uri loginLink, Uri forgotPasswordLink) =>
        new(
            to,
            EmailKind.AccountExists,
            AccountExistsSubject,
            $"""
            Hola:

            Alguien intentó crear una cuenta en Ruta de Producto con este correo, pero ya tienes una.

            Para entrar: {loginLink.AbsoluteUri}

            Si olvidaste tu contraseña: {forgotPasswordLink.AbsoluteUri}

            Si no fuiste tú, ignora este correo.
            """);

    public static EmailMessage ResetPassword(string to, Uri resetLink) =>
        new(
            to,
            EmailKind.ResetPassword,
            ResetPasswordSubject,
            $"""
            Hola:

            Recibimos una solicitud para restablecer tu contraseña de Ruta de Producto. Abre este enlace para elegir una nueva (vale 2 horas):

            {resetLink.AbsoluteUri}

            Si no fuiste tú, ignora este correo.
            """);
}
