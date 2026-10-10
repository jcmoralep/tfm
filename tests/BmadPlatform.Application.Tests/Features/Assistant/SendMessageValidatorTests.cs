using BmadPlatform.Application.Features.Assistant.SendMessage;

namespace BmadPlatform.Application.Tests.Features.Assistant;

public sealed class SendMessageValidatorTests
{
    private readonly SendMessageCommandValidator validator = new();

    [Fact]
    public void Text_of_exactly_2000_characters_is_valid()
    {
        Assert.True(Validate(new string('a', 2000), null).IsValid);
    }

    [Fact]
    public void Text_of_2001_characters_fails_with_the_length_message()
    {
        var result = Validate(new string('a', 2001), null);

        Assert.Equal("La respuesta no puede superar los 2000 caracteres.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void Length_is_measured_after_trimming()
    {
        Assert.True(Validate($"   {new string('a', 2000)}   ", null).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void Empty_or_whitespace_text_without_a_quick_reply_fails(string? text)
    {
        var result = Validate(text, null);

        Assert.Equal("La respuesta no puede estar vacía.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void A_quick_reply_alone_is_valid_and_a_blank_box_beside_it_counts_as_absent()
    {
        Assert.True(Validate(null, "customers").IsValid);
        Assert.True(Validate("   ", "customers").IsValid);
    }

    [Fact]
    public void Text_and_a_quick_reply_together_fail()
    {
        var result = Validate("Algo", "customers");

        Assert.Equal("Escriba una respuesta o elija una opción.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void A_quick_reply_key_over_50_characters_is_not_valid()
    {
        var result = Validate(null, new string('k', 51));

        Assert.Equal("La respuesta rápida no es válida para esta pregunta.", Assert.Single(result.Errors).ErrorMessage);
    }

    private FluentValidation.Results.ValidationResult Validate(string? text, string? key) =>
        validator.Validate(new SendMessageCommand(Guid.NewGuid(), 0, text, key));
}
