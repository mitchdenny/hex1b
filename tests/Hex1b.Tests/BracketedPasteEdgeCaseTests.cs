using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for bracketed paste tokenization edge cases.
/// Verifies that CSI 200~ and CSI 201~ are correctly tokenized as SpecialKeyTokens,
/// and that content between paste markers tokenizes correctly even with unusual content.
/// Inspired by psmux's test_ssh_vt_paste.rs edge cases.
/// </summary>
public class BracketedPasteEdgeCaseTests
{
    [Fact]
    public void Tokenize_PasteStartMarker_ReturnsSpecialKeyToken200()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[200~");

        var token = Assert.Single(result);
        var sk = Assert.IsType<SpecialKeyToken>(token);
        Assert.Equal(200, sk.KeyCode);
    }

    [Fact]
    public void Tokenize_PasteEndMarker_ReturnsSpecialKeyToken201()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[201~");

        var token = Assert.Single(result);
        var sk = Assert.IsType<SpecialKeyToken>(token);
        Assert.Equal(201, sk.KeyCode);
    }

    [Fact]
    public void Tokenize_CompletePasteSequence_ProducesStartContentEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[200~hello world\x1b[201~");

        Assert.Equal(3, result.Count);
        Assert.IsType<SpecialKeyToken>(result[0]);
        Assert.Equal(200, ((SpecialKeyToken)result[0]).KeyCode);

        Assert.IsType<TextToken>(result[1]);
        Assert.Equal("hello world", ((TextToken)result[1]).Text);

        Assert.IsType<SpecialKeyToken>(result[2]);
        Assert.Equal(201, ((SpecialKeyToken)result[2]).KeyCode);
    }

    [Fact]
    public void Tokenize_PasteWithEscInsideContent_TokenizesEscSeparately()
    {
        // Paste containing ESC followed by a non-CSI character
        // The ESC should start a new token parse, not be swallowed
        var result = AnsiTokenizer.Tokenize("\x1b[200~before\x1b[31mred\x1b[201~");

        // Should see: SpecialKey(200), Text("before"), SGR(31), Text("red"), SpecialKey(201)
        Assert.True(result.Count >= 4, $"Expected at least 4 tokens, got {result.Count}");

        Assert.IsType<SpecialKeyToken>(result[0]);
        Assert.Equal(200, ((SpecialKeyToken)result[0]).KeyCode);

        Assert.IsType<SpecialKeyToken>(result[^1]);
        Assert.Equal(201, ((SpecialKeyToken)result[^1]).KeyCode);
    }

    [Fact]
    public void Tokenize_ConsecutivePasteSequences_ProducesSeparateMarkers()
    {
        var result = AnsiTokenizer.Tokenize(
            "\x1b[200~first\x1b[201~\x1b[200~second\x1b[201~");

        // Should have: Start, "first", End, Start, "second", End
        Assert.Equal(6, result.Count);

        Assert.Equal(200, ((SpecialKeyToken)result[0]).KeyCode);
        Assert.Equal("first", ((TextToken)result[1]).Text);
        Assert.Equal(201, ((SpecialKeyToken)result[2]).KeyCode);
        Assert.Equal(200, ((SpecialKeyToken)result[3]).KeyCode);
        Assert.Equal("second", ((TextToken)result[4]).Text);
        Assert.Equal(201, ((SpecialKeyToken)result[5]).KeyCode);
    }

    [Fact]
    public void Tokenize_EmptyPasteSequence_ProducesStartEnd()
    {
        var result = AnsiTokenizer.Tokenize("\x1b[200~\x1b[201~");

        Assert.Equal(2, result.Count);
        Assert.Equal(200, ((SpecialKeyToken)result[0]).KeyCode);
        Assert.Equal(201, ((SpecialKeyToken)result[1]).KeyCode);
    }
}
