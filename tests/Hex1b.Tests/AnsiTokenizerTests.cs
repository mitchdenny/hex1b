using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class AnsiTokenizerTests
{
    #region Basic Text Tests

    [TestMethod]
    public void Tokenize_EmptyString_ReturnsEmptyList()
    {
        var result = AnsiTokenizer.Tokenize("");
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Tokenize_NullString_ReturnsEmptyList()
    {
        var result = AnsiTokenizer.Tokenize(null!);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Tokenize_PlainText_ReturnsSingleTextToken()
    {
        var result = AnsiTokenizer.Tokenize("Hello, World!");

        var token = TestSeq.Single(result);
        var textToken = TestSeq.IsType<TextToken>(token);
        Assert.AreEqual("Hello, World!", textToken.Text);
    }

    [TestMethod]
    public void Tokenize_ConsecutiveTextAndControlChars_BatchesTextCorrectly()
    {
        var result = AnsiTokenizer.Tokenize("Hello\nWorld");

        TestSeq.Collection(result, t => Assert.AreEqual("Hello", TestSeq.IsType<TextToken>(t).Text), t => Assert.AreSame(ControlCharacterToken.LineFeed, t), t => Assert.AreEqual("World", TestSeq.IsType<TextToken>(t).Text));
    }

    [TestMethod]
    public void Tokenize_UnicodeEmoji_PreservesGraphemeClusters()
    {
        // Family emoji is a complex grapheme cluster
        var result = AnsiTokenizer.Tokenize("Hello 👨‍👩‍👧 World");

        var token = TestSeq.Single(result);
        var textToken = TestSeq.IsType<TextToken>(token);
        Assert.AreEqual("Hello 👨‍👩‍👧 World", textToken.Text);
    }

    #endregion

    #region Control Character Tests

    [TestMethod]
    public void Tokenize_LineFeed_ReturnsLineFeedToken()
    {
        var result = AnsiTokenizer.Tokenize("\n");

        var token = TestSeq.Single(result);
        Assert.AreSame(ControlCharacterToken.LineFeed, token);
    }

    [TestMethod]
    public void Tokenize_CarriageReturn_ReturnsCarriageReturnToken()
    {
        var result = AnsiTokenizer.Tokenize("\r");

        var token = TestSeq.Single(result);
        Assert.AreSame(ControlCharacterToken.CarriageReturn, token);
    }

    [TestMethod]
    public void Tokenize_Tab_ReturnsTabToken()
    {
        var result = AnsiTokenizer.Tokenize("\t");

        var token = TestSeq.Single(result);
        Assert.AreSame(ControlCharacterToken.Tab, token);
    }

    [TestMethod]
    public void Tokenize_CrLf_ReturnsTwoTokens()
    {
        var result = AnsiTokenizer.Tokenize("\r\n");

        TestSeq.Collection(result, t => Assert.AreSame(ControlCharacterToken.CarriageReturn, t), t => Assert.AreSame(ControlCharacterToken.LineFeed, t));
    }

    [TestMethod]
    public void Tokenize_MultipleNewlines_ReturnsSeparateTokens()
    {
        var result = AnsiTokenizer.Tokenize("\n\n\n");

        Assert.AreEqual(3, result.Count);
        TestSeq.All(result, t => Assert.AreSame(ControlCharacterToken.LineFeed, t));
    }

    #endregion

    #region SGR Token Tests

    [TestMethod]
    public void Tokenize_SgrReset_ReturnsSgrToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0m");

        var token = TestSeq.Single(result);
        var sgrToken = TestSeq.IsType<SgrToken>(token);
        Assert.AreEqual("0", sgrToken.Parameters);
    }

    [TestMethod]
    public void Tokenize_SgrEmpty_ReturnsSgrTokenWithEmptyParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[m");

        var token = TestSeq.Single(result);
        var sgrToken = TestSeq.IsType<SgrToken>(token);
        Assert.AreEqual("", sgrToken.Parameters);
    }

    [TestMethod]
    public void Tokenize_SgrBold_ReturnsSgrToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1m");

        var token = TestSeq.Single(result);
        var sgrToken = TestSeq.IsType<SgrToken>(token);
        Assert.AreEqual("1", sgrToken.Parameters);
    }

    [TestMethod]
    public void Tokenize_SgrMultipleParams_PreservesAllParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1;31;42m");

        var token = TestSeq.Single(result);
        var sgrToken = TestSeq.IsType<SgrToken>(token);
        Assert.AreEqual("1;31;42", sgrToken.Parameters);
    }

    [TestMethod]
    public void Tokenize_Sgr256Color_PreservesParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[38;5;196m");

        var token = TestSeq.Single(result);
        var sgrToken = TestSeq.IsType<SgrToken>(token);
        Assert.AreEqual("38;5;196", sgrToken.Parameters);
    }

    [TestMethod]
    public void Tokenize_SgrRgbColor_PreservesParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[38;2;255;128;64m");

        var token = TestSeq.Single(result);
        var sgrToken = TestSeq.IsType<SgrToken>(token);
        Assert.AreEqual("38;2;255;128;64", sgrToken.Parameters);
    }

    #endregion

    #region Cursor Position Tests

    [TestMethod]
    public void Tokenize_CursorPositionDefault_ReturnsOneOne()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[H");

        var token = TestSeq.Single(result);
        var posToken = TestSeq.IsType<CursorPositionToken>(token);
        Assert.AreEqual(1, posToken.Row);
        Assert.AreEqual(1, posToken.Column);
    }

    [TestMethod]
    public void Tokenize_CursorPositionExplicit_ReturnsCorrectPosition()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[10;20H");

        var token = TestSeq.Single(result);
        var posToken = TestSeq.IsType<CursorPositionToken>(token);
        Assert.AreEqual(10, posToken.Row);
        Assert.AreEqual(20, posToken.Column);
    }

    [TestMethod]
    public void Tokenize_CursorPositionRowOnly_ReturnsRowWithDefaultColumn()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5H");

        var token = TestSeq.Single(result);
        var posToken = TestSeq.IsType<CursorPositionToken>(token);
        Assert.AreEqual(5, posToken.Row);
        Assert.AreEqual(1, posToken.Column);
    }

    [TestMethod]
    public void Tokenize_CursorPositionWithF_SameAsH()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[10;20f");

        var token = TestSeq.Single(result);
        var posToken = TestSeq.IsType<CursorPositionToken>(token);
        Assert.AreEqual(10, posToken.Row);
        Assert.AreEqual(20, posToken.Column);
    }

    #endregion

    #region Clear Screen Tests

    [TestMethod]
    public void Tokenize_ClearScreenDefault_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[J");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearScreenToken>(token);
        Assert.AreEqual(ClearMode.ToEnd, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearScreenToEnd_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0J");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearScreenToken>(token);
        Assert.AreEqual(ClearMode.ToEnd, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearScreenToStart_ReturnsClearToStart()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1J");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearScreenToken>(token);
        Assert.AreEqual(ClearMode.ToStart, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearScreenAll_ReturnsClearAll()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[2J");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearScreenToken>(token);
        Assert.AreEqual(ClearMode.All, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearScreenAllAndScrollback_ReturnsClearAllAndScrollback()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[3J");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearScreenToken>(token);
        Assert.AreEqual(ClearMode.AllAndScrollback, clearToken.Mode);
    }

    #endregion

    #region Clear Line Tests

    [TestMethod]
    public void Tokenize_ClearLineDefault_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[K");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearLineToken>(token);
        Assert.AreEqual(ClearMode.ToEnd, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearLineToEnd_ReturnsClearToEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0K");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearLineToken>(token);
        Assert.AreEqual(ClearMode.ToEnd, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearLineToStart_ReturnsClearToStart()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1K");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearLineToken>(token);
        Assert.AreEqual(ClearMode.ToStart, clearToken.Mode);
    }

    [TestMethod]
    public void Tokenize_ClearLineAll_ReturnsClearAll()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[2K");

        var token = TestSeq.Single(result);
        var clearToken = TestSeq.IsType<ClearLineToken>(token);
        Assert.AreEqual(ClearMode.All, clearToken.Mode);
    }

    #endregion

    #region Private Mode Tests

    [TestMethod]
    public void Tokenize_AlternateScreenEnable_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1049h");

        var token = TestSeq.Single(result);
        var pmToken = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1049, pmToken.Mode);
        Assert.IsTrue(pmToken.Enable);
    }

    [TestMethod]
    public void Tokenize_AlternateScreenDisable_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1049l");

        var token = TestSeq.Single(result);
        var pmToken = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1049, pmToken.Mode);
        Assert.IsFalse(pmToken.Enable);
    }

    [TestMethod]
    public void Tokenize_CursorVisible_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?25h");

        var token = TestSeq.Single(result);
        var pmToken = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(25, pmToken.Mode);
        Assert.IsTrue(pmToken.Enable);
    }

    [TestMethod]
    public void Tokenize_CursorHidden_ReturnsPrivateModeToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?25l");

        var token = TestSeq.Single(result);
        var pmToken = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(25, pmToken.Mode);
        Assert.IsFalse(pmToken.Enable);
    }

    [TestMethod]
    public void Tokenize_MultiplePrivateModesEnable_EmitsTokenPerMode()
    {
        // edit.exe sends this to enable mouse tracking + SGR mouse + bracketed paste
        var result = AnsiTokenizer.Tokenize("\x1b[?1002;1006;2004h");

        Assert.AreEqual(3, result.Count);
        var pm0 = TestSeq.IsType<PrivateModeToken>(result[0]);
        Assert.AreEqual(1002, pm0.Mode);
        Assert.IsTrue(pm0.Enable);
        var pm1 = TestSeq.IsType<PrivateModeToken>(result[1]);
        Assert.AreEqual(1006, pm1.Mode);
        Assert.IsTrue(pm1.Enable);
        var pm2 = TestSeq.IsType<PrivateModeToken>(result[2]);
        Assert.AreEqual(2004, pm2.Mode);
        Assert.IsTrue(pm2.Enable);
    }

    [TestMethod]
    public void Tokenize_MultiplePrivateModesDisable_EmitsTokenPerMode()
    {
        // edit.exe cleanup sequence
        var result = AnsiTokenizer.Tokenize("\x1b[?1002;1006;2004l");

        Assert.AreEqual(3, result.Count);
        var pm0 = TestSeq.IsType<PrivateModeToken>(result[0]);
        Assert.AreEqual(1002, pm0.Mode);
        Assert.IsFalse(pm0.Enable);
        var pm1 = TestSeq.IsType<PrivateModeToken>(result[1]);
        Assert.AreEqual(1006, pm1.Mode);
        Assert.IsFalse(pm1.Enable);
        var pm2 = TestSeq.IsType<PrivateModeToken>(result[2]);
        Assert.AreEqual(2004, pm2.Mode);
        Assert.IsFalse(pm2.Enable);
    }

    [TestMethod]
    public void Tokenize_TwoPrivateModesEnable_EmitsTokenPerMode()
    {
        // Common pattern: alt screen + cursor hide
        var result = AnsiTokenizer.Tokenize("\x1b[?1049;25h");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1049, TestSeq.IsType<PrivateModeToken>(result[0]).Mode);
        Assert.AreEqual(25, TestSeq.IsType<PrivateModeToken>(result[1]).Mode);
    }

    [TestMethod]
    public void Tokenize_MultipleStandardModesEnable_EmitsTokenPerMode()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[4;20h");

        Assert.AreEqual(2, result.Count);
        var sm0 = TestSeq.IsType<StandardModeToken>(result[0]);
        Assert.AreEqual(4, sm0.Mode);
        Assert.IsTrue(sm0.Enable);
        var sm1 = TestSeq.IsType<StandardModeToken>(result[1]);
        Assert.AreEqual(20, sm1.Mode);
        Assert.IsTrue(sm1.Enable);
    }

    [TestMethod]
    public void Tokenize_MouseTrackingModes_AllRecognized()
    {
        // Verify all mouse tracking private modes are correctly tokenized
        var result1000 = AnsiTokenizer.Tokenize("\x1b[?1000h");
        Assert.AreEqual(1000, TestSeq.IsType<PrivateModeToken>(TestSeq.Single(result1000)).Mode);

        var result1002 = AnsiTokenizer.Tokenize("\x1b[?1002h");
        Assert.AreEqual(1002, TestSeq.IsType<PrivateModeToken>(TestSeq.Single(result1002)).Mode);

        var result1003 = AnsiTokenizer.Tokenize("\x1b[?1003h");
        Assert.AreEqual(1003, TestSeq.IsType<PrivateModeToken>(TestSeq.Single(result1003)).Mode);

        var result1006 = AnsiTokenizer.Tokenize("\x1b[?1006h");
        Assert.AreEqual(1006, TestSeq.IsType<PrivateModeToken>(TestSeq.Single(result1006)).Mode);
    }

    #endregion

    #region Cursor Shape Tests

    [TestMethod]
    public void Tokenize_CursorShapeDefault_ReturnsDefaultToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[0q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.Default, token);
    }

    [TestMethod]
    public void Tokenize_CursorShapeBlinkingBlock_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.BlinkingBlock, token);
    }

    [TestMethod]
    public void Tokenize_CursorShapeSteadyBlock_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[2q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.SteadyBlock, token);
    }

    [TestMethod]
    public void Tokenize_CursorShapeBlinkingUnderline_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[3q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.BlinkingUnderline, token);
    }

    [TestMethod]
    public void Tokenize_CursorShapeSteadyUnderline_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[4q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.SteadyUnderline, token);
    }

    [TestMethod]
    public void Tokenize_CursorShapeBlinkingBar_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.BlinkingBar, token);
    }

    [TestMethod]
    public void Tokenize_CursorShapeSteadyBar_ReturnsCorrectToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[6q");

        var token = TestSeq.Single(result);
        Assert.AreSame(CursorShapeToken.SteadyBar, token);
    }

    #endregion

    #region Scroll Region Tests

    [TestMethod]
    public void Tokenize_ScrollRegionReset_ReturnsResetToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[r");

        var token = TestSeq.Single(result);
        Assert.AreSame(ScrollRegionToken.Reset, token);
    }

    [TestMethod]
    public void Tokenize_ScrollRegionExplicit_ReturnsCorrectRegion()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[5;20r");

        var token = TestSeq.Single(result);
        var scrollToken = TestSeq.IsType<ScrollRegionToken>(token);
        Assert.AreEqual(5, scrollToken.Top);
        Assert.AreEqual(20, scrollToken.Bottom);
    }

    #endregion

    #region Save/Restore Cursor Tests

    [TestMethod]
    public void Tokenize_SaveCursorAnsi_ReturnsAnsiToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[s");

        var token = TestSeq.Single(result);
        Assert.AreSame(SaveCursorToken.Ansi, token);
    }

    [TestMethod]
    public void Tokenize_RestoreCursorAnsi_ReturnsAnsiToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[u");

        var token = TestSeq.Single(result);
        Assert.AreSame(RestoreCursorToken.Ansi, token);
    }

    [TestMethod]
    public void Tokenize_SaveCursorDec_ReturnsDecToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b" + "7");

        var token = TestSeq.Single(result);
        Assert.AreSame(SaveCursorToken.Dec, token);
    }

    [TestMethod]
    public void Tokenize_RestoreCursorDec_ReturnsDecToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b" + "8");

        var token = TestSeq.Single(result);
        Assert.AreSame(RestoreCursorToken.Dec, token);
    }

    #endregion

    #region OSC Token Tests

    [TestMethod]
    public void Tokenize_OscHyperlinkWithBel_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("8", oscToken.Command);
        Assert.AreEqual("", oscToken.Parameters);
        Assert.AreEqual("https://example.com", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_OscHyperlinkWithST_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x1b\\");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("8", oscToken.Command);
        Assert.AreEqual("https://example.com", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_OscHyperlinkWithParams_PreservesParams()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;id=mylink;https://example.com\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("8", oscToken.Command);
        Assert.AreEqual("id=mylink", oscToken.Parameters);
        Assert.AreEqual("https://example.com", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_OscHyperlinkEnd_ReturnsOscWithEmptyPayload()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("8", oscToken.Command);
        Assert.AreEqual("", oscToken.Parameters);
        Assert.AreEqual("", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_OscWithC1Start_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x9d" + "8;;https://example.com\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("8", oscToken.Command);
        Assert.AreEqual("https://example.com", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_OscWithC1Terminator_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x9c");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("8", oscToken.Command);
        Assert.AreEqual("https://example.com", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_OscWindowTitle_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]0;My Window Title\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("0", oscToken.Command);
        Assert.AreEqual("My Window Title", oscToken.Payload);
    }

    #endregion

    #region DCS Token Tests

    [TestMethod]
    public void Tokenize_DcsSequence_ReturnsDcsToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1bPq#0;2;0;0;0\x1b\\");

        var token = TestSeq.Single(result);
        var dcsToken = TestSeq.IsType<DcsToken>(token);
        Assert.AreEqual("q#0;2;0;0;0", dcsToken.Payload);
    }

    [TestMethod]
    public void Tokenize_DcsWithC1Start_ReturnsDcsToken()
    {
        var result = AnsiTokenizer.Tokenize("\x90q#0;2;0;0;0\x1b\\");

        var token = TestSeq.Single(result);
        var dcsToken = TestSeq.IsType<DcsToken>(token);
        Assert.AreEqual("q#0;2;0;0;0", dcsToken.Payload);
    }

    [TestMethod]
    public void Tokenize_DcsWithC1Terminator_ReturnsDcsToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1bPq#0;2;0;0;0\x9c");

        var token = TestSeq.Single(result);
        var dcsToken = TestSeq.IsType<DcsToken>(token);
        Assert.AreEqual("q#0;2;0;0;0", dcsToken.Payload);
    }

    #endregion

    #region Unrecognized Sequence Tests

    [TestMethod]
    public void Tokenize_UnrecognizedEscapeSequence_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1bX");

        var token = TestSeq.Single(result);
        var unrecToken = TestSeq.IsType<UnrecognizedSequenceToken>(token);
        Assert.AreEqual("\x1bX", unrecToken.Sequence);
    }

    [TestMethod]
    public void Tokenize_IncompleteEscapeSequence_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b");

        var token = TestSeq.Single(result);
        var unrecToken = TestSeq.IsType<UnrecognizedSequenceToken>(token);
        Assert.AreEqual("\x1b", unrecToken.Sequence);
    }

    [TestMethod]
    public void Tokenize_UnknownCsiCommand_ReturnsUnrecognizedToken()
    {
        // Use CSI W which is not a standard command
        var result = AnsiTokenizer.Tokenize("\x1b[5W");

        var token = TestSeq.Single(result);
        var unrecToken = TestSeq.IsType<UnrecognizedSequenceToken>(token);
        Assert.AreEqual("\x1b[5W", unrecToken.Sequence);
    }
    
    [TestMethod]
    public void Tokenize_BackTab_ReturnsBackTabToken()
    {
        // CSI Z is Shift+Tab (Backtab)
        var result = AnsiTokenizer.Tokenize("\x1b[Z");

        var token = TestSeq.Single(result);
        Assert.AreSame(BackTabToken.Instance, token);
    }

    #endregion

    #region Complex Sequence Tests

    [TestMethod]
    public void Tokenize_ColoredText_ReturnsCorrectTokenSequence()
    {
        // ESC[31m (red) + "Hello" + ESC[0m (reset)
        var result = AnsiTokenizer.Tokenize("\x1b[31mHello\x1b[0m");

        TestSeq.Collection(result, t => Assert.AreEqual("31", TestSeq.IsType<SgrToken>(t).Parameters), t => Assert.AreEqual("Hello", TestSeq.IsType<TextToken>(t).Text), t => Assert.AreEqual("0", TestSeq.IsType<SgrToken>(t).Parameters));
    }

    [TestMethod]
    public void Tokenize_PositionedText_ReturnsCorrectTokenSequence()
    {
        // ESC[5;10H + "Hello"
        var result = AnsiTokenizer.Tokenize("\x1b[5;10HHello");

        TestSeq.Collection(result, t =>
            {
                var pos = TestSeq.IsType<CursorPositionToken>(t);
                Assert.AreEqual(5, pos.Row);
                Assert.AreEqual(10, pos.Column);
            }, t => Assert.AreEqual("Hello", TestSeq.IsType<TextToken>(t).Text));
    }

    [TestMethod]
    public void Tokenize_MultiLineOutput_ReturnsCorrectSequence()
    {
        var result = AnsiTokenizer.Tokenize("Line 1\r\nLine 2\r\nLine 3");

        TestSeq.Collection(result, t => Assert.AreEqual("Line 1", TestSeq.IsType<TextToken>(t).Text), t => Assert.AreSame(ControlCharacterToken.CarriageReturn, t), t => Assert.AreSame(ControlCharacterToken.LineFeed, t), t => Assert.AreEqual("Line 2", TestSeq.IsType<TextToken>(t).Text), t => Assert.AreSame(ControlCharacterToken.CarriageReturn, t), t => Assert.AreSame(ControlCharacterToken.LineFeed, t), t => Assert.AreEqual("Line 3", TestSeq.IsType<TextToken>(t).Text));
    }

    [TestMethod]
    public void Tokenize_HyperlinkWithText_ReturnsCorrectSequence()
    {
        // Start hyperlink, text, end hyperlink
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com\x07" + "Click here\x1b]8;;\x07");

        TestSeq.Collection(result, t =>
            {
                var osc = TestSeq.IsType<OscToken>(t);
                Assert.AreEqual("8", osc.Command);
                Assert.AreEqual("https://example.com", osc.Payload);
            }, t => Assert.AreEqual("Click here", TestSeq.IsType<TextToken>(t).Text), t =>
            {
                var osc = TestSeq.IsType<OscToken>(t);
                Assert.AreEqual("8", osc.Command);
                Assert.AreEqual("", osc.Payload);
            });
    }

    [TestMethod]
    public void Tokenize_ClearAndPosition_ReturnsCorrectSequence()
    {
        // Clear screen + position cursor + write text
        var result = AnsiTokenizer.Tokenize("\x1b[2J\x1b[1;1HWelcome");

        TestSeq.Collection(result, t =>
            {
                var clear = TestSeq.IsType<ClearScreenToken>(t);
                Assert.AreEqual(ClearMode.All, clear.Mode);
            }, t =>
            {
                var pos = TestSeq.IsType<CursorPositionToken>(t);
                Assert.AreEqual(1, pos.Row);
                Assert.AreEqual(1, pos.Column);
            }, t => Assert.AreEqual("Welcome", TestSeq.IsType<TextToken>(t).Text));
    }

    [TestMethod]
    public void Tokenize_AlternateScreenSequence_ReturnsCorrectSequence()
    {
        // Enter alternate screen + clear + text + exit alternate screen
        var result = AnsiTokenizer.Tokenize("\x1b[?1049h\x1b[2JHello\x1b[?1049l");

        TestSeq.Collection(result, t =>
            {
                var pm = TestSeq.IsType<PrivateModeToken>(t);
                Assert.AreEqual(1049, pm.Mode);
                Assert.IsTrue(pm.Enable);
            }, t => TestSeq.IsType<ClearScreenToken>(t), t => Assert.AreEqual("Hello", TestSeq.IsType<TextToken>(t).Text), t =>
            {
                var pm = TestSeq.IsType<PrivateModeToken>(t);
                Assert.AreEqual(1049, pm.Mode);
                Assert.IsFalse(pm.Enable);
            });
    }

    [TestMethod]
    public void Tokenize_SaveRestoreCursor_ReturnsCorrectSequence()
    {
        // Save cursor, move, restore cursor
        var result = AnsiTokenizer.Tokenize("\x1b" + "7\x1b[10;20H\x1b" + "8");

        TestSeq.Collection(result, t => Assert.AreSame(SaveCursorToken.Dec, t), t =>
            {
                var pos = TestSeq.IsType<CursorPositionToken>(t);
                Assert.AreEqual(10, pos.Row);
                Assert.AreEqual(20, pos.Column);
            }, t => Assert.AreSame(RestoreCursorToken.Dec, t));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Tokenize_TrailingEscape_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("Hello\x1b");

        TestSeq.Collection(result, t => Assert.AreEqual("Hello", TestSeq.IsType<TextToken>(t).Text), t => TestSeq.IsType<UnrecognizedSequenceToken>(t));
    }

    [TestMethod]
    public void Tokenize_IncompleteCsi_ReturnsUnrecognizedToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[123");

        var token = TestSeq.Single(result);
        TestSeq.IsType<UnrecognizedSequenceToken>(token);
    }

    [TestMethod]
    public void Tokenize_IncompleteOsc_ReturnsText()
    {
        // OSC without terminator - should not be parsed as OSC
        // The ESC ] will be treated as an unrecognized escape
        var result = AnsiTokenizer.Tokenize("\x1b]8;;https://example.com");

        // Since there's no terminator, the OSC parsing fails
        // and we get an unrecognized sequence for the ESC ]
        Assert.IsTrue(result.Count >= 1);
    }

    [TestMethod]
    public void Tokenize_EmptyGrapheme_HandledCorrectly()
    {
        var result = AnsiTokenizer.Tokenize("");
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Tokenize_MixedSgrAndClear_ReturnsCorrectSequence()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[1;31m\x1b[2K");

        TestSeq.Collection(result, t => Assert.AreEqual("1;31", TestSeq.IsType<SgrToken>(t).Parameters), t =>
            {
                var clear = TestSeq.IsType<ClearLineToken>(t);
                Assert.AreEqual(ClearMode.All, clear.Mode);
            });
    }

    #endregion

    #region DECSLRM Tests

    [TestMethod]
    public void Tokenize_DECSLRM_WithParameters_ReturnsLeftRightMarginToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[91;178s");

        var token = TestSeq.Single(result);
        var lrmToken = TestSeq.IsType<LeftRightMarginToken>(token);
        Assert.AreEqual(91, lrmToken.Left);
        Assert.AreEqual(178, lrmToken.Right);
    }

    [TestMethod]
    public void Tokenize_SaveCursor_WithNoParameters_ReturnsSaveCursorToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[s");

        var token = TestSeq.Single(result);
        TestSeq.IsType<SaveCursorToken>(token);
    }

    #endregion

    #region Arrow Key With Modifiers Tests

    [TestMethod]
    [DataRow("\x1b[A", CursorMoveDirection.Up)]
    [DataRow("\x1b[B", CursorMoveDirection.Down)]
    [DataRow("\x1b[C", CursorMoveDirection.Forward)]
    [DataRow("\x1b[D", CursorMoveDirection.Back)]
    public void Tokenize_PlainArrowKey_ReturnsCursorMoveToken(string input, CursorMoveDirection expectedDirection)
    {
        var result = AnsiTokenizer.Tokenize(input);

        var token = TestSeq.Single(result);
        var moveToken = TestSeq.IsType<CursorMoveToken>(token);
        Assert.AreEqual(expectedDirection, moveToken.Direction);
        Assert.AreEqual(1, moveToken.Count);
    }

    [TestMethod]
    [DataRow("\x1b[1;2D", CursorMoveDirection.Back, 2)]   // Shift+Left
    [DataRow("\x1b[1;5D", CursorMoveDirection.Back, 5)]   // Ctrl+Left
    [DataRow("\x1b[1;2C", CursorMoveDirection.Forward, 2)] // Shift+Right
    [DataRow("\x1b[1;5C", CursorMoveDirection.Forward, 5)] // Ctrl+Right
    [DataRow("\x1b[1;3A", CursorMoveDirection.Up, 3)]     // Alt+Up
    [DataRow("\x1b[1;6B", CursorMoveDirection.Down, 6)]   // Shift+Ctrl+Down
    public void Tokenize_ArrowKeyWithModifiers_ReturnsArrowKeyToken(string input, CursorMoveDirection expectedDirection, int expectedModifiers)
    {
        var result = AnsiTokenizer.Tokenize(input);

        var token = TestSeq.Single(result);
        var arrowToken = TestSeq.IsType<ArrowKeyToken>(token);
        Assert.AreEqual(expectedDirection, arrowToken.Direction);
        Assert.AreEqual(expectedModifiers, arrowToken.Modifiers);
    }

    [TestMethod]
    [DataRow("\x1b[5A", CursorMoveDirection.Up, 5)]    // Move up 5 lines (no modifiers)
    [DataRow("\x1b[10C", CursorMoveDirection.Forward, 10)] // Move right 10 columns (no modifiers)
    public void Tokenize_CursorMoveWithCount_ReturnsCursorMoveToken(string input, CursorMoveDirection expectedDirection, int expectedCount)
    {
        var result = AnsiTokenizer.Tokenize(input);

        var token = TestSeq.Single(result);
        var moveToken = TestSeq.IsType<CursorMoveToken>(token);
        Assert.AreEqual(expectedDirection, moveToken.Direction);
        Assert.AreEqual(expectedCount, moveToken.Count);
    }

    #endregion
}
