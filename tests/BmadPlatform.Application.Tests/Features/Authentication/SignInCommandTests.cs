using BmadPlatform.Application.Features.Authentication.SignIn;

namespace BmadPlatform.Application.Tests.Features.Authentication;

public sealed class SignInCommandTests
{
    private readonly SignInCommandValidator validator = new();

    [Fact]
    public void Well_formed_credentials_are_valid()
    {
        var result = validator.Validate(new SignInCommand("ana@example.com", "any-password"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "El correo electrónico es obligatorio.")]
    [InlineData("not-an-email", "El formato del correo electrónico no es válido.")]
    public void Invalid_email_is_rejected_with_a_spanish_message(string email, string expectedMessage)
    {
        var result = validator.Validate(new SignInCommand(email, "any-password"));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(SignInCommand.Email), failure.PropertyName);
        Assert.Equal(expectedMessage, failure.ErrorMessage);
    }

    [Fact]
    public void Missing_password_is_rejected_with_a_spanish_message()
    {
        var result = validator.Validate(new SignInCommand("ana@example.com", string.Empty));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(SignInCommand.Password), failure.PropertyName);
        Assert.Equal("La contraseña es obligatoria.", failure.ErrorMessage);
    }

    [Fact]
    public void ToString_does_not_expose_credentials()
    {
        var text = new SignInCommand("ana@example.com", "P@ssw0rd-secret").ToString();

        Assert.DoesNotContain("P@ssw0rd-secret", text);
        Assert.DoesNotContain("ana@example.com", text);
    }
}
