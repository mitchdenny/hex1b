using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class AnsiTokenUtf8SerializerTests
{
    [TestMethod]
    public void Serialize_SingleToken_MatchesStringSerializerUtf8()
    {
        var token = new CursorPositionToken(10, 20);

        var expected = Encoding.UTF8.GetBytes(AnsiTokenSerializer.Serialize(token));
        var actual = AnsiTokenUtf8Serializer.Serialize(token).Span.ToArray();

        TestSeq.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Serialize_TokenList_MatchesStringSerializerUtf8()
    {
        IReadOnlyList<AnsiToken> tokens =
        [
            new CursorPositionToken(10, 20),
            new SgrToken("38;2;255;128;64"),
            new TextToken("Hello 👨‍👩‍👧 World"),
            new OscToken("8", "", "https://example.com", UseEscBackslash: true),
            new TextToken("link"),
            new OscToken("8", "", "", UseEscBackslash: true),
            new DcsToken("qABC"),
            ControlCharacterToken.LineFeed
        ];

        var expected = Encoding.UTF8.GetBytes(AnsiTokenSerializer.Serialize(tokens));
        var actual = AnsiTokenUtf8Serializer.Serialize(tokens).Span.ToArray();

        TestSeq.AreEqual(expected, actual);
    }
}

