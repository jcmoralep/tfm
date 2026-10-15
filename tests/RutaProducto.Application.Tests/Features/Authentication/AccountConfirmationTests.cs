using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Features.Authentication.ConfirmEmail;
using RutaProducto.Application.Features.Authentication.Register;
using RutaProducto.Application.Features.Authentication.ResendConfirmation;
using RutaProducto.Application.Tests.TestDoubles;
using MediatR;

namespace RutaProducto.Application.Tests.Features.Authentication;

public sealed class AccountConfirmationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirming_returns_what_the_token_service_decides(bool valid)
    {
        var tokens = new FakeAccountTokenService { ConfirmResult = valid };

        var result = await new ConfirmEmailCommandHandler(tokens).Handle(new ConfirmEmailCommand("user-1", "code"), CancellationToken.None);

        Assert.Equal(valid, result);
    }

    [Theory]
    [InlineData("", "code")]
    [InlineData("user-1", "")]
    [InlineData("user-1", "too-long")]
    public void A_missing_or_oversized_link_part_is_invalid(string userId, string code)
    {
        if (code == "too-long")
        {
            code = new string('a', ConfirmEmailCommandValidator.MaxCodeLength + 1);
        }

        var result = new ConfirmEmailCommandValidator().Validate(new ConfirmEmailCommand(userId, code));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("pending@example.com", 1)]
    [InlineData("confirmed@example.com", 0)]
    [InlineData("nobody@example.com", 0)]
    public async Task Resend_gives_the_same_result_and_mails_only_a_pending_account(string email, int expectedMessages)
    {
        var tokens = new FakeAccountTokenService();
        tokens.KnownEmails.Add("pending@example.com");
        var sender = new RecordingEmailSender();
        var handler = new ResendConfirmationCommandHandler(tokens, new FakeAccountLinks(), sender, new AccountEmailSettings(true, true, null));

        var result = await handler.Handle(new ResendConfirmationCommand(email), CancellationToken.None);

        Assert.Equal(Unit.Value, result);
        Assert.Equal(expectedMessages, sender.Messages.Count);
        Assert.All(sender.Messages, message => Assert.Equal(EmailKind.ConfirmEmail, message.Kind));
    }

    [Fact]
    public async Task Resend_does_nothing_when_email_is_off()
    {
        var tokens = new FakeAccountTokenService();
        tokens.KnownEmails.Add("pending@example.com");
        var sender = new RecordingEmailSender();
        var handler = new ResendConfirmationCommandHandler(tokens, new FakeAccountLinks(), sender, new AccountEmailSettings(false, false, null));

        await handler.Handle(new ResendConfirmationCommand("pending@example.com"), CancellationToken.None);

        Assert.Empty(sender.Messages);
        Assert.Empty(tokens.Calls);
    }

    [Fact]
    public void Resend_validates_the_address_format()
    {
        var failure = Assert.Single(new ResendConfirmationCommandValidator().Validate(new ResendConfirmationCommand("not-an-email")).Errors);

        Assert.Equal("El formato del correo electrónico no es válido.", failure.ErrorMessage);
    }

    [Fact]
    public void No_command_or_token_prints_the_email_the_password_or_the_code()
    {
        var printed = new object[]
        {
            new RegisterCommand("ana@example.com", "Secret-123", "Secret-123", "CAPTCHA-TOKEN"),
            new ConfirmEmailCommand("user-1", "CODE-123"),
            new ResendConfirmationCommand("ana@example.com"),
            new AccountToken("user-1", "CODE-123"),
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
