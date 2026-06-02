using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for mouse protocol mode tokenization (CSI ? 1000/1002/1003/1006 h/l).
/// Hex1b sends these to the host terminal; these tests verify correct tokenization
/// of the sequences both for output generation and for parsing terminal output
/// from child processes.
/// Inspired by psmux's test_vt100_mouse.rs.
/// </summary>
[TestClass]
public class MouseProtocolModeTests
{
    [TestMethod]
    public void Tokenize_MousePressRelease_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1000h");

        var token = TestSeq.Single(result);
        var pm = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1000, pm.Mode);
        Assert.IsTrue(pm.Enable);
    }

    [TestMethod]
    public void Tokenize_MousePressRelease_Disable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1000l");

        var token = TestSeq.Single(result);
        var pm = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1000, pm.Mode);
        Assert.IsFalse(pm.Enable);
    }

    [TestMethod]
    public void Tokenize_MouseButtonMotion_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1002h");

        var token = TestSeq.Single(result);
        var pm = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1002, pm.Mode);
        Assert.IsTrue(pm.Enable);
    }

    [TestMethod]
    public void Tokenize_MouseAnyMotion_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1003h");

        var token = TestSeq.Single(result);
        var pm = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1003, pm.Mode);
        Assert.IsTrue(pm.Enable);
    }

    [TestMethod]
    public void Tokenize_MouseSgrEncoding_Enable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1006h");

        var token = TestSeq.Single(result);
        var pm = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1006, pm.Mode);
        Assert.IsTrue(pm.Enable);
    }

    [TestMethod]
    public void Tokenize_MouseSgrEncoding_Disable()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[?1006l");

        var token = TestSeq.Single(result);
        var pm = TestSeq.IsType<PrivateModeToken>(token);
        Assert.AreEqual(1006, pm.Mode);
        Assert.IsFalse(pm.Enable);
    }

    [TestMethod]
    public void Tokenize_FullMouseEnableSequence_ReturnsAllFourTokens()
    {
        // This is the full sequence MouseParser.EnableMouseTracking sends
        var result = AnsiTokenizer.Tokenize(
            "\x1b[?1000h\x1b[?1002h\x1b[?1003h\x1b[?1006h");

        Assert.AreEqual(4, result.Count);

        var modes = result.Cast<PrivateModeToken>().Select(t => t.Mode).ToList();
        TestSeq.AreEqual([1000, 1002, 1003, 1006], modes);
        TestSeq.All(result.Cast<PrivateModeToken>(), t => Assert.IsTrue(t.Enable));
    }

    [TestMethod]
    public void Tokenize_FullMouseDisableSequence_ReturnsAllFourTokensReversed()
    {
        // This is the full sequence MouseParser.DisableMouseTracking sends
        var result = AnsiTokenizer.Tokenize(
            "\x1b[?1006l\x1b[?1003l\x1b[?1002l\x1b[?1000l");

        Assert.AreEqual(4, result.Count);

        var modes = result.Cast<PrivateModeToken>().Select(t => t.Mode).ToList();
        TestSeq.AreEqual([1006, 1003, 1002, 1000], modes);
        TestSeq.All(result.Cast<PrivateModeToken>(), t => Assert.IsFalse(t.Enable));
    }
}
