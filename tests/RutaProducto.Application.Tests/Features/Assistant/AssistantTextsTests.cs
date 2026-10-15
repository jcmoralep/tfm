using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Assistant;

namespace RutaProducto.Application.Tests.Features.Assistant;

public sealed class AssistantTextsTests
{
    [Fact]
    public void The_exception_default_is_neutral_and_defaults_to_timeout()
    {
        var error = new AssistantUnavailableException();

        Assert.Equal(AssistantUnavailableException.DefaultMessage, error.Message);
        Assert.DoesNotContain("guardado", error.Message);
        Assert.Equal(AssistantUnavailableReason.Timeout, error.Reason);
        Assert.Equal(AssistantUnavailableReason.Timeout, new AssistantUnavailableException("x").Reason);
    }

    [Theory]
    [InlineData(AssistantUnavailableReason.Timeout, AssistantTexts.Unavailable)]
    [InlineData(AssistantUnavailableReason.Blocked, AssistantTexts.Unavailable)]
    [InlineData(AssistantUnavailableReason.Malformed, AssistantTexts.Unavailable)]
    [InlineData(AssistantUnavailableReason.Quota, AssistantTexts.Quota)]
    [InlineData(AssistantUnavailableReason.Auth, AssistantTexts.Misconfigured)]
    [InlineData(AssistantUnavailableReason.Model, AssistantTexts.Misconfigured)]
    public void Each_reason_maps_to_its_chat_text(AssistantUnavailableReason reason, string expected)
    {
        var text = AssistantTexts.UnavailableFor(reason);

        Assert.Equal(expected, text);
        Assert.Contains("quedó guardado", text);
    }
}
