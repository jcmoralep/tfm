using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Captcha;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Features.Authentication.ForgotPassword;
using RutaProducto.Application.Features.Authentication.ResetPassword;
using RutaProducto.Application.Tests.TestDoubles;

namespace RutaProducto.Application.Tests.Features.Authentication;

public sealed class PasswordRecoveryTests
{
    private static readonly AccountEmailSettings EmailOn = new(true, true, null);

    private static (ForgotPasswordCommandHandler Handler, FakeAccountTokenService Tokens, RecordingEmailSender Sender, FakeCaptchaVerifier Captcha) Forgot(
        CaptchaOutcome captcha = CaptchaOutcome.Passed, AccountEmailSettings? settings = null)
    {
        var tokens = new FakeAccountTokenService();
        tokens.ExistingEmails.Add("confirmed@example.com");
        tokens.ExistingEmails.Add("unconfirmed@example.com");
        tokens.ExistingEmails.Add("locked@example.com");
        var sender = new RecordingEmailSender();
        var verifier = new FakeCaptchaVerifier(captcha);

        return (new ForgotPasswordCommandHandler(verifier, tokens, new FakeAccountLinks(), sender, settings ?? EmailOn), tokens, sender, verifier);
    }

    [Theory]
    [InlineData("confirmed@example.com", 1)]
    [InlineData("unconfirmed@example.com", 1)]
    [InlineData("locked@example.com", 1)]
    [InlineData("nobody@example.com", 0)]
    public async Task Forgot_gives_the_same_result_and_mails_only_an_existing_account_a_reset_link(string email, int expectedMessages)
    {
        var (handler, _, sender, _) = Forgot();

        var result = await handler.Handle(new ForgotPasswordCommand(email, "token"), CancellationToken.None);

        Assert.Equal(ForgotPasswordResult.Accepted, result);
        Assert.Equal(expectedMessages, sender.Messages.Count);
        Assert.All(sender.Messages, message =>
        {
            Assert.Equal(EmailKind.ResetPassword, message.Kind);
            Assert.Equal(email, message.To);
            Assert.Contains("restablecer-contrasena?usuario=user-1&codigo=reset-code-1", message.Body);
        });
    }

    [Theory]
    [InlineData(CaptchaOutcome.Rejected, ForgotPasswordResult.CaptchaRejected)]
    [InlineData(CaptchaOutcome.Unavailable, ForgotPasswordResult.CaptchaUnavailable)]
    public async Task A_failed_captcha_stops_before_any_lookup(CaptchaOutcome outcome, ForgotPasswordResult expected)
    {
        var (handler, tokens, sender, captcha) = Forgot(outcome);

        var result = await handler.Handle(new ForgotPasswordCommand("confirmed@example.com", "token"), CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(1, captcha.Calls);
        Assert.Empty(tokens.Calls);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task With_email_off_nothing_is_looked_up_or_sent()
    {
        var (handler, tokens, sender, _) = Forgot(settings: new AccountEmailSettings(false, false, null));

        var result = await handler.Handle(new ForgotPasswordCommand("confirmed@example.com", "token"), CancellationToken.None);

        Assert.Equal(ForgotPasswordResult.Accepted, result);
        Assert.Empty(tokens.Calls);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(PasswordResetStatus.Succeeded)]
    [InlineData(PasswordResetStatus.InvalidLink)]
    [InlineData(PasswordResetStatus.InvalidPassword)]
    public async Task Reset_returns_what_the_token_service_decides(PasswordResetStatus status)
    {
        var expected = new PasswordResetResult(status, status == PasswordResetStatus.InvalidPassword ? ["La contraseña es débil."] : []);
        var tokens = new FakeAccountTokenService { ResetResult = expected };

        var result = await new ResetPasswordCommandHandler(tokens).Handle(
            new ResetPasswordCommand("user-1", "code", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "", "La contraseña es obligatoria.")]
    [InlineData("Secret-123", "Secret-124", "Las contraseñas no coinciden.")]
    public void The_reset_validator_uses_the_registration_messages(string password, string confirmation, string message)
    {
        var failure = Assert.Single(new ResetPasswordCommandValidator().Validate(new ResetPasswordCommand("u", "c", password, confirmation)).Errors);

        Assert.Equal(message, failure.ErrorMessage);
    }

    [Fact]
    public void The_forgot_validator_checks_the_address_format()
    {
        var failure = Assert.Single(new ForgotPasswordCommandValidator().Validate(new ForgotPasswordCommand("not-an-email")).Errors);

        Assert.Equal("El formato del correo electrónico no es válido.", failure.ErrorMessage);
    }

    [Fact]
    public void No_recovery_command_prints_the_email_the_password_the_code_or_the_captcha_token()
    {
        var printed = new object[]
        {
            new ForgotPasswordCommand("ana@example.com", "CAPTCHA-TOKEN"),
            new ResetPasswordCommand("user-1", "CODE-123", "Secret-123", "Secret-123"),
        }.Select(item => item.ToString()).ToList();

        foreach (var text in printed)
        {
            Assert.DoesNotContain("ana@example.com", text);
            Assert.DoesNotContain("Secret-123", text);
            Assert.DoesNotContain("CODE-123", text);
            Assert.DoesNotContain("CAPTCHA-TOKEN", text);
        }
    }
}
