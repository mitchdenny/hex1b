using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OSC 7 (Current Working Directory) tokenization.
/// OSC 7 format: ESC ] 7 ; file://hostname/path ST
/// Emitted by bash, zsh, fish, and PowerShell to announce the shell's CWD.
/// Inspired by psmux's test_vt100_screen.rs OSC 7 tests.
/// </summary>
[TestClass]
public class Osc7CwdTests
{
    [TestMethod]
    public void Tokenize_Osc7_WithBelTerminator_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]7;file:///home/user/project\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("7", oscToken.Command);
        Assert.AreEqual("file:///home/user/project", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_Osc7_WithStTerminator_ReturnsOscToken()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]7;file:///tmp/test\x1b\\");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("7", oscToken.Command);
        Assert.AreEqual("file:///tmp/test", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_Osc7_WithHostname_ReturnsFullUri()
    {
        var result = AnsiTokenizer.Tokenize("\x1b]7;file://myhost/home/user\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("7", oscToken.Command);
        Assert.AreEqual("file://myhost/home/user", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_Osc7_WithPercentEncoding_PreservesRawUri()
    {
        // Tokenizer should preserve the raw URI; decoding is the consumer's job
        var result = AnsiTokenizer.Tokenize("\x1b]7;file:///home/user/my%20project\x07");

        var token = TestSeq.Single(result);
        var oscToken = TestSeq.IsType<OscToken>(token);
        Assert.AreEqual("7", oscToken.Command);
        Assert.AreEqual("file:///home/user/my%20project", oscToken.Payload);
    }

    [TestMethod]
    public void Tokenize_Osc7_DoesNotInterfereWithOsc0()
    {
        // OSC 7 followed by OSC 0 — both should parse independently
        var result = AnsiTokenizer.Tokenize(
            "\x1b]7;file:///tmp\x07\x1b]0;My Title\x07");

        Assert.AreEqual(2, result.Count);

        var osc7 = TestSeq.IsType<OscToken>(result[0]);
        Assert.AreEqual("7", osc7.Command);
        Assert.AreEqual("file:///tmp", osc7.Payload);

        var osc0 = TestSeq.IsType<OscToken>(result[1]);
        Assert.AreEqual("0", osc0.Command);
        Assert.AreEqual("My Title", osc0.Payload);
    }
}
