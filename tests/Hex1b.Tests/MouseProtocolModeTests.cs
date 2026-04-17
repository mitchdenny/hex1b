using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for mouse protocol mode tokenization (CSI ? 1000/1002/1003/1006 h/l).
/// Hex1b sends these to the host terminal; these tests verify correct tokenization
/// of the sequences both for output generation and for parsing terminal output
/// from child processes.
/// Inspired by psmux's test_vt100_mouse.rs.
/// </summary>
public class MouseProtocolModeTests
{
    [Fact]
    public void Tokenize_MousePressRelease_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1000h");

        var token = Assert.Single(result);
        var pm = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1000, pm.Mode);
        Assert.True(pm.Enable);
    }

    [Fact]
    public void Tokenize_MousePressRelease_Disable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1000l");

        var token = Assert.Single(result);
        var pm = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1000, pm.Mode);
        Assert.False(pm.Enable);
    }

    [Fact]
    public void Tokenize_MouseButtonMotion_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1002h");

        var token = Assert.Single(result);
        var pm = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1002, pm.Mode);
        Assert.True(pm.Enable);
    }

    [Fact]
    public void Tokenize_MouseAnyMotion_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1003h");

        var token = Assert.Single(result);
        var pm = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1003, pm.Mode);
        Assert.True(pm.Enable);
    }

    [Fact]
    public void Tokenize_MouseSgrEncoding_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1006h");

        var token = Assert.Single(result);
        var pm = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1006, pm.Mode);
        Assert.True(pm.Enable);
    }

    [Fact]
    public void Tokenize_MouseSgrEncoding_Disable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1006l");

        var token = Assert.Single(result);
        var pm = Assert.IsType<PrivateModeToken>(token);
        Assert.Equal(1006, pm.Mode);
        Assert.False(pm.Enable);
    }

    [Fact]
    public void Tokenize_FullMouseEnableSequence_ReturnsAllFourTokens()
    {
        // This is the full sequence MouseParser.EnableMouseTracking sends
        var result = AnsiTokenizer.Tokenize(
            "\x1b[?1000h\x1b[?1002h\x1b[?1003h\x1b[?1006h");

        Assert.Equal(4, result.Count);

        var modes = result.Cast<PrivateModeToken>().Select(t => t.Mode).ToList();
        Assert.Equal([1000, 1002, 1003, 1006], modes);
        Assert.All(result.Cast<PrivateModeToken>(), t => Assert.True(t.Enable));
    }

    [Fact]
    public void Tokenize_FullMouseDisableSequence_ReturnsAllFourTokensReversed()
    {
        // This is the full sequence MouseParser.DisableMouseTracking sends
        var result = AnsiTokenizer.Tokenize(
            "\x1b[?1006l\x1b[?1003l\x1b[?1002l\x1b[?1000l");

        Assert.Equal(4, result.Count);

        var modes = result.Cast<PrivateModeToken>().Select(t => t.Mode).ToList();
        Assert.Equal([1006, 1003, 1002, 1000], modes);
        Assert.All(result.Cast<PrivateModeToken>(), t => Assert.False(t.Enable));
    }
}
