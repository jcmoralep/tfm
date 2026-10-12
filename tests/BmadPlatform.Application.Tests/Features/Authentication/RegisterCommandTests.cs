using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Features.Authentication.Register;

namespace BmadPlatform.Application.Tests.Features.Authentication;

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
    public async Task A_new_account_is_created_with_the_trimmed_email_and_signed_in()
    {
        var authService = new RecordingAuthService(RegistrationResult.Succeeded);

        var result = await new RegisterCommandHandler(authService).Handle(
            new RegisterCommand("  ana@example.com ", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(RegistrationStatus.Succeeded, result.Status);
        Assert.Equal("ana@example.com", authService.RegisteredEmail);
        Assert.Equal("ana@example.com", authService.SignedInEmail);
    }

    [Theory]
    [InlineData(RegistrationStatus.EmailTaken)]
    [InlineData(RegistrationStatus.InvalidPassword)]
    public async Task A_rejected_registration_does_not_sign_anyone_in(RegistrationStatus status)
    {
        var authService = new RecordingAuthService(new RegistrationResult(status, []));

        var result = await new RegisterCommandHandler(authService).Handle(
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123"), CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Null(authService.SignedInEmail);
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
