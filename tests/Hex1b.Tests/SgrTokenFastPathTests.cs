using System.Text;
using Hex1b.Tokens;
using Xunit;

namespace Hex1b.Tests;

public class SgrTokenFastPathTests
{
    [Fact]
    public void GetSgrTokenFromBytes_EmptySpan_ReturnsReset()
    {
        var token = AnsiTokenCache.GetSgrTokenFromBytes(ReadOnlySpan<byte>.Empty);
        Assert.Same(SgrToken.Reset, token);
    }

    [Fact]
    public void GetSgrTokenFromBytes_RoundTripsParameters()
    {
        ReadOnlySpan<byte> bytes = "38;2;255;128;64"u8;
        var token = AnsiTokenCache.GetSgrTokenFromBytes(bytes);

        Assert.Equal("38;2;255;128;64", token.Parameters);
        Assert.NotNull(token.PreformattedBytes);
        Assert.Equal(bytes.ToArray(), token.PreformattedBytes!);
    }

    [Fact]
    public void GetSgrTokenFromBytes_SecondCallReturnsCachedInstance()
    {
        ReadOnlySpan<byte> bytes = "1;31"u8;
        var first = AnsiTokenCache.GetSgrTokenFromBytes(bytes);
        var second = AnsiTokenCache.GetSgrTokenFromBytes(bytes);
        Assert.Same(first, second);
    }

    [Fact]
    public void SgrToken_EqualityIgnoresPreformattedBytes()
    {
        var plain = new SgrToken("31");
        var withBytes = new SgrToken("31") { PreformattedBytes = "31"u8.ToArray() };
        Assert.Equal(plain, withBytes);
        Assert.Equal(plain.GetHashCode(), withBytes.GetHashCode());
    }

    [Fact]
    public void Serialize_UsesPreformattedBytes_WhenAvailable()
    {
        var token = new SgrToken("ignored-on-fast-path") { PreformattedBytes = "31"u8.ToArray() };
        var bytes = AnsiTokenUtf8Serializer.Serialize(new[] { token });
        // Should emit "\x1b[31m" using the PreformattedBytes, not the Parameters string.
        Assert.Equal("\x1b[31m", Encoding.UTF8.GetString(bytes.Span));
    }

    [Fact]
    public void Serialize_FallsBackToParameters_WhenPreformattedBytesNull()
    {
        var token = new SgrToken("1;38;2;255;0;0");
        var bytes = AnsiTokenUtf8Serializer.Serialize(new[] { token });
        Assert.Equal("\x1b[1;38;2;255;0;0m", Encoding.UTF8.GetString(bytes.Span));
    }
}
