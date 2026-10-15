using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Captcha;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Features.Authentication.Register;
using RutaProducto.Application.Tests.TestDoubles;

namespace RutaProducto.Application.Tests.Features.Authentication;

public sealed class RegisterCommandTests
{
    private readonly RegisterCommandValidator validator = new();

    [Fact]
    public void A_well_formed_registration_is_valid()
    {
        var result = validator.Validate(new RegisterCommand("ana@example.com", "Secret-123", "Secret-123"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Secret-123", "Secret-123", nameof(RegisterCommand.Email), "El correo electrónico es obligatorio.")]
    [InlineData("not-an-email", "Secret-123", "Secret-123", nameof(RegisterCommand.Email), "El formato del correo electrónico no es válido.")]
    [InlineData("ana@example.com", "", "", nameof(RegisterCommand.Password), "La contraseña es obligatoria.")]
    [InlineData("ana@example.com", "Secret-123", "Secret-124", nameof(RegisterCommand.ConfirmPassword), "Las contraseñas no coinciden.")]
    public void Invalid_input_is_rejected_with_a_spanish_message(
        string email, string password, string confirmation, string property, string message)
    {
        var failure = Assert.Single(validator.Validate(new RegisterCommand(email, password, confirmation)).Errors);

        Assert.Equal(property, failure.PropertyName);
        Assert.Equal(message, failure.ErrorMessage);
    }

    [Fact]
    public void An_overlong_password_is_rejected()
    {
        var tooLong = new string('a', RegisterCommandValidator.MaxPasswordLength + 1);

        var failure = Assert.Single(validator.Validate(new RegisterCommand("ana@example.com", tooLong, tooLong)).Errors);

        Assert.Equal("La contraseña es demasiado larga.", failure.ErrorMessage);
    }

    [Fact]
    public void ToString_does_not_expose_credentials()
    {
        var text = new RegisterCommand("ana@example.com", "Secret-123", "Secret-123").ToString();

        Assert.DoesNotContain("Secret-123", text);
        Assert.DoesNotContain("ana@example.com", text);
    }

    [Fact]
    public void ToString_does_not_expose_the_captcha_token()
    {
        var text = new RegisterCommand("ana@example.com", "Secret-123", "Secret-123", "TOKEN-xyz").ToString();

        Assert.DoesNotContain("TOKEN-xyz", text);
    }

    [Fact]
    public async Task With_confirmation_off_a_new_account_is_created_with_the_trimmed_email_signed_in_and_nothing_is_emailed()
    {
        var authService = new RecordingAuthService(RegistrationResult.Succeeded);
        var sender = new RecordingEmailSender();

        var result = await Handler(authService, sender: sender).Handle(
            new RegisterCommand("  ana@example.com ", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(RegistrationStatus.Succeeded, result.Status);
        Assert.Equal("ana@example.com", authService.RegisteredEmail);
        Assert.Equal("ana@example.com", authService.SignedInEmail);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(RegistrationStatus.EmailTaken)]
    [InlineData(RegistrationStatus.InvalidPassword)]
    public async Task With_confirmation_off_a_rejected_registration_does_not_sign_anyone_in(RegistrationStatus status)
    {
        var authService = new RecordingAuthService(new RegistrationResult(status, []));
        var sender = new RecordingEmailSender();

        var result = await Handler(authService, sender: sender).Handle(
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Null(authService.SignedInEmail);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(CaptchaOutcome.Rejected, RegistrationStatus.CaptchaRejected, false)]
    [InlineData(CaptchaOutcome.Unavailable, RegistrationStatus.CaptchaUnavailable, false)]
    [InlineData(CaptchaOutcome.Rejected, RegistrationStatus.CaptchaRejected, true)]
    [InlineData(CaptchaOutcome.Unavailable, RegistrationStatus.CaptchaUnavailable, true)]
    public async Task A_captcha_that_did_not_pass_creates_no_account_signs_no_one_in_and_emails_nobody(
        CaptchaOutcome outcome, RegistrationStatus expected, bool confirmation)
    {
        var authService = new RecordingAuthService(RegistrationResult.Succeeded);
        var sender = new RecordingEmailSender();

        var result = await Handler(authService, new FakeCaptchaVerifier(outcome), sender, confirmation).Handle(
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123", "tok"), CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Null(authService.RegisteredEmail);
        Assert.Null(authService.SignedInEmail);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task A_passed_captcha_receives_the_token_and_registration_goes_on_as_before()
    {
        var authService = new RecordingAuthService(RegistrationResult.Succeeded);
        var captcha = new FakeCaptchaVerifier(CaptchaOutcome.Passed);

        var result = await Handler(authService, captcha).Handle(
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123", "tok"), CancellationToken.None);

        Assert.Equal(RegistrationStatus.Succeeded, result.Status);
        Assert.Equal("tok", captcha.LastToken);
        Assert.Equal("ana@example.com", authService.SignedInEmail);
    }

    [Fact]
    public async Task With_confirmation_on_a_new_account_gets_a_confirmation_email_and_is_not_signed_in()
    {
        var authService = new RecordingAuthService(RegistrationResult.Succeeded);
        var sender = new RecordingEmailSender();

        var result = await Handler(authService, sender: sender, confirmation: true, pendingEmail: "ana@example.com").Handle(
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(RegistrationStatus.ConfirmationPending, result.Status);
        Assert.Null(authService.SignedInEmail);
        var message = Assert.Single(sender.Messages);
        Assert.Equal(EmailKind.ConfirmEmail, message.Kind);
        Assert.Equal("ana@example.com", message.To);
        Assert.Contains("https://app.test/confirmar-correo?usuario=user-1&codigo=code-1", message.Body);
    }

    [Fact]
    public async Task With_confirmation_on_a_duplicate_address_gets_the_same_result_and_an_account_exists_email()
    {
        var authService = new RecordingAuthService(RegistrationResult.EmailTaken);
        var sender = new RecordingEmailSender();

        var result = await Handler(authService, sender: sender, confirmation: true).Handle(
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(RegistrationStatus.ConfirmationPending, result.Status);
        Assert.Null(authService.SignedInEmail);
        var message = Assert.Single(sender.Messages);
        Assert.Equal(EmailKind.AccountExists, message.Kind);
        Assert.Contains("https://app.test/login", message.Body);
        Assert.Contains("https://app.test/recuperar-contrasena", message.Body);
    }

    [Fact]
    public async Task With_confirmation_on_a_weak_password_is_reported_as_before_and_nothing_is_emailed()
    {
        var authService = new RecordingAuthService(RegistrationResult.InvalidPassword(["weak"]));
        var sender = new RecordingEmailSender();

        var result = await Handler(authService, sender: sender, confirmation: true).Handle(
            new RegisterCommand("ana@example.com", "abc", "abc"), CancellationToken.None);

        Assert.Equal(RegistrationStatus.InvalidPassword, result.Status);
        Assert.Empty(sender.Messages);
    }

    private static RegisterCommandHandler Handler(
        RecordingAuthService authService,
        FakeCaptchaVerifier? captcha = null,
        RecordingEmailSender? sender = null,
        bool confirmation = false,
        string? pendingEmail = null)
    {
        var tokens = new FakeAccountTokenService();

        if (pendingEmail is not null)
        {
            tokens.KnownEmails.Add(pendingEmail);
        }

        return new RegisterCommandHandler(
            authService,
            captcha ?? new FakeCaptchaVerifier(),
            tokens,
            new FakeAccountLinks(),
            sender ?? new RecordingEmailSender(),
            new AccountEmailSettings(confirmation, confirmation, null));
    }

    private sealed class RecordingAuthService(RegistrationResult registration) : IAuthService
    {
        public string? RegisteredEmail { get; private set; }

        public string? SignedInEmail { get; private set; }

        public Task<RegistrationResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            RegisteredEmail = email;
            return Task.FromResult(registration);
        }

        public Task<AuthenticationResult> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default)
        {
            SignedInEmail = email;
            return Task.FromResult(AuthenticationResult.Succeeded);
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
