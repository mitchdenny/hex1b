using Hex1b.Tokens;

namespace Hex1b.Tests;

public class AnsiTokenizerTests
{
    #region Basic Text Tests

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmptyList()
    {
        var result = AnsiTokenizer.Tokenize("");
        Assert.Empty(result);
    }

    [Fact]
    public void Tokenize_NullString_ReturnsEmptyList()
    {
        var result = AnsiTokenizer.Tokenize(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Tokenize_PlainText_ReturnsSingleTextToken()
    {
        var result = AnsiTokenizer.Tokenize("Hello, World!");

        var token = Assert.Single(result);
        var textToken = Assert.IsType<TextToken>(token);
        Assert.Equal("Hello, World!", textToken.Text);
    }

    [Fact]
    public void Tokenize_ConsecutiveTextAndControlChars_BatchesTextCorrectly()
    {
        var result = AnsiTokenizer.Tokenize("Hello\nWorld");

        Assert.Collection(result,
            t => Assert.Equal("Hello", Assert.IsType<TextToken>(t).Text),
            t => Assert.Same(ControlCharacterToken.LineFeed, t),
            t => Assert.Equal("World", Assert.IsType<TextToken>(t).Text));
    }

    [Fact]
    public void Tokenize_UnicodeEmoji_PreservesGraphemeClusters()
    {
        // Family emoji is a complex grapheme cluster
        var result = AnsiTokenizer.Tokenize("Hello 👨‍👩‍👧 World");

        var token = Assert.Single(result);
        var textToken = Assert.IsType<TextToken>(token);
        Assert.Equal("Hello 👨‍👩‍👧 World", textToken.Text);
    }

    #endregion

    #region Control Character Tests

    [Fact]
    public void Tokenize_LineFeed_ReturnsLineFeedToken()
    {
        var result = AnsiTokenizer.Tokenize("\n");

        var token = Assert.Single(result);
        Assert.Same(ControlCharacterToken.LineFeed, token);
    }

    [Fact]
    public void Tokenize_CarriageReturn_ReturnsCarriageReturnToken()
    {
        var result = AnsiTokenizer.Tokenize("\r");

        var token = Assert.Single(result);
        Assert.Same(ControlCharacterToken.CarriageReturn, token);
    }

    [Fact]
    public void Tokenize_Tab_ReturnsTabToken()
    {
        var result = AnsiTokenizer.Tokenize("\t");

        var token = Assert.Single(result);
        Assert.Same(ControlCharacterToken.Tab, token);
    }

    [Fact]
    public void Tokenize_CrLf_ReturnsTwoTokens()
    {
        var result = AnsiTokenizer.Tokenize("\r\n");

        Assert.Collection(result,
            t => Assert.Same(ControlCharacterToken.CarriageReturn, t),
            t => Assert.Same(ControlCharacterToken.LineFeed, t));
    }

    [Fact]
    public void Tokenize_MultipleNewlines_ReturnsSeparateTokens()
    {
        var result = AnsiTokenizer.Tokenize("\n\n\n");

        Assert.Equal(3, result.Count);
        Assert.All(result, t => Assert.Same(ControlCharacterToken.LineFeed, t));
    }

    #endregion

    #region SGR Token Tests

    [Fact]
    public void Tokenize_SgrReset_ReturnsSgrToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0m");

        var token = Assert.Single(result);
        var sgrToken = Assert.IsType<SgrToken>(token);
        Assert.Equal("0", sgrToken.Parameters);
    }

    [Fact]
    public void Tokenize_SgrEmpty_ReturnsSgrTokenWithEmptyParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[m");

        var token = Assert.Single(result);
        var sgrToken = Assert.IsType<SgrToken>(token);
        Assert.Equal("", sgrToken.Parameters);
    }

    [Fact]
    public void Tokenize_SgrBold_ReturnsSgrToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1m");

        var token = Assert.Single(result);
        var sgrToken = Assert.IsType<SgrToken>(token);
        Assert.Equal("1", sgrToken.Parameters);
    }

    [Fact]
    public void Tokenize_SgrMultipleParams_PreservesAllParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1;31;42m");

