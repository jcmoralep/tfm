using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Features.Authentication;

namespace RutaProducto.Application.Tests.Features.Authentication;

public sealed class AccountEmailsTests
{
    private static readonly Uri Link = new("https://app.example.com/confirmar-correo?usuario=1&codigo=abc");
    private static readonly Uri Other = new("https://app.example.com/recuperar-contrasena");

    public static TheoryData<EmailMessage, string, EmailKind> Messages => new()
    {
        { AccountEmails.ConfirmEmail("ana@example.com", Link), "Confirma tu correo en Ruta de Producto", EmailKind.ConfirmEmail },
        { AccountEmails.AccountExists("ana@example.com", Link, Other), "Ya tienes una cuenta en Ruta de Producto", EmailKind.AccountExists },
        { AccountEmails.ResetPassword("ana@example.com", Link), "Restablece tu contraseña de Ruta de Producto", EmailKind.ResetPassword },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Each_template_is_spanish_carries_its_link_and_the_product_name(EmailMessage message, string subject, EmailKind kind)
    {
        Assert.Equal(kind, message.Kind);
        Assert.Equal(subject, message.Subject);
        Assert.Equal("ana@example.com", message.To);
        Assert.Contains(Link.ToString(), message.Body);
        Assert.Contains("Ruta de Producto", message.Body);
        Assert.DoesNotContain("BMAD", message.Subject + message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_account_exists_email_offers_the_sign_in_and_the_forgot_links()
    {
        var message = AccountEmails.AccountExists("ana@example.com", Link, Other);

        Assert.Contains(Other.ToString(), message.Body);
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void The_text_of_a_message_is_its_kind_only(EmailMessage message, string subject, EmailKind kind)
    {
        var text = message.ToString();

        Assert.Equal(kind.ToString(), text);
        Assert.DoesNotContain("ana@example.com", text);
        Assert.DoesNotContain("codigo", text);
        Assert.DoesNotContain(subject, text);
    }
}
