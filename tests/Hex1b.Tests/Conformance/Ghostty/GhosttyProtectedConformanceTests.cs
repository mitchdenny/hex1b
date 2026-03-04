using Xunit;

namespace Hex1b.Tests.Conformance.Ghostty;

/// <summary>
/// Tests for DECSCA (Select Character Protection Attribute) conformance.
/// Translated from Ghostty's Terminal.zig protected attribute tests.
///
/// Protection modes:
/// - ISO: Normal erase (ED/EL/ECH) respects protection; selective erase also respects.
/// - DEC: Only selective erase (DECSED/DECSEL with CSI ?) respects protection; normal erase ignores.
/// - Off: No protection (but _protectedMode remembers last set mode for erase logic).
///
/// Key behavior: _protectedMode tracks the MOST RECENT mode set (ISO or DEC) and is
/// never reset to Off. The cursor's protected flag tracks whether new chars are protected.
/// </summary>
[Trait("Category", "GhosttyConformance")]
public class GhosttyProtectedConformanceTests
{
    // ═══════════════════════════════════════════════════════════════
    // setProtectedMode basic behavior
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SetProtectedMode_BasicToggle()
    {
        // Ghostty: "Terminal: setProtectedMode"
        var t = GhosttyTestFixture.CreateTerminal(3, 3);

        Assert.False(t.CursorProtected);
        t.SetProtectedMode(ProtectedMode.Off);
        Assert.False(t.CursorProtected);
        t.SetProtectedMode(ProtectedMode.Iso);
        Assert.True(t.CursorProtected);
        t.SetProtectedMode(ProtectedMode.Dec);
        Assert.True(t.CursorProtected);
        t.SetProtectedMode(ProtectedMode.Off);
        Assert.False(t.CursorProtected);
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseChars (ECH) — CSI X
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseChars_ProtectedRespectedWithIso()
    {
        // Ghostty: "Terminal: eraseChars protected attributes respected with iso"
        // ISO mode: normal ECH respects protection — characters NOT erased.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[2X");    // eraseChars(2)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_ProtectedIgnoredWithDecMostRecent()
    {
        // Ghostty: "Terminal: eraseChars protected attributes ignored with dec most recent"
        // ISO prints protected chars, then DEC mode set → dec becomes most recent.
        // Normal ECH ignores protection when last mode was DEC.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        t.SetProtectedMode(ProtectedMode.Dec);
        t.SetProtectedMode(ProtectedMode.Off);
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[2X");    // eraseChars(2)

        Assert.Equal("  C", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseChars_ProtectedIgnoredWithDecSet()
    {
        // Ghostty: "Terminal: eraseChars protected attributes ignored with dec set"
        // DEC mode: normal ECH ignores protection.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[2X");    // eraseChars(2)

        Assert.Equal("  C", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseLine right (EL 0 / DECSEL 0) — CSI K / CSI ? K
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseLine_Right_ProtectedRespectedWithIso()
    {
        // Ghostty: "Terminal: eraseLine right protected attributes respected with iso"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[K");     // eraseLine right (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Right_ProtectedIgnoredWithDecMostRecent()
    {
        // Ghostty: "Terminal: eraseLine right protected attributes ignored with dec most recent"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        t.SetProtectedMode(ProtectedMode.Dec);
        t.SetProtectedMode(ProtectedMode.Off);
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[K");     // eraseLine right (normal)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Right_ProtectedIgnoredWithDecSet()
    {
        // Ghostty: "Terminal: eraseLine right protected attributes ignored with dec set"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[K");     // eraseLine right (normal)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Right_ProtectedRequested()
    {
        // Ghostty: "Terminal: eraseLine right protected requested"
        // Selective erase (CSI ? K) always respects protection regardless of mode.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "12345678");
        GhosttyTestFixture.Feed(t, "\u001b[1;6H"); // setCursorPos(1,6) → cursor at col 5 (0-based)
        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[1;4H"); // setCursorPos(1,4) → cursor at col 3
        GhosttyTestFixture.Feed(t, "\u001b[?K");    // DECSEL right (selective)

        Assert.Equal("123  X", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseLine left (EL 1 / DECSEL 1) — CSI 1 K / CSI ? 1 K
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseLine_Left_ProtectedRespectedWithIso()
    {
        // Ghostty: "Terminal: eraseLine left protected attributes respected with iso"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[1K");    // eraseLine left (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Left_ProtectedIgnoredWithDecMostRecent()
    {
        // Ghostty: "Terminal: eraseLine left protected attributes ignored with dec most recent"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        t.SetProtectedMode(ProtectedMode.Dec);
        t.SetProtectedMode(ProtectedMode.Off);
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[1K");    // eraseLine left (normal)

        Assert.Equal("  C", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Left_ProtectedIgnoredWithDecSet()
    {
        // Ghostty: "Terminal: eraseLine left protected attributes ignored with dec set"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[1K");    // eraseLine left (normal)

        Assert.Equal("  C", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Left_ProtectedRequested()
    {
        // Ghostty: "Terminal: eraseLine left protected requested"
        // Selective erase (CSI ? 1 K) always respects protection.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[1;6H"); // setCursorPos(1,6) → cursor at col 5
        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[1;8H"); // setCursorPos(1,8) → cursor at col 7
        GhosttyTestFixture.Feed(t, "\u001b[?1K");   // DECSEL left (selective)

        Assert.Equal("     X  9", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseLine complete (EL 2 / DECSEL 2) — CSI 2 K / CSI ? 2 K
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseLine_Complete_ProtectedRespectedWithIso()
    {
        // Ghostty: "Terminal: eraseLine complete protected attributes respected with iso"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // setCursorPos(1,1)
        GhosttyTestFixture.Feed(t, "\u001b[2K");    // eraseLine complete (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Complete_ProtectedIgnoredWithDecMostRecent()
    {
        // Ghostty: "Terminal: eraseLine complete protected attributes ignored with dec most recent"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC");
        t.SetProtectedMode(ProtectedMode.Dec);
        t.SetProtectedMode(ProtectedMode.Off);
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[2K");    // eraseLine complete (normal)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Complete_ProtectedIgnoredWithDecSet()
    {
        // Ghostty: "Terminal: eraseLine complete protected attributes ignored with dec set"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC");
        GhosttyTestFixture.Feed(t, "\u001b[1;2H"); // setCursorPos(1,2)
        GhosttyTestFixture.Feed(t, "\u001b[2K");    // eraseLine complete (normal)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
    }

    [Fact]
    public void EraseLine_Complete_ProtectedRequested()
    {
        // Ghostty: "Terminal: eraseLine complete protected requested"
        // Selective erase (CSI ? 2 K) always respects protection.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[1;6H"); // setCursorPos(1,6) → cursor at col 5
        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[1;8H"); // setCursorPos(1,8) → cursor at col 7
        GhosttyTestFixture.Feed(t, "\u001b[?2K");   // DECSEL complete (selective)

        Assert.Equal("     X", GhosttyTestFixture.GetLine(t, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseDisplay below (ED 0 / DECSED 0) — CSI J / CSI ? J
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseDisplay_Below_ProtectedRespectedWithIso()
    {
        // Ghostty: "Terminal: eraseDisplay below protected attributes respected with iso"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[J");     // eraseDisplay below (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_Below_ProtectedIgnoredWithDecMostRecent()
    {
        // Ghostty: "Terminal: eraseDisplay below protected attributes ignored with dec most recent"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        t.SetProtectedMode(ProtectedMode.Dec);
        t.SetProtectedMode(ProtectedMode.Off);
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[J");     // eraseDisplay below (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_Below_ProtectedIgnoredWithDecSet()
    {
        // Ghostty: "Terminal: eraseDisplay below protected attributes ignored with dec set"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[J");     // eraseDisplay below (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("D", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_Below_ProtectedRespectedWithForce()
    {
        // Ghostty: "Terminal: eraseDisplay below protected attributes respected with force"
        // Selective erase (CSI ? J) always respects protection.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[?J");    // DECSED below (selective)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseDisplay above (ED 1 / DECSED 1) — CSI 1 J / CSI ? 1 J
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseDisplay_Above_ProtectedRespectedWithIso()
    {
        // Ghostty: "Terminal: eraseDisplay above protected attributes respected with iso"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[1J");    // eraseDisplay above (normal)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_Above_ProtectedIgnoredWithDecMostRecent()
    {
        // Ghostty: "Terminal: eraseDisplay above protected attributes ignored with dec most recent"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        t.SetProtectedMode(ProtectedMode.Dec);
        t.SetProtectedMode(ProtectedMode.Off);
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[1J");    // eraseDisplay above (normal)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("  F", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_Above_ProtectedIgnoredWithDecSet()
    {
        // Ghostty: "Terminal: eraseDisplay above protected attributes ignored with dec set"
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[1J");    // eraseDisplay above (normal)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("  F", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    [Fact]
    public void EraseDisplay_Above_ProtectedRespectedWithForce()
    {
        // Ghostty: "Terminal: eraseDisplay above protected attributes respected with force"
        // Selective erase (CSI ? 1 J) always respects protection.
        var t = GhosttyTestFixture.CreateTerminal(5, 5);

        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "ABC\r\nDEF\r\nGHI");
        GhosttyTestFixture.Feed(t, "\u001b[2;2H"); // setCursorPos(2,2)
        GhosttyTestFixture.Feed(t, "\u001b[?1J");   // DECSED above (selective)

        Assert.Equal("ABC", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("DEF", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("GHI", GhosttyTestFixture.GetLine(t, 2));
    }

    // ═══════════════════════════════════════════════════════════════
    // eraseDisplay with mixed protected/unprotected — selective erase
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EraseDisplay_ProtectedComplete()
    {
        // Ghostty: "Terminal: eraseDisplay protected complete"
        // Selective complete erase: only erases unprotected cells.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[2;6H"); // setCursorPos(2,6) → cursor at col 5
        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[2;4H"); // setCursorPos(2,4) → cursor at col 3

        GhosttyTestFixture.Feed(t, "\u001b[?2J");   // DECSED complete (selective)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("     X", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void EraseDisplay_ProtectedBelow()
    {
        // Ghostty: "Terminal: eraseDisplay protected below"
        // Selective below erase: preserves protected cells, erases rest from cursor.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[2;6H"); // setCursorPos(2,6)
        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[2;4H"); // setCursorPos(2,4)
        GhosttyTestFixture.Feed(t, "\u001b[?J");    // DECSED below (selective)

        Assert.Equal("A", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("123  X", GhosttyTestFixture.GetLine(t, 1));
    }

    [Fact]
    public void EraseDisplay_ProtectedAbove()
    {
        // Ghostty: "Terminal: eraseDisplay protected above"
        // Selective above erase: preserves protected cells, erases rest up to cursor.
        var t = GhosttyTestFixture.CreateTerminal(10, 3);

        GhosttyTestFixture.Feed(t, "A");
        GhosttyTestFixture.Feed(t, "\r\n");
        GhosttyTestFixture.Feed(t, "123456789");
        GhosttyTestFixture.Feed(t, "\u001b[2;6H"); // setCursorPos(2,6)
        t.SetProtectedMode(ProtectedMode.Dec);
        GhosttyTestFixture.Feed(t, "X");
        GhosttyTestFixture.Feed(t, "\u001b[2;8H"); // setCursorPos(2,8)
        GhosttyTestFixture.Feed(t, "\u001b[?1J");   // DECSED above (selective)

        Assert.Equal("", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("     X  9", GhosttyTestFixture.GetLine(t, 1));
    }

    // ═══════════════════════════════════════════════════════════════
    // saveCursor / restoreCursor with protected pen
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SaveCursor_ProtectedPen()
    {
        // Ghostty: "Terminal: saveCursor protected pen"
        // Save/restore cursor must preserve the protected flag.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        t.SetProtectedMode(ProtectedMode.Iso);
        Assert.True(t.CursorProtected);
        GhosttyTestFixture.Feed(t, "\u001b[1;10H"); // setCursorPos(1,10)
        GhosttyTestFixture.Feed(t, "\u001b7");       // saveCursor (DECSC)
        t.SetProtectedMode(ProtectedMode.Off);
        Assert.False(t.CursorProtected);
        GhosttyTestFixture.Feed(t, "\u001b8");       // restoreCursor (DECRC)
        Assert.True(t.CursorProtected);
    }

    // ═══════════════════════════════════════════════════════════════
    // DECALN resets graphemes with protected mode
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Decaln_ResetsGraphemesWithProtectedMode()
    {
        // Ghostty: "Terminal: DECALN resets graphemes with protected mode"
        // DECALN fills screen with 'E', cursor/protection state preserved.
        var t = GhosttyTestFixture.CreateTerminal(3, 3);

        t.SetProtectedMode(ProtectedMode.Iso);
        GhosttyTestFixture.Feed(t, "XYZ");
        GhosttyTestFixture.Feed(t, "\u001b#8"); // DECALN

        // DECALN preserves cursor protected and mode, but resets cursor position
        Assert.True(t.CursorProtected);

        Assert.Equal("EEE", GhosttyTestFixture.GetLine(t, 0));
        Assert.Equal("EEE", GhosttyTestFixture.GetLine(t, 1));
        Assert.Equal("EEE", GhosttyTestFixture.GetLine(t, 2));
    }

    // ═══════════════════════════════════════════════════════════════
    // DECSCA via escape sequence (CSI Ps " q)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Decsca_ViaEscapeSequence_SetsProtection()
    {
        // DECSCA 1 enables character protection, DECSCA 0 disables it.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        Assert.False(t.CursorProtected);
        GhosttyTestFixture.Feed(t, "\u001b[1\"q"); // DECSCA 1 (protected)
        Assert.True(t.CursorProtected);
        GhosttyTestFixture.Feed(t, "\u001b[0\"q"); // DECSCA 0 (unprotected)
        Assert.False(t.CursorProtected);
        GhosttyTestFixture.Feed(t, "\u001b[1\"q"); // DECSCA 1 (protected)
        Assert.True(t.CursorProtected);
        GhosttyTestFixture.Feed(t, "\u001b[2\"q"); // DECSCA 2 (also unprotected)
        Assert.False(t.CursorProtected);
    }

    [Fact]
    public void Decsca_ProtectedCharactersPreservedByIsoErase()
    {
        // Characters printed while DECSCA is active get protection attribute.
        // ISO mode + normal erase should preserve them.
        var t = GhosttyTestFixture.CreateTerminal(10, 5);

        // Use DECSCA to set protection (which sets DEC mode)
        GhosttyTestFixture.Feed(t, "\u001b[1\"q"); // DECSCA 1
        GhosttyTestFixture.Feed(t, "ABCDE");
        GhosttyTestFixture.Feed(t, "\u001b[0\"q"); // DECSCA 0
        // Now switch to ISO mode for the erase to respect protection
        t.SetProtectedMode(ProtectedMode.Iso);
        t.SetProtectedMode(ProtectedMode.Off); // Turn off protection on cursor
        GhosttyTestFixture.Feed(t, "\u001b[1;1H"); // Home
        GhosttyTestFixture.Feed(t, "\u001b[2K");    // Erase line complete

        Assert.Equal("ABCDE", GhosttyTestFixture.GetLine(t, 0));
    }
}
