using Hex1b.Tokens;

namespace Hex1b.Tests;

public class AnsiTokenSerializerTests
{
    #region TextToken Tests

    [Fact]
    public void Serialize_TextToken_ReturnsText()
    {
        var token = new TextToken("Hello, World!");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Serialize_TextTokenWithUnicode_PreservesText()
    {
        var token = new TextToken("Hello 👨‍👩‍👧 World");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("Hello 👨‍👩‍👧 World", result);
    }

    #endregion

    #region ControlCharacterToken Tests

    [Fact]
    public void Serialize_LineFeed_ReturnsNewline()
    {
        var result = AnsiTokenSerializer.Serialize(ControlCharacterToken.LineFeed);
        Assert.Equal("\n", result);
    }

    [Fact]
    public void Serialize_CarriageReturn_ReturnsCarriageReturn()
    {
        var result = AnsiTokenSerializer.Serialize(ControlCharacterToken.CarriageReturn);
        Assert.Equal("\r", result);
    }

    [Fact]
    public void Serialize_Tab_ReturnsTab()
    {
        var result = AnsiTokenSerializer.Serialize(ControlCharacterToken.Tab);
        Assert.Equal("\t", result);
    }

    #endregion

    #region SgrToken Tests

    [Fact]
    public void Serialize_SgrReset_ReturnsResetSequence()
    {
        var token = new SgrToken("0");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[0m", result);
    }

    [Fact]
    public void Serialize_SgrEmpty_ReturnsEmptyParams()
    {
        var token = new SgrToken("");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[m", result);
    }

    [Fact]
    public void Serialize_SgrBold_ReturnsBoldSequence()
    {
        var token = new SgrToken("1");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[1m", result);
    }

    [Fact]
    public void Serialize_SgrMultipleParams_PreservesParams()
    {
        var token = new SgrToken("1;31;42");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[1;31;42m", result);
    }

    [Fact]
    public void Serialize_SgrRgbColor_PreservesParams()
    {
        var token = new SgrToken("38;2;255;128;64");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[38;2;255;128;64m", result);
    }

    #endregion

    #region CursorPositionToken Tests

    [Fact]
    public void Serialize_CursorPositionDefault_ReturnsOptimized()
    {
        var token = new CursorPositionToken(1, 1);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[H", result);
    }

    [Fact]
    public void Serialize_CursorPositionRowOnly_ReturnsOptimized()
    {
        var token = new CursorPositionToken(5, 1);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[5H", result);
    }

    [Fact]
    public void Serialize_CursorPositionFull_ReturnsBothParams()
    {
        var token = new CursorPositionToken(10, 20);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[10;20H", result);
    }

    #endregion

    #region CursorShapeToken Tests

    [Fact]
    public void Serialize_CursorShapeDefault_ReturnsDefaultSequence()
    {
        var result = AnsiTokenSerializer.Serialize(CursorShapeToken.Default);
        Assert.Equal("\x1b[0 q", result);
    }

    [Fact]
    public void Serialize_CursorShapeBlinkingBlock_ReturnsCorrectSequence()
    {
        var result = AnsiTokenSerializer.Serialize(CursorShapeToken.BlinkingBlock);
        Assert.Equal("\x1b[1 q", result);
    }

    [Fact]
    public void Serialize_CursorShapeSteadyBar_ReturnsCorrectSequence()
    {
        var result = AnsiTokenSerializer.Serialize(CursorShapeToken.SteadyBar);
        Assert.Equal("\x1b[6 q", result);
    }

    #endregion

    #region ClearScreenToken Tests

    [Fact]
    public void Serialize_ClearScreenToEnd_ReturnsOptimized()
    {
        var token = new ClearScreenToken(ClearMode.ToEnd);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[J", result);
    }

    [Fact]
    public void Serialize_ClearScreenToStart_ReturnsWithParam()
    {
        var token = new ClearScreenToken(ClearMode.ToStart);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[1J", result);
    }

    [Fact]
    public void Serialize_ClearScreenAll_ReturnsWithParam()
    {
        var token = new ClearScreenToken(ClearMode.All);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[2J", result);
    }

    [Fact]
    public void Serialize_ClearScreenAllAndScrollback_ReturnsWithParam()
    {
        var token = new ClearScreenToken(ClearMode.AllAndScrollback);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[3J", result);
    }

    #endregion

    #region ClearLineToken Tests

    [Fact]
    public void Serialize_ClearLineToEnd_ReturnsOptimized()
    {
        var token = new ClearLineToken(ClearMode.ToEnd);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[K", result);
    }

    [Fact]
    public void Serialize_ClearLineToStart_ReturnsWithParam()
    {
        var token = new ClearLineToken(ClearMode.ToStart);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[1K", result);
    }

    [Fact]
    public void Serialize_ClearLineAll_ReturnsWithParam()
    {
        var token = new ClearLineToken(ClearMode.All);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[2K", result);
    }

    #endregion

    #region ScrollRegionToken Tests

    [Fact]
    public void Serialize_ScrollRegionReset_ReturnsResetSequence()
    {
        var result = AnsiTokenSerializer.Serialize(ScrollRegionToken.Reset);
        Assert.Equal("\x1b[r", result);
    }

    [Fact]
    public void Serialize_ScrollRegionExplicit_ReturnsWithParams()
    {
        var token = new ScrollRegionToken(5, 20);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[5;20r", result);
    }

    #endregion

    #region SaveCursorToken Tests

    [Fact]
    public void Serialize_SaveCursorDec_ReturnsDecSequence()
    {
        var result = AnsiTokenSerializer.Serialize(SaveCursorToken.Dec);
        Assert.Equal("\x1b" + "7", result);
    }

    [Fact]
    public void Serialize_SaveCursorAnsi_ReturnsAnsiSequence()
    {
        var result = AnsiTokenSerializer.Serialize(SaveCursorToken.Ansi);
        Assert.Equal("\x1b[s", result);
    }

    #endregion

    #region RestoreCursorToken Tests

    [Fact]
    public void Serialize_RestoreCursorDec_ReturnsDecSequence()
    {
        var result = AnsiTokenSerializer.Serialize(RestoreCursorToken.Dec);
        Assert.Equal("\x1b" + "8", result);
    }

    [Fact]
    public void Serialize_RestoreCursorAnsi_ReturnsAnsiSequence()
    {
        var result = AnsiTokenSerializer.Serialize(RestoreCursorToken.Ansi);
        Assert.Equal("\x1b[u", result);
    }

    #endregion

    #region PrivateModeToken Tests

    [Fact]
    public void Serialize_PrivateModeEnable_ReturnsEnableSequence()
    {
        var token = new PrivateModeToken(1049, true);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[?1049h", result);
    }

    [Fact]
    public void Serialize_PrivateModeDisable_ReturnsDisableSequence()
    {
        var token = new PrivateModeToken(1049, false);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[?1049l", result);
    }

    [Fact]
    public void Serialize_PrivateModeCursorHide_ReturnsCorrectSequence()
    {
        var token = new PrivateModeToken(25, false);
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[?25l", result);
    }

    #endregion

    #region OscToken Tests

    [Fact]
    public void Serialize_OscHyperlink_ReturnsHyperlinkSequence()
    {
        var token = new OscToken("8", "", "https://example.com");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b]8;;https://example.com\x07", result);
    }

    [Fact]
    public void Serialize_OscHyperlinkWithParams_IncludesParams()
    {
        var token = new OscToken("8", "id=mylink", "https://example.com");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b]8;id=mylink;https://example.com\x07", result);
    }

    [Fact]
    public void Serialize_OscHyperlinkEnd_ReturnsEndSequence()
    {
        var token = new OscToken("8", "", "");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b]8;;\x07", result);
    }

    [Fact]
    public void Serialize_OscWindowTitle_ReturnsCorrectSequence()
    {
        var token = new OscToken("0", "", "My Window Title");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b]0;My Window Title\x07", result);
    }

    #endregion

    #region DcsToken Tests

    [Fact]
    public void Serialize_DcsSequence_ReturnsDcsWithSt()
    {
        var token = new DcsToken("q#0;2;0;0;0");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1bPq#0;2;0;0;0\x1b\\", result);
    }

    #endregion

    #region UnrecognizedSequenceToken Tests

    [Fact]
    public void Serialize_UnrecognizedSequence_ReturnsRawSequence()
    {
        var token = new UnrecognizedSequenceToken("\x1b[5Z");
        var result = AnsiTokenSerializer.Serialize(token);
        Assert.Equal("\x1b[5Z", result);
    }

    #endregion

    #region Multi-Token Tests

    [Fact]
    public void Serialize_MultipleTokens_ConcatenatesResults()
    {
        var tokens = new AnsiToken[]
        {
            new SgrToken("31"),
            new TextToken("Hello"),
            new SgrToken("0")
        };

        var result = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal("\x1b[31mHello\x1b[0m", result);
    }

    [Fact]
    public void Serialize_EmptyList_ReturnsEmptyString()
    {
        var result = AnsiTokenSerializer.Serialize(Array.Empty<AnsiToken>());
        Assert.Equal("", result);
    }

    [Fact]
    public void Serialize_ComplexSequence_ProducesValidOutput()
    {
        var tokens = new AnsiToken[]
        {
            new PrivateModeToken(1049, true),
            new ClearScreenToken(ClearMode.All),
            new CursorPositionToken(1, 1),
            new SgrToken("1;32"),
            new TextToken("Welcome!"),
            new SgrToken("0"),
            ControlCharacterToken.LineFeed
        };

        var result = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal("\x1b[?1049h\x1b[2J\x1b[H\x1b[1;32mWelcome!\x1b[0m\n", result);
    }

    #endregion

    #region Round-Trip Tests

    [Theory]
    [InlineData("Hello, World!")]
    [InlineData("\x1b[31mRed\x1b[0m")]
    [InlineData("\x1b[H")]
    [InlineData("\x1b[10;20H")]
    [InlineData("\x1b[2J")]
    [InlineData("\x1b[K")]
    [InlineData("\x1b[?1049h")]
    [InlineData("\x1b[?25l")]
    public void RoundTrip_TokenizeAndSerialize_ProducesEquivalentOutput(string input)
    {
        var tokens = AnsiTokenizer.Tokenize(input);
        var output = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(input, output);
    }

    [Fact]
    public void RoundTrip_ComplexAnsiSequence_PreservesSemantics()
    {
        // Note: The round-trip might not produce identical output due to optimizations,
        // but tokenizing the output should produce semantically equivalent tokens
        var input = "\x1b[1;1H\x1b[0J";
        var tokens = AnsiTokenizer.Tokenize(input);
        var output = AnsiTokenSerializer.Serialize(tokens);
        var retokenized = AnsiTokenizer.Tokenize(output);

        Assert.Equal(tokens.Count, retokenized.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            Assert.Equal(tokens[i], retokenized[i]);
        }
    }

    [Fact]
    public void RoundTrip_HyperlinkSequence_PreservesPayload()
    {
        var input = "\x1b]8;;https://example.com\x07" + "Click\x1b]8;;\x07";
        var tokens = AnsiTokenizer.Tokenize(input);
        var output = AnsiTokenSerializer.Serialize(tokens);
        var retokenized = AnsiTokenizer.Tokenize(output);

        Assert.Equal(3, retokenized.Count);

        var startLink = Assert.IsType<OscToken>(retokenized[0]);
        Assert.Equal("8", startLink.Command);
        Assert.Equal("https://example.com", startLink.Payload);

        var text = Assert.IsType<TextToken>(retokenized[1]);
        Assert.Equal("Click", text.Text);

        var endLink = Assert.IsType<OscToken>(retokenized[2]);
        Assert.Equal("8", endLink.Command);
        Assert.Equal("", endLink.Payload);
    }

    #endregion

    #region FrameToken Tests

    [Fact]
    public void Serialize_FrameBeginToken_ReturnsApcSequence()
    {
        var result = AnsiTokenSerializer.Serialize(FrameBeginToken.Instance);
        Assert.Equal("\x1b_HEX1BAPP:FRAME:BEGIN\x1b\\", result);
    }

    [Fact]
    public void Serialize_FrameEndToken_ReturnsApcSequence()
    {
        var result = AnsiTokenSerializer.Serialize(FrameEndToken.Instance);
        Assert.Equal("\x1b_HEX1BAPP:FRAME:END\x1b\\", result);
    }

    [Fact]
    public void RoundTrip_FrameBeginToken_PreservesToken()
    {
        var input = "\x1b_HEX1BAPP:FRAME:BEGIN\x1b\\";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        Assert.Single(tokens);
        Assert.Same(FrameBeginToken.Instance, tokens[0]);
        
        var output = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(input, output);
    }

    [Fact]
    public void RoundTrip_FrameEndToken_PreservesToken()
    {
        var input = "\x1b_HEX1BAPP:FRAME:END\x1b\\";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        Assert.Single(tokens);
        Assert.Same(FrameEndToken.Instance, tokens[0]);
        
        var output = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(input, output);
    }

    [Fact]
    public void RoundTrip_FrameBoundaries_InComplexSequence()
    {
        var tokens = new AnsiToken[]
        {
            FrameBeginToken.Instance,
            new ClearScreenToken(ClearMode.All),
            new CursorPositionToken(1, 1),
            new TextToken("Hello"),
            FrameEndToken.Instance
        };

        var serialized = AnsiTokenSerializer.Serialize(tokens);
        var retokenized = AnsiTokenizer.Tokenize(serialized);

        Assert.Equal(5, retokenized.Count);
        Assert.Same(FrameBeginToken.Instance, retokenized[0]);
        Assert.IsType<ClearScreenToken>(retokenized[1]);
        Assert.IsType<CursorPositionToken>(retokenized[2]);
        Assert.IsType<TextToken>(retokenized[3]);
        Assert.Same(FrameEndToken.Instance, retokenized[4]);
    }

    #endregion
}
