using RutaProducto.Infrastructure.Identity;

namespace RutaProducto.Infrastructure.Tests.Identity;

public sealed class AccountTokenCodecTests
{
    [Fact]
    public void A_token_round_trips_and_the_code_is_url_safe()
    {
        const string token = "CfDJ8+abc/def==xyz";

        var code = AccountTokenCodec.Encode(token);

        Assert.Equal(token, AccountTokenCodec.Decode(code));
        Assert.DoesNotContain('+', code);
        Assert.DoesNotContain('/', code);
        Assert.DoesNotContain('=', code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("***")]
    [InlineData("a")]
    public void Malformed_input_gives_null(string? code) => Assert.Null(AccountTokenCodec.Decode(code));

    [Fact]
    public void Input_over_the_limit_gives_null() =>
        Assert.Null(AccountTokenCodec.Decode(new string('A', AccountTokenCodec.MaxCodeLength + 1)));
}
