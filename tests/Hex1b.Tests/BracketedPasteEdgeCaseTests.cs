using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for bracketed paste tokenization edge cases.
/// Verifies that CSI 200~ and CSI 201~ are correctly tokenized as SpecialKeyTokens,
/// and that content between paste markers tokenizes correctly even with unusual content.
/// Inspired by psmux's test_ssh_vt_paste.rs edge cases.
/// </summary>
[TestClass]
public class BracketedPasteEdgeCaseTests
{
    [TestMethod]
    public void Tokenize_PasteStartMarker_ReturnsSpecialKeyToken200()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[200~");

        var token = TestSeq.Single(result);
        var sk = TestSeq.IsType<SpecialKeyToken>(token);
        Assert.AreEqual(200, sk.KeyCode);
    }

    [TestMethod]
    public void Tokenize_PasteEndMarker_ReturnsSpecialKeyToken201()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[201~");

        var token = TestSeq.Single(result);
        var sk = TestSeq.IsType<SpecialKeyToken>(token);
        Assert.AreEqual(201, sk.KeyCode);
    }

    [TestMethod]
    public void Tokenize_CompletePasteSequence_ProducesStartContentEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[200~hello world\x1b[201~");

        Assert.AreEqual(3, result.Count);
        TestSeq.IsType<SpecialKeyToken>(result[0]);
        Assert.AreEqual(200, ((SpecialKeyToken)result[0]).KeyCode);

        TestSeq.IsType<TextToken>(result[1]);
        Assert.AreEqual("hello world", ((TextToken)result[1]).Text);

        TestSeq.IsType<SpecialKeyToken>(result[2]);
        Assert.AreEqual(201, ((SpecialKeyToken)result[2]).KeyCode);
    }

    [TestMethod]
    public void Tokenize_PasteWithEscInsideContent_TokenizesEscSeparately()
    {
        // Paste containing ESC followed by a non-CSI character
        // The ESC should start a new token parse, not be swallowed
        var result = AnsiTokenizer.Tokenize("\x1b[200~before\x1b[31mred\x1b[201~");

        // Should see: SpecialKey(200), Text("before"), SGR(31), Text("red"), SpecialKey(201)
        Assert.IsTrue(result.Count >= 4, $"Expected at least 4 tokens, got {result.Count}");

        TestSeq.IsType<SpecialKeyToken>(result[0]);
        Assert.AreEqual(200, ((SpecialKeyToken)result[0]).KeyCode);

        TestSeq.IsType<SpecialKeyToken>(result[^1]);
        Assert.AreEqual(201, ((SpecialKeyToken)result[^1]).KeyCode);
    }

    [TestMethod]
    public void Tokenize_ConsecutivePasteSequences_ProducesSeparateMarkers()
    {
        var result = AnsiTokenizer.Tokenize(
            "\x1b[200~first\x1b[201~\x1b[200~second\x1b[201~");

        // Should have: Start, "first", End, Start, "second", End
        Assert.AreEqual(6, result.Count);

        Assert.AreEqual(200, ((SpecialKeyToken)result[0]).KeyCode);
        Assert.AreEqual("first", ((TextToken)result[1]).Text);
        Assert.AreEqual(201, ((SpecialKeyToken)result[2]).KeyCode);
        Assert.AreEqual(200, ((SpecialKeyToken)result[3]).KeyCode);
        Assert.AreEqual("second", ((TextToken)result[4]).Text);
        Assert.AreEqual(201, ((SpecialKeyToken)result[5]).KeyCode);
    }

    [TestMethod]
    public void Tokenize_EmptyPasteSequence_ProducesStartEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[200~\x1b[201~");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(200, ((SpecialKeyToken)result[0]).KeyCode);
        Assert.AreEqual(201, ((SpecialKeyToken)result[1]).KeyCode);
    }
}
