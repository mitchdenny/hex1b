using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ANSI token types.
/// </summary>
[TestClass]
public class AnsiTokenTests
{
    // ===================
    // TextToken Tests
    // ===================

    [TestMethod]
    public void TextToken_StoresText()
    {
        var token = new TextToken("Hello");
        Assert.AreEqual("Hello", token.Text);
    }

    [TestMethod]
    public void TextToken_SupportsGraphemeClusters()
    {
        // Emoji with skin tone modifier (multiple code points, single grapheme)
        var token = new TextToken("👋🏽");
        Assert.AreEqual("👋🏽", token.Text);
    }

    [TestMethod]
    public void TextToken_Equality()
    {
        var token1 = new TextToken("Hello");
        var token2 = new TextToken("Hello");
        var token3 = new TextToken("World");

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // ControlCharacterToken Tests
    // ===================

    [TestMethod]
    public void ControlCharacterToken_StoresCharacter()
    {
        var token = new ControlCharacterToken('\n');
        Assert.AreEqual('\n', token.Character);
    }

    [TestMethod]
    public void ControlCharacterToken_StaticInstances()
    {
        Assert.AreEqual('\r', ControlCharacterToken.CarriageReturn.Character);
        Assert.AreEqual('\n', ControlCharacterToken.LineFeed.Character);
        Assert.AreEqual('\t', ControlCharacterToken.Tab.Character);
    }

    [TestMethod]
    public void ControlCharacterToken_StaticInstancesAreEqual()
    {
        // Static instances should equal newly created ones
        Assert.AreEqual(ControlCharacterToken.LineFeed, new ControlCharacterToken('\n'));
        Assert.AreEqual(ControlCharacterToken.CarriageReturn, new ControlCharacterToken('\r'));
    }

    // ===================
    // CursorPositionToken Tests
    // ===================

    [TestMethod]
    public void CursorPositionToken_DefaultsToTopLeft()
    {
        var token = new CursorPositionToken();
        Assert.AreEqual(1, token.Row);
        Assert.AreEqual(1, token.Column);
    }

    [TestMethod]
    public void CursorPositionToken_StoresPosition()
    {
        var token = new CursorPositionToken(5, 10);
        Assert.AreEqual(5, token.Row);
        Assert.AreEqual(10, token.Column);
    }

    [TestMethod]
    public void CursorPositionToken_Equality()
    {
        var token1 = new CursorPositionToken(5, 10);
        var token2 = new CursorPositionToken(5, 10);
        var token3 = new CursorPositionToken(5, 11);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // SgrToken Tests
    // ===================

    [TestMethod]
    public void SgrToken_StoresParameters()
    {
        var token = new SgrToken("1;38;2;255;0;0");
        Assert.AreEqual("1;38;2;255;0;0", token.Parameters);
    }

    [TestMethod]
    public void SgrToken_ResetInstance()
    {
        Assert.AreEqual("", SgrToken.Reset.Parameters);
    }

    [TestMethod]
    public void SgrToken_ResetEqualsEmptyParameters()
    {
        Assert.AreEqual(SgrToken.Reset, new SgrToken(""));
    }

    [TestMethod]
    public void SgrToken_Equality()
    {
        var token1 = new SgrToken("1");
        var token2 = new SgrToken("1");
        var token3 = new SgrToken("0");

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // ClearScreenToken Tests
    // ===================

    [TestMethod]
    public void ClearScreenToken_DefaultsToToEnd()
    {
        var token = new ClearScreenToken();
        Assert.AreEqual(ClearMode.ToEnd, token.Mode);
    }

    [TestMethod]
    [DataRow(ClearMode.ToEnd)]
    [DataRow(ClearMode.ToStart)]
    [DataRow(ClearMode.All)]
    [DataRow(ClearMode.AllAndScrollback)]
    public void ClearScreenToken_StoresMode(ClearMode mode)
    {
        var token = new ClearScreenToken(mode);
        Assert.AreEqual(mode, token.Mode);
    }

    [TestMethod]
    public void ClearScreenToken_Equality()
    {
        var token1 = new ClearScreenToken(ClearMode.All);
        var token2 = new ClearScreenToken(ClearMode.All);
        var token3 = new ClearScreenToken(ClearMode.ToEnd);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // PrivateModeToken Tests
    // ===================

    [TestMethod]
    public void PrivateModeToken_StoresModeAndEnable()
    {
        var enableToken = new PrivateModeToken(1049, true);
        var disableToken = new PrivateModeToken(1049, false);

        Assert.AreEqual(1049, enableToken.Mode);
        Assert.IsTrue(enableToken.Enable);
        
        Assert.AreEqual(1049, disableToken.Mode);
        Assert.IsFalse(disableToken.Enable);
    }

    [TestMethod]
    public void PrivateModeToken_Equality()
    {
        var token1 = new PrivateModeToken(1049, true);
        var token2 = new PrivateModeToken(1049, true);
        var token3 = new PrivateModeToken(1049, false);
        var token4 = new PrivateModeToken(25, true);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);  // Different enable
        Assert.AreNotEqual(token1, token4);  // Different mode
    }

    // ===================
    // OscToken Tests
    // ===================

    [TestMethod]
    public void OscToken_StoresAllParts()
    {
        var token = new OscToken("8", "id=link1", "https://example.com");
        
        Assert.AreEqual("8", token.Command);
        Assert.AreEqual("id=link1", token.Parameters);
        Assert.AreEqual("https://example.com", token.Payload);
    }

    [TestMethod]
    public void OscToken_EmptyPartsAllowed()
    {
        var token = new OscToken("8", "", "https://example.com");
        
        Assert.AreEqual("8", token.Command);
        Assert.AreEqual("", token.Parameters);
        Assert.AreEqual("https://example.com", token.Payload);
    }

    [TestMethod]
    public void OscToken_Equality()
    {
        var token1 = new OscToken("8", "", "https://example.com");
        var token2 = new OscToken("8", "", "https://example.com");
        var token3 = new OscToken("8", "", "https://other.com");

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // DcsToken Tests
    // ===================

    [TestMethod]
    public void DcsToken_StoresPayload()
    {
        var sixelPayload = "\x1bPq#0;2;0;0;0~-\x1b\\";
        var token = new DcsToken(sixelPayload);
        
        Assert.AreEqual(sixelPayload, token.Payload);
    }

    [TestMethod]
    public void DcsToken_Equality()
    {
        var token1 = new DcsToken("\x1bPq~\x1b\\");
        var token2 = new DcsToken("\x1bPq~\x1b\\");
        var token3 = new DcsToken("\x1bPq-\x1b\\");

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // UnrecognizedSequenceToken Tests
    // ===================

    [TestMethod]
    public void UnrecognizedSequenceToken_StoresSequence()
    {
        var token = new UnrecognizedSequenceToken("\x1b[?999z");
        Assert.AreEqual("\x1b[?999z", token.Sequence);
    }

    [TestMethod]
    public void UnrecognizedSequenceToken_Equality()
    {
        var token1 = new UnrecognizedSequenceToken("\x1b[?1z");
        var token2 = new UnrecognizedSequenceToken("\x1b[?1z");
        var token3 = new UnrecognizedSequenceToken("\x1b[?2z");

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // ClearLineToken Tests
    // ===================

    [TestMethod]
    public void ClearLineToken_DefaultsToToEnd()
    {
        var token = new ClearLineToken();
        Assert.AreEqual(ClearMode.ToEnd, token.Mode);
    }

    [TestMethod]
    [DataRow(ClearMode.ToEnd)]
    [DataRow(ClearMode.ToStart)]
    [DataRow(ClearMode.All)]
    public void ClearLineToken_StoresMode(ClearMode mode)
    {
        var token = new ClearLineToken(mode);
        Assert.AreEqual(mode, token.Mode);
    }

    [TestMethod]
    public void ClearLineToken_Equality()
    {
        var token1 = new ClearLineToken(ClearMode.All);
        var token2 = new ClearLineToken(ClearMode.All);
        var token3 = new ClearLineToken(ClearMode.ToEnd);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // ScrollRegionToken Tests
    // ===================

    [TestMethod]
    public void ScrollRegionToken_DefaultsToReset()
    {
        var token = new ScrollRegionToken();
        Assert.AreEqual(1, token.Top);
        Assert.AreEqual(0, token.Bottom);
    }

    [TestMethod]
    public void ScrollRegionToken_StoresRegion()
    {
        var token = new ScrollRegionToken(5, 20);
        Assert.AreEqual(5, token.Top);
        Assert.AreEqual(20, token.Bottom);
    }

    [TestMethod]
    public void ScrollRegionToken_ResetInstance()
    {
        Assert.AreEqual(1, ScrollRegionToken.Reset.Top);
        Assert.AreEqual(0, ScrollRegionToken.Reset.Bottom);
    }

    [TestMethod]
    public void ScrollRegionToken_Equality()
    {
        var token1 = new ScrollRegionToken(5, 20);
        var token2 = new ScrollRegionToken(5, 20);
        var token3 = new ScrollRegionToken(5, 21);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // SaveCursorToken Tests
    // ===================

    [TestMethod]
    public void SaveCursorToken_DefaultsToDec()
    {
        var token = new SaveCursorToken();
        Assert.IsTrue(token.UseDec);
    }

    [TestMethod]
    public void SaveCursorToken_StaticInstances()
    {
        Assert.IsTrue(SaveCursorToken.Dec.UseDec);
        Assert.IsFalse(SaveCursorToken.Ansi.UseDec);
    }

    [TestMethod]
    public void SaveCursorToken_Equality()
    {
        var token1 = new SaveCursorToken(true);
        var token2 = new SaveCursorToken(true);
        var token3 = new SaveCursorToken(false);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // RestoreCursorToken Tests
    // ===================

    [TestMethod]
    public void RestoreCursorToken_DefaultsToDec()
    {
        var token = new RestoreCursorToken();
        Assert.IsTrue(token.UseDec);
    }

    [TestMethod]
    public void RestoreCursorToken_StaticInstances()
    {
        Assert.IsTrue(RestoreCursorToken.Dec.UseDec);
        Assert.IsFalse(RestoreCursorToken.Ansi.UseDec);
    }

    [TestMethod]
    public void RestoreCursorToken_Equality()
    {
        var token1 = new RestoreCursorToken(true);
        var token2 = new RestoreCursorToken(true);
        var token3 = new RestoreCursorToken(false);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // CursorShapeToken Tests
    // ===================

    [TestMethod]
    public void CursorShapeToken_StoresShape()
    {
        var token = new CursorShapeToken(2);
        Assert.AreEqual(2, token.Shape);
    }

    [TestMethod]
    public void CursorShapeToken_StaticInstances()
    {
        Assert.AreEqual(0, CursorShapeToken.Default.Shape);
        Assert.AreEqual(1, CursorShapeToken.BlinkingBlock.Shape);
        Assert.AreEqual(2, CursorShapeToken.SteadyBlock.Shape);
        Assert.AreEqual(3, CursorShapeToken.BlinkingUnderline.Shape);
        Assert.AreEqual(4, CursorShapeToken.SteadyUnderline.Shape);
        Assert.AreEqual(5, CursorShapeToken.BlinkingBar.Shape);
        Assert.AreEqual(6, CursorShapeToken.SteadyBar.Shape);
    }

    [TestMethod]
    public void CursorShapeToken_Equality()
    {
        var token1 = new CursorShapeToken(2);
        var token2 = new CursorShapeToken(2);
        var token3 = new CursorShapeToken(4);

        Assert.AreEqual(token1, token2);
        Assert.AreNotEqual(token1, token3);
    }

    // ===================
    // Polymorphism Tests
    // ===================

    [TestMethod]
    public void AllTokens_AreAnsiToken()
    {
        AnsiToken[] tokens = 
        [
            new TextToken("hello"),
            new ControlCharacterToken('\n'),
            new CursorPositionToken(1, 1),
            new CursorShapeToken(2),
            new SgrToken("1"),
            new ClearScreenToken(ClearMode.All),
            new PrivateModeToken(1049, true),
            new OscToken("8", "", "url"),
            new DcsToken("payload"),
            new UnrecognizedSequenceToken("seq"),
            new ClearLineToken(ClearMode.ToEnd),
            new ScrollRegionToken(5, 20),
            new SaveCursorToken(true),
            new RestoreCursorToken(true)
        ];

        Assert.AreEqual(14, tokens.Length);
        TestSeq.All(tokens, t => TestSeq.IsType<AnsiToken>(t));
    }

    [TestMethod]
    public void AllTokens_SupportPatternMatching()
    {
        AnsiToken token = new CursorPositionToken(5, 10);

        var result = token switch
        {
            TextToken t => $"text:{t.Text}",
            ControlCharacterToken c => $"ctrl:{c.Character}",
            CursorPositionToken cp => $"cursor:{cp.Row},{cp.Column}",
            CursorShapeToken csh => $"shape:{csh.Shape}",
            SgrToken sgr => $"sgr:{sgr.Parameters}",
            ClearScreenToken cls => $"clearscreen:{cls.Mode}",
            ClearLineToken cll => $"clearline:{cll.Mode}",
            ScrollRegionToken sr => $"scroll:{sr.Top},{sr.Bottom}",
            SaveCursorToken sc => $"save:{sc.UseDec}",
            RestoreCursorToken rc => $"restore:{rc.UseDec}",
            PrivateModeToken pm => $"mode:{pm.Mode}:{pm.Enable}",
            OscToken osc => $"osc:{osc.Command}",
            DcsToken dcs => $"dcs:{dcs.Payload.Length}",
            UnrecognizedSequenceToken u => $"unknown:{u.Sequence}",
            _ => "other"
        };

        Assert.AreEqual("cursor:5,10", result);
    }

    // ===================
    // ClearMode Enum Tests
    // ===================

    [TestMethod]
    public void ClearMode_HasCorrectValues()
    {
        // These values must match ANSI spec for serialization
        Assert.AreEqual(0, (int)ClearMode.ToEnd);
        Assert.AreEqual(1, (int)ClearMode.ToStart);
        Assert.AreEqual(2, (int)ClearMode.All);
        Assert.AreEqual(3, (int)ClearMode.AllAndScrollback);
    }
}
