using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class SgrTokenFastPathTests
{
    [TestMethod]
    public void GetSgrTokenFromBytes_EmptySpan_ReturnsReset()
    {
        var token = AnsiTokenCache.GetSgrTokenFromBytes(ReadOnlySpan<byte>.Empty);
        Assert.AreSame(SgrToken.Reset, token);
    }

    [TestMethod]
    public void GetSgrTokenFromBytes_RoundTripsParameters()
    {
        ReadOnlySpan<byte> bytes = "38;2;255;128;64"u8;
        var token = AnsiTokenCache.GetSgrTokenFromBytes(bytes);

        Assert.AreEqual("38;2;255;128;64", token.Parameters);
        Assert.IsNotNull(token.PreformattedBytes);
        TestSeq.AreEqual(bytes.ToArray(), token.PreformattedBytes!);
    }

    [TestMethod]
    public void GetSgrTokenFromBytes_SecondCallReturnsCachedInstance()
    {
        ReadOnlySpan<byte> bytes = "1;31"u8;
        var first = AnsiTokenCache.GetSgrTokenFromBytes(bytes);
        var second = AnsiTokenCache.GetSgrTokenFromBytes(bytes);
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void SgrToken_EqualityIgnoresPreformattedBytes()
    {
        var plain = new SgrToken("31");
        var withBytes = new SgrToken("31") { PreformattedBytes = "31"u8.ToArray() };
        Assert.AreEqual(plain, withBytes);
        Assert.AreEqual(plain.GetHashCode(), withBytes.GetHashCode());
    }

    [TestMethod]
    public void Serialize_UsesPreformattedBytes_WhenAvailable()
    {
        var token = new SgrToken("ignored-on-fast-path") { PreformattedBytes = "31"u8.ToArray() };
        var bytes = AnsiTokenUtf8Serializer.Serialize(new[] { token });
        // Should emit "\x1b[31m" using the PreformattedBytes, not the Parameters string.
        Assert.AreEqual("\x1b[31m", Encoding.UTF8.GetString(bytes.Span));
    }

    [TestMethod]
    public void Serialize_FallsBackToParameters_WhenPreformattedBytesNull()
    {
        var token = new SgrToken("1;38;2;255;0;0");
        var bytes = AnsiTokenUtf8Serializer.Serialize(new[] { token });
        Assert.AreEqual("\x1b[1;38;2;255;0;0m", Encoding.UTF8.GetString(bytes.Span));
    }
}