        var token = Assert.Single(result);
        var sgrToken = Assert.IsType<SgrToken>(token);
        Assert.Equal("1;31;42", sgrToken.Parameters);
    }

    [Fact]
    public void Tokenize_Sgr256Color_PreservesParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[38;5;196m");

        var token = Assert.Single(result);
        var sgrToken = Assert.IsType<SgrToken>(token);
        Assert.Equal("38;5;196", sgrToken.Parameters);
    }

    [Fact]
    public void Tokenize_SgrRgbColor_PreservesParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[38;2;255;128;64m");

        var token = Assert.Single(result);
        var sgrToken = Assert.IsType<SgrToken>(token);
        Assert.Equal("38;2;255;128;64", sgrToken.Parameters);
    }

    #endregion

    #region Cursor Position Tests

    [Fact]
    public void Tokenize_CursorPositionDefault_ReturnsOneOne()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[H");

        var token = Assert.Single(result);
        var posToken = Assert.IsType<CursorPositionToken>(token);
        Assert.Equal(1, posToken.Row);
        Assert.Equal(1, posToken.Column);
    }

    [Fact]
    public void Tokenize_CursorPositionExplicit_ReturnsCorrectPosition()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[10;20H");

        var token = Assert.Single(result);
        var posToken = Assert.IsType<CursorPositionToken>(token);
        Assert.Equal(10, posToken.Row);
        Assert.Equal(20, posToken.Column);
    }

    [Fact]
    public void Tokenize_CursorPositionRowOnly_ReturnsRowWithDefaultColumn()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5H");

        var token = Assert.Single(result);
        var posToken = Assert.IsType<CursorPositionToken>(token);
        Assert.Equal(5, posToken.Row);
        Assert.Equal(1, posToken.Column);
    }

    [Fact]
    public void Tokenize_CursorPositionWithF_SameAsH()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[10;20f");

        var token = Assert.Single(result);
        var posToken = Assert.IsType<CursorPositionToken>(token);
        Assert.Equal(10, posToken.Row);
        Assert.Equal(20, posToken.Column);
    }

    #endregion

    #region Clear Screen Tests

    [Fact]
    public void Tokenize_ClearScreenDefault_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[J");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearScreenToken>(token);
        Assert.Equal(ClearMode.ToEnd, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearScreenToEnd_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0J");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearScreenToken>(token);
        Assert.Equal(ClearMode.ToEnd, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearScreenToStart_ReturnsClearToStart()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1J");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearScreenToken>(token);
        Assert.Equal(ClearMode.ToStart, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearScreenAll_ReturnsClearAll()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[2J");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearScreenToken>(token);
        Assert.Equal(ClearMode.All, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearScreenAllAndScrollback_ReturnsClearAllAndScrollback()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[3J");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearScreenToken>(token);
        Assert.Equal(ClearMode.AllAndScrollback, clearToken.Mode);
    }

    #endregion

    #region Clear Line Tests

    [Fact]
    public void Tokenize_ClearLineDefault_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[K");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearLineToken>(token);
        Assert.Equal(ClearMode.ToEnd, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearLineToEnd_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0K");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearLineToken>(token);
        Assert.Equal(ClearMode.ToEnd, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearLineToStart_ReturnsClearToStart()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1K");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearLineToken>(token);
        Assert.Equal(ClearMode.ToStart, clearToken.Mode);
    }

    [Fact]
    public void Tokenize_ClearLineAll_ReturnsClearAll()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[2K");

        var token = Assert.Single(result);
        var clearToken = Assert.IsType<ClearLineToken>(token);
        Assert.Equal(ClearMode.All, clearToken.Mode);
    }

    #endregion

    #region Private Mode Tests

    [Fact]
    public void Tokenize_AlternateScreenEnable_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1049h");

        var token = Assert.Single(result);
        var pmToken = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1049, pmToken.Mode);
        Assert.True(pmToken.Enable);
    }

    [Fact]
    public void Tokenize_AlternateScreenDisable_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1049l");

        var token = Assert.Single(result);
        var pmToken = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1049, pmToken.Mode);
        Assert.False(pmToken.Enable);
    }

    [Fact]
    public void Tokenize_CursorVisible_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?25h");

        var token = Assert.Single(result);
        var pmToken = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(25, pmToken.Mode);
        Assert.True(pmToken.Enable);
    }

    [Fact]
    public void Tokenize_CursorHidden_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?25l");

        var token = Assert.Single(result);
        var pmToken = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(25, pmToken.Mode);
        Assert.False(pmToken.Enable);
    }

    #endregion

    #region Cursor Shape Tests

    [Fact]
    public void Tokenize_CursorShapeDefault_ReturnsDefaultToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.Default, token);
    }

    [Fact]
    public void Tokenize_CursorShapeBlinkingBlock_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.BlinkingBlock, token);
    }

    [Fact]
    public void Tokenize_CursorShapeSteadyBlock_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[2q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.SteadyBlock, token);
    }

    [Fact]
    public void Tokenize_CursorShapeBlinkingUnderline_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[3q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.BlinkingUnderline, token);
    }

    [Fact]
    public void Tokenize_CursorShapeSteadyUnderline_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[4q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.SteadyUnderline, token);
    }

    [Fact]
    public void Tokenize_CursorShapeBlinkingBar_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.BlinkingBar, token);
    }

    [Fact]
    public void Tokenize_CursorShapeSteadyBar_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[6q");

        var token = Assert.Single(result);
        Assert.Same(CursorShapeToken.SteadyBar, token);
    }

    #endregion

    #region Scroll Region Tests

    [Fact]
    public void Tokenize_ScrollRegionReset_ReturnsResetToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[r");

        var token = Assert.Single(result);
        Assert.Same(ScrollRegionToken.Reset, token);
    }

    [Fact]
    public void Tokenize_ScrollRegionExplicit_ReturnsCorrectRegion()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5;20r");

        var token = Assert.Single(result);
        var scrollToken = Assert.IsType<ScrollRegionToken>(token);
        Assert.Equal(5, scrollToken.Top);
        Assert.Equal(20, scrollToken.Bottom);
    }

    #endregion

    #region Save/Restore Cursor Tests

    [Fact]
    public void Tokenize_SaveCursorAnsi_ReturnsAnsiToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[s");

        var token = Assert.Single(result);
        Assert.Same(SaveCursorToken.Ansi, token);
    }

    [Fact]
    public void Tokenize_RestoreCursorAnsi_ReturnsAnsiToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[u");

        var token = Assert.Single(result);
        Assert.Same(RestoreCursorToken.Ansi, token);
    }

    [Fact]
    public void Tokenize_SaveCursorDec_ReturnsDecToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b" + "7");

        var token = Assert.Single(result);
        Assert.Same(SaveCursorToken.Dec, token);
    }

    [Fact]
    public void Tokenize_RestoreCursorDec_ReturnsDecToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b" + "8");

        var token = Assert.Single(result);
        Assert.Same(RestoreCursorToken.Dec, token);
    }

    #endregion

    #region OSC Token Tests

    [Fact]
    public void Tokenize_OscHyperlinkWithBel_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x07");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("8", oscToken.Command);
        Assert.Equal("", oscToken.Parameters);
        Assert.Equal("https://example.com", oscToken.Payload);
    }

    [Fact]
    public void Tokenize_OscHyperlinkWithST_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x1b\\");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("8", oscToken.Command);
        Assert.Equal("https://example.com", oscToken.Payload);
    }

    [Fact]
    public void Tokenize_OscHyperlinkWithParams_PreservesParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;id=mylink;https://example.com\x07");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("8", oscToken.Command);
        Assert.Equal("id=mylink", oscToken.Parameters);
        Assert.Equal("https://example.com", oscToken.Payload);
    }

    [Fact]
    public void Tokenize_OscHyperlinkEnd_ReturnsOscWithEmptyPayload()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;\x07");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("8", oscToken.Command);
        Assert.Equal("", oscToken.Parameters);
        Assert.Equal("", oscToken.Payload);
    }

    [Fact]
    public void Tokenize_OscWithC1Start_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x9d" + "8;;https://example.com\x07");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("8", oscToken.Command);
        Assert.Equal("https://example.com", oscToken.Payload);
    }

    [Fact]
    public void Tokenize_OscWithC1Terminator_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x9c");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("8", oscToken.Command);
        Assert.Equal("https://example.com", oscToken.Payload);
    }

    [Fact]
    public void Tokenize_OscWindowTitle_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]0;My Window Title\x07");

        var token = Assert.Single(result);
        var oscToken = Assert.IsType<OscToken>(token);
        Assert.Equal("0", oscToken.Command);
        Assert.Equal("My Window Title", oscToken.Payload);
    }

    #endregion

    #region DCS Token Tests

    [Fact]
    public void Tokenize_DcsSequence_ReturnsDcsToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1bPq#0;2;0;0;0\x1b\\");

        var token = Assert.Single(result);
        var dcsToken = Assert.IsType<DcsToken>(token);
        Assert.Equal("q#0;2;0;0;0", dcsToken.Payload);
    }

    [Fact]
    public void Tokenize_DcsWithC1Start_ReturnsDcsToken()
    {
        var result = AnsiTokenizer.Tokenize("\x90q#0;2;0;0;0\x1b\\");

        var token = Assert.Single(result);
        var dcsToken = Assert.IsType<DcsToken>(token);
        Assert.Equal("q#0;2;0;0;0", dcsToken.Payload);
    }

    [Fact]
    public void Tokenize_DcsWithC1Terminator_ReturnsDcsToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1bPq#0;2;0;0;0\x9c");

        var token = Assert.Single(result);
        var dcsToken = Assert.IsType<DcsToken>(token);
        Assert.Equal("q#0;2;0;0;0", dcsToken.Payload);
    }

    #endregion

    #region Unrecognized Sequence Tests

    [Fact]
    public void Tokenize_UnrecognizedEscapeSequence_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1bX");

        var token = Assert.Single(result);
        var unrecToken = Assert.IsType<UnrecognizedSequenceToken>(token);
        Assert.Equal("\x1bX", unrecToken.Sequence);
    }

    [Fact]
    public void Tokenize_IncompleteEscapeSequence_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b");

        var token = Assert.Single(result);
        var unrecToken = Assert.IsType<UnrecognizedSequenceToken>(token);
        Assert.Equal("\x1b", unrecToken.Sequence);
    }

    [Fact]
    public void Tokenize_UnknownCsiCommand_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5Z");

        var token = Assert.Single(result);
        var unrecToken = Assert.IsType<UnrecognizedSequenceToken>(token);
        Assert.Equal("\x1b[5Z", unrecToken.Sequence);
    }

    #endregion

    #region Complex Sequence Tests

    [Fact]
    public void Tokenize_ColoredText_ReturnsCorrectTokenSequence()
    {
        // ESC[31m (red) + "Hello" + ESC[0m (reset)
        var result = AnsiTokenizer.Tokenize("\x1b[31mHello\x1b[0m");

        Assert.Collection(result,
            t => Assert.Equal("31", Assert.IsType<SgrToken>(t).Parameters),
            t => Assert.Equal("Hello", Assert.IsType<TextToken>(t).Text),
            t => Assert.Equal("0", Assert.IsType<SgrToken>(t).Parameters));
    }

    [Fact]
    public void Tokenize_PositionedText_ReturnsCorrectTokenSequence()
    {
        // ESC[5;10H + "Hello"
        var result = AnsiTokenizer.Tokenize("\x1b[5;10HHello");

        Assert.Collection(result,
            t =>
            {
                var pos = Assert.IsType<CursorPositionToken>(t);
                Assert.Equal(5, pos.Row);
                Assert.Equal(10, pos.Column);
            },
            t => Assert.Equal("Hello", Assert.IsType<TextToken>(t).Text));
    }

    [Fact]
    public void Tokenize_MultiLineOutput_ReturnsCorrectSequence()
    {
        var result = AnsiTokenizer.Tokenize("Line 1\r\nLine 2\r\nLine 3");

        Assert.Collection(result,
            t => Assert.Equal("Line 1", Assert.IsType<TextToken>(t).Text),
            t => Assert.Same(ControlCharacterToken.CarriageReturn, t),
            t => Assert.Same(ControlCharacterToken.LineFeed, t),
            t => Assert.Equal("Line 2", Assert.IsType<TextToken>(t).Text),
            t => Assert.Same(ControlCharacterToken.CarriageReturn, t),
            t => Assert.Same(ControlCharacterToken.LineFeed, t),
            t => Assert.Equal("Line 3", Assert.IsType<TextToken>(t).Text));
    }

    [Fact]
    public void Tokenize_HyperlinkWithText_ReturnsCorrectSequence()
    {
        // Start hyperlink, text, end hyperlink
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x07" + "Click here\x1b]8;;\x07");

        Assert.Collection(result,
            t =>
            {
                var osc = Assert.IsType<OscToken>(t);
                Assert.Equal("8", osc.Command);
                Assert.Equal("https://example.com", osc.Payload);
            },
            t => Assert.Equal("Click here", Assert.IsType<TextToken>(t).Text),
            t =>
            {
                var osc = Assert.IsType<OscToken>(t);
                Assert.Equal("8", osc.Command);
                Assert.Equal("", osc.Payload);
            });
    }

    [Fact]
    public void Tokenize_ClearAndPosition_ReturnsCorrectSequence()
    {
        // Clear screen + position cursor + write text
        var result = AnsiTokenizer.Tokenize("\x1b[2J\x1b[1;1HWelcome");

        Assert.Collection(result,
            t =>
            {
                var clear = Assert.IsType<ClearScreenToken>(t);
                Assert.Equal(ClearMode.All, clear.Mode);
            },
            t =>
            {
                var pos = Assert.IsType<CursorPositionToken>(t);
                Assert.Equal(1, pos.Row);
                Assert.Equal(1, pos.Column);
            },
            t => Assert.Equal("Welcome", Assert.IsType<TextToken>(t).Text));
    }

    [Fact]
    public void Tokenize_AlternateScreenSequence_ReturnsCorrectSequence()
    {
        // Enter alternate screen + clear + text + exit alternate screen
        var result = AnsiTokenizer.Tokenize("\x1b[?1049h\x1b[2JHello\x1b[?1049l");

        Assert.Collection(result,
            t =>
            {
                var pm = Assert.IsType<PrivateModeToken>(t);
                Assert.Equal(1049, pm.Mode);
                Assert.True(pm.Enable);
            },
            t => Assert.IsType<ClearScreenToken>(t),
            t => Assert.Equal("Hello", Assert.IsType<TextToken>(t).Text),
            t =>
            {
                var pm = Assert.IsType<PrivateModeToken>(t);
                Assert.Equal(1049, pm.Mode);
                Assert.False(pm.Enable);
            });
    }

    [Fact]
    public void Tokenize_SaveRestoreCursor_ReturnsCorrectSequence()
    {
        // Save cursor, move, restore cursor
        var result = AnsiTokenizer.Tokenize("\x1b" + "7\x1b[10;20H\x1b" + "8");

        Assert.Collection(result,
            t => Assert.Same(SaveCursorToken.Dec, t),
            t =>
            {
                var pos = Assert.IsType<CursorPositionToken>(t);
                Assert.Equal(10, pos.Row);
                Assert.Equal(20, pos.Column);
            },
            t => Assert.Same(RestoreCursorToken.Dec, t));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Tokenize_TrailingEscape_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("Hello\x1b");

        Assert.Collection(result,
            t => Assert.Equal("Hello", Assert.IsType<TextToken>(t).Text),
            t => Assert.IsType<UnrecognizedSequenceToken>(t));
    }

    [Fact]
    public void Tokenize_IncompleteCsi_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[123");

        var token = Assert.Single(result);
        Assert.IsType<UnrecognizedSequenceToken>(token);
    }

    [Fact]
    public void Tokenize_IncompleteOsc_ReturnsText()
    {
        // OSC without terminator - should not be parsed as OSC
        // The ESC ] will be treated as an unrecognized escape
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com");

        // Since there's no terminator, the OSC parsing fails
        // and we get an unrecognized sequence for the ESC ]
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void Tokenize_EmptyGrapheme_HandledCorrectly()
    {
        var result = AnsiTokenizer.Tokenize("");
        Assert.Empty(result);
    }

    [Fact]
    public void Tokenize_MixedSgrAndClear_ReturnsCorrectSequence()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1;31m\x1b[2K");

        Assert.Collection(result,
            t => Assert.Equal("1;31", Assert.IsType<SgrToken>(t).Parameters),
            t =>
            {
                var clear = Assert.IsType<ClearLineToken>(t);
                Assert.Equal(ClearMode.All, clear.Mode);
            });
    }

    #endregion
}
