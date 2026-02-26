using Hex1b.Tokens;

namespace Hex1b.Tests;

public class KgpTokenizerTests
{
    [Fact]
    public void Tokenize_KgpApcSequence_ProducesKgpToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=T,f=24,s=1,v=1;AAAA\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("a=T,f=24,s=1,v=1", kgpToken.ControlData);
        Assert.Equal("AAAA", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpWithNoPayload_ProducesKgpTokenWithEmptyPayload()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=d,d=a\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("a=d,d=a", kgpToken.ControlData);
        Assert.Equal("", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpWithSemicolonButNoPayload_ProducesKgpTokenWithEmptyPayload()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=d;\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("a=d", kgpToken.ControlData);
        Assert.Equal("", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpWithOnlyGAndSemicolon_ProducesKgpTokenWithEmptyControlData()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_G;AAAA\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("", kgpToken.ControlData);
        Assert.Equal("AAAA", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpWithOnlyG_ProducesKgpTokenWithEmptyControlDataAndPayload()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_G\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("", kgpToken.ControlData);
        Assert.Equal("", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_NonKgpApcSequence_ProducesUnrecognizedToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_SomeOtherApc\x1b\\");

        Assert.DoesNotContain(tokens, t => t is KgpToken);
        Assert.Contains(tokens, t => t is UnrecognizedSequenceToken);
    }

    [Fact]
    public void Tokenize_EmptyApcSequence_ProducesUnrecognizedToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_\x1b\\");

        Assert.DoesNotContain(tokens, t => t is KgpToken);
    }

    [Fact]
    public void Tokenize_KgpAmidstText_PreservesTextTokens()
    {
        var tokens = AnsiTokenizer.Tokenize("Hello\x1b_Gi=1;AAAA\x1b\\World");

        Assert.Equal(3, tokens.Count);
        Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("Hello", ((TextToken)tokens[0]).Text);
        Assert.IsType<KgpToken>(tokens[1]);
        Assert.IsType<TextToken>(tokens[2]);
        Assert.Equal("World", ((TextToken)tokens[2]).Text);
    }

    [Fact]
    public void Tokenize_KgpChunkedSequence_ProducesMultipleKgpTokens()
    {
        // First chunk (m=1 means more coming)
        var input = "\x1b_Ga=T,f=24,s=2,v=2,m=1;AAAA\x1b\\" +
                    "\x1b_Gm=1;BBBB\x1b\\" +
                    "\x1b_Gm=0;CCCC\x1b\\";

        var tokens = AnsiTokenizer.Tokenize(input);

        var kgpTokens = tokens.OfType<KgpToken>().ToList();
        Assert.Equal(3, kgpTokens.Count);

        Assert.Equal("a=T,f=24,s=2,v=2,m=1", kgpTokens[0].ControlData);
        Assert.Equal("AAAA", kgpTokens[0].Payload);

        Assert.Equal("m=1", kgpTokens[1].ControlData);
        Assert.Equal("BBBB", kgpTokens[1].Payload);

        Assert.Equal("m=0", kgpTokens[2].ControlData);
        Assert.Equal("CCCC", kgpTokens[2].Payload);
    }

    [Fact]
    public void Tokenize_KgpWithAllKeys_ParsesControlData()
    {
        var tokens = AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,t=d,s=10,v=20,i=1,p=2,m=0,q=1;AAAA\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("a=T,f=32,t=d,s=10,v=20,i=1,p=2,m=0,q=1", kgpToken.ControlData);
    }

    [Fact]
    public void Tokenize_KgpWith8BitApcStart_ProducesKgpToken()
    {
        // 0x9F is the 8-bit APC start, 0x9C is the 8-bit ST
        var tokens = AnsiTokenizer.Tokenize("\x9fGa=T;AAAA\x9c");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("a=T", kgpToken.ControlData);
        Assert.Equal("AAAA", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpMissingTerminator_DoesNotProduceToken()
    {
        // APC without ST should not produce a token (TryParseApcSequence returns false)
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=T;AAAA");

        Assert.DoesNotContain(tokens, t => t is KgpToken);
    }

    [Fact]
    public void Tokenize_KgpQueryAction_ProducesCorrectToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("i=31,s=1,v=1,a=q,t=d,f=24", kgpToken.ControlData);
        Assert.Equal("AAAA", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpDeleteAction_ProducesCorrectToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=d,d=i,i=10\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal("a=d,d=i,i=10", kgpToken.ControlData);
        Assert.Equal("", kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_KgpLargePayload_PreservesFullPayload()
    {
        var largePayload = new string('A', 4096);
        var tokens = AnsiTokenizer.Tokenize($"\x1b_Gf=24,s=1,v=1;{largePayload}\x1b\\");

        var kgpToken = Assert.Single(tokens.OfType<KgpToken>());
        Assert.Equal(4096, kgpToken.Payload.Length);
        Assert.Equal(largePayload, kgpToken.Payload);
    }

    [Fact]
    public void Tokenize_MultipleKgpSequences_ProducesMultipleTokens()
    {
        var input = "\x1b_Gi=1,a=T,f=24,s=1,v=1;AAAA\x1b\\" +
                    "\x1b_Ga=p,i=1\x1b\\";

        var tokens = AnsiTokenizer.Tokenize(input);
        var kgpTokens = tokens.OfType<KgpToken>().ToList();

        Assert.Equal(2, kgpTokens.Count);
        Assert.Contains("a=T", kgpTokens[0].ControlData);
        Assert.Contains("a=p", kgpTokens[1].ControlData);
    }
}
