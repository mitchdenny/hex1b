using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ANSI token types.
/// </summary>
public class AnsiTokenTests
{
    // ===================
    // TextToken Tests
    // ===================

    [Fact]
    public void TextToken_StoresText()
    {
        var token = new TextToken("Hello");
        Assert.Equal("Hello", token.Text);
    }

    [Fact]
    public void TextToken_SupportsGraphemeClusters()
    {
        // Emoji with skin tone modifier (multiple code points, single grapheme)
        var token = new TextToken("👋🏽");
        Assert.Equal("👋🏽", token.Text);
    }

    [Fact]
    public void TextToken_Equality()
    {
        var token1 = new TextToken("Hello");
        var token2 = new TextToken("Hello");
        var token3 = new TextToken("World");

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // ControlCharacterToken Tests
    // ===================

    [Fact]
    public void ControlCharacterToken_StoresCharacter()
    {
        var token = new ControlCharacterToken('\n');
        Assert.Equal('\n', token.Character);
    }

    [Fact]
    public void ControlCharacterToken_StaticInstances()
    {
        Assert.Equal('\r', ControlCharacterToken.CarriageReturn.Character);
        Assert.Equal('\n', ControlCharacterToken.LineFeed.Character);
        Assert.Equal('\t', ControlCharacterToken.Tab.Character);
    }

    [Fact]
    public void ControlCharacterToken_StaticInstancesAreEqual()
    {
        // Static instances should equal newly created ones
        Assert.Equal(ControlCharacterToken.LineFeed, new ControlCharacterToken('\n'));
        Assert.Equal(ControlCharacterToken.CarriageReturn, new ControlCharacterToken('\r'));
    }

    // ===================
    // CursorPositionToken Tests
    // ===================

    [Fact]
    public void CursorPositionToken_DefaultsToTopLeft()
    {
        var token = new CursorPositionToken();
        Assert.Equal(1, token.Row);
        Assert.Equal(1, token.Column);
    }

    [Fact]
    public void CursorPositionToken_StoresPosition()
    {
        var token = new CursorPositionToken(5, 10);
        Assert.Equal(5, token.Row);
        Assert.Equal(10, token.Column);
    }

    [Fact]
    public void CursorPositionToken_Equality()
    {
        var token1 = new CursorPositionToken(5, 10);
        var token2 = new CursorPositionToken(5, 10);
        var token3 = new CursorPositionToken(5, 11);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // SgrToken Tests
    // ===================

    [Fact]
    public void SgrToken_StoresParameters()
    {
        var token = new SgrToken("1;38;2;255;0;0");
        Assert.Equal("1;38;2;255;0;0", token.Parameters);
    }

    [Fact]
    public void SgrToken_ResetInstance()
    {
        Assert.Equal("", SgrToken.Reset.Parameters);
    }

    [Fact]
    public void SgrToken_ResetEqualsEmptyParameters()
    {
        Assert.Equal(SgrToken.Reset, new SgrToken(""));
    }

    [Fact]
    public void SgrToken_Equality()
    {
        var token1 = new SgrToken("1");
        var token2 = new SgrToken("1");
        var token3 = new SgrToken("0");

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // ClearScreenToken Tests
    // ===================

    [Fact]
    public void ClearScreenToken_DefaultsToToEnd()
    {
        var token = new ClearScreenToken();
        Assert.Equal(ClearMode.ToEnd, token.Mode);
    }

    [Theory]
    [InlineData(ClearMode.ToEnd)]
    [InlineData(ClearMode.ToStart)]
    [InlineData(ClearMode.All)]
    [InlineData(ClearMode.AllAndScrollback)]
    public void ClearScreenToken_StoresMode(ClearMode mode)
    {
        var token = new ClearScreenToken(mode);
        Assert.Equal(mode, token.Mode);
    }

    [Fact]
    public void ClearScreenToken_Equality()
    {
        var token1 = new ClearScreenToken(ClearMode.All);
        var token2 = new ClearScreenToken(ClearMode.All);
        var token3 = new ClearScreenToken(ClearMode.ToEnd);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // PrivateModeToken Tests
    // ===================

    [Fact]
    public void PrivateModeToken_StoresModeAndEnable()
    {
        var enableToken = new PrivateModeToken(1049, true);
        var disableToken = new PrivateModeToken(1049, false);

        Assert.Equal(1049, enableToken.Mode);
        Assert.True(enableToken.Enable);
        
        Assert.Equal(1049, disableToken.Mode);
        Assert.False(disableToken.Enable);
    }

    [Fact]
    public void PrivateModeToken_Equality()
    {
        var token1 = new PrivateModeToken(1049, true);
        var token2 = new PrivateModeToken(1049, true);
        var token3 = new PrivateModeToken(1049, false);
        var token4 = new PrivateModeToken(25, true);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);  // Different enable
        Assert.NotEqual(token1, token4);  // Different mode
    }

    // ===================
    // OscToken Tests
    // ===================

    [Fact]
    public void OscToken_StoresAllParts()
    {
        var token = new OscToken("8", "id=link1", "https://example.com");
        
        Assert.Equal("8", token.Command);
        Assert.Equal("id=link1", token.Parameters);
        Assert.Equal("https://example.com", token.Payload);
    }

    [Fact]
    public void OscToken_EmptyPartsAllowed()
    {
        var token = new OscToken("8", "", "https://example.com");
        
        Assert.Equal("8", token.Command);
        Assert.Equal("", token.Parameters);
        Assert.Equal("https://example.com", token.Payload);
    }

    [Fact]
    public void OscToken_Equality()
    {
        var token1 = new OscToken("8", "", "https://example.com");
        var token2 = new OscToken("8", "", "https://example.com");
        var token3 = new OscToken("8", "", "https://other.com");

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // DcsToken Tests
    // ===================

    [Fact]
    public void DcsToken_StoresPayload()
    {
        var sixelPayload = "\x1bPq#0;2;0;0;0~-\x1b\\";
        var token = new DcsToken(sixelPayload);
        
        Assert.Equal(sixelPayload, token.Payload);
    }

    [Fact]
    public void DcsToken_Equality()
    {
        var token1 = new DcsToken("\x1bPq~\x1b\\");
        var token2 = new DcsToken("\x1bPq~\x1b\\");
        var token3 = new DcsToken("\x1bPq-\x1b\\");

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // UnrecognizedSequenceToken Tests
    // ===================

    [Fact]
    public void UnrecognizedSequenceToken_StoresSequence()
    {
        var token = new UnrecognizedSequenceToken("\x1b[?999z");
        Assert.Equal("\x1b[?999z", token.Sequence);
    }

    [Fact]
    public void UnrecognizedSequenceToken_Equality()
    {
        var token1 = new UnrecognizedSequenceToken("\x1b[?1z");
        var token2 = new UnrecognizedSequenceToken("\x1b[?1z");
        var token3 = new UnrecognizedSequenceToken("\x1b[?2z");

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // ClearLineToken Tests
    // ===================

    [Fact]
    public void ClearLineToken_DefaultsToToEnd()
    {
        var token = new ClearLineToken();
        Assert.Equal(ClearMode.ToEnd, token.Mode);
    }

    [Theory]
    [InlineData(ClearMode.ToEnd)]
    [InlineData(ClearMode.ToStart)]
    [InlineData(ClearMode.All)]
    public void ClearLineToken_StoresMode(ClearMode mode)
    {
        var token = new ClearLineToken(mode);
        Assert.Equal(mode, token.Mode);
    }

    [Fact]
    public void ClearLineToken_Equality()
    {
        var token1 = new ClearLineToken(ClearMode.All);
        var token2 = new ClearLineToken(ClearMode.All);
        var token3 = new ClearLineToken(ClearMode.ToEnd);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // ScrollRegionToken Tests
    // ===================

    [Fact]
    public void ScrollRegionToken_DefaultsToReset()
    {
        var token = new ScrollRegionToken();
        Assert.Equal(1, token.Top);
        Assert.Equal(0, token.Bottom);
    }

    [Fact]
    public void ScrollRegionToken_StoresRegion()
    {
        var token = new ScrollRegionToken(5, 20);
        Assert.Equal(5, token.Top);
        Assert.Equal(20, token.Bottom);
    }

    [Fact]
    public void ScrollRegionToken_ResetInstance()
    {
        Assert.Equal(1, ScrollRegionToken.Reset.Top);
        Assert.Equal(0, ScrollRegionToken.Reset.Bottom);
    }

    [Fact]
    public void ScrollRegionToken_Equality()
    {
        var token1 = new ScrollRegionToken(5, 20);
        var token2 = new ScrollRegionToken(5, 20);
        var token3 = new ScrollRegionToken(5, 21);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // SaveCursorToken Tests
    // ===================

    [Fact]
    public void SaveCursorToken_DefaultsToDec()
    {
        var token = new SaveCursorToken();
        Assert.True(token.UseDec);
    }

    [Fact]
    public void SaveCursorToken_StaticInstances()
    {
        Assert.True(SaveCursorToken.Dec.UseDec);
        Assert.False(SaveCursorToken.Ansi.UseDec);
    }

    [Fact]
    public void SaveCursorToken_Equality()
    {
        var token1 = new SaveCursorToken(true);
        var token2 = new SaveCursorToken(true);
        var token3 = new SaveCursorToken(false);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // RestoreCursorToken Tests
    // ===================

    [Fact]
    public void RestoreCursorToken_DefaultsToDec()
    {
        var token = new RestoreCursorToken();
        Assert.True(token.UseDec);
    }

    [Fact]
    public void RestoreCursorToken_StaticInstances()
    {
        Assert.True(RestoreCursorToken.Dec.UseDec);
        Assert.False(RestoreCursorToken.Ansi.UseDec);
    }

    [Fact]
    public void RestoreCursorToken_Equality()
    {
        var token1 = new RestoreCursorToken(true);
        var token2 = new RestoreCursorToken(true);
        var token3 = new RestoreCursorToken(false);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // CursorShapeToken Tests
    // ===================

    [Fact]
    public void CursorShapeToken_StoresShape()
    {
        var token = new CursorShapeToken(2);
        Assert.Equal(2, token.Shape);
    }

    [Fact]
    public void CursorShapeToken_StaticInstances()
    {
        Assert.Equal(0, CursorShapeToken.Default.Shape);
        Assert.Equal(1, CursorShapeToken.BlinkingBlock.Shape);
        Assert.Equal(2, CursorShapeToken.SteadyBlock.Shape);
        Assert.Equal(3, CursorShapeToken.BlinkingUnderline.Shape);
        Assert.Equal(4, CursorShapeToken.SteadyUnderline.Shape);
        Assert.Equal(5, CursorShapeToken.BlinkingBar.Shape);
        Assert.Equal(6, CursorShapeToken.SteadyBar.Shape);
    }

    [Fact]
    public void CursorShapeToken_Equality()
    {
        var token1 = new CursorShapeToken(2);
        var token2 = new CursorShapeToken(2);
        var token3 = new CursorShapeToken(4);

        Assert.Equal(token1, token2);
        Assert.NotEqual(token1, token3);
    }

    // ===================
    // Polymorphism Tests
    // ===================

    [Fact]
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

        Assert.Equal(14, tokens.Length);
        Assert.All(tokens, t => Assert.IsAssignableFrom<AnsiToken>(t));
    }

    [Fact]
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

        Assert.Equal("cursor:5,10", result);
    }

    // ===================
    // ClearMode Enum Tests
    // ===================

    [Fact]
    public void ClearMode_HasCorrectValues()
    {
        // These values must match ANSI spec for serialization
        Assert.Equal(0, (int)ClearMode.ToEnd);
        Assert.Equal(1, (int)ClearMode.ToStart);
        Assert.Equal(2, (int)ClearMode.All);
        Assert.Equal(3, (int)ClearMode.AllAndScrollback);
    }
}
