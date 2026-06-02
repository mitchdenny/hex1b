using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KgpTokenizerTests
{
    [TestMethod]
    public void Tokenize_KgpApcSequence_ProducesKgpToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=T,f=24,s=1,v=1;AAAA\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("a=T,f=24,s=1,v=1", kgpToken.ControlData);
        Assert.AreEqual("AAAA", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpWithNoPayload_ProducesKgpTokenWithEmptyPayload()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=d,d=a\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("a=d,d=a", kgpToken.ControlData);
        Assert.AreEqual("", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpWithSemicolonButNoPayload_ProducesKgpTokenWithEmptyPayload()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=d;\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("a=d", kgpToken.ControlData);
        Assert.AreEqual("", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpWithOnlyGAndSemicolon_ProducesKgpTokenWithEmptyControlData()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_G;AAAA\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("", kgpToken.ControlData);
        Assert.AreEqual("AAAA", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpWithOnlyG_ProducesKgpTokenWithEmptyControlDataAndPayload()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_G\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("", kgpToken.ControlData);
        Assert.AreEqual("", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_NonKgpApcSequence_ProducesUnrecognizedToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_SomeOtherApc\x1b\\");

        Assert.IsFalse(tokens.Any(t => t is KgpToken));
        Assert.IsTrue(tokens.Any(t => t is UnrecognizedSequenceToken));
    }

    [TestMethod]
    public void Tokenize_EmptyApcSequence_ProducesUnrecognizedToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_\x1b\\");

        Assert.IsFalse(tokens.Any(t => t is KgpToken));
    }

    [TestMethod]
    public void Tokenize_KgpAmidstText_PreservesTextTokens()
    {
        var tokens = AnsiTokenizer.Tokenize("Hello\x1b_Gi=1;AAAA\x1b\\World");

        Assert.AreEqual(3, tokens.Count);
        TestSeq.IsType<TextToken>(tokens[0]);
        Assert.AreEqual("Hello", ((TextToken)tokens[0]).Text);
        TestSeq.IsType<KgpToken>(tokens[1]);
        TestSeq.IsType<TextToken>(tokens[2]);
        Assert.AreEqual("World", ((TextToken)tokens[2]).Text);
    }

    [TestMethod]
    public void Tokenize_KgpChunkedSequence_ProducesMultipleKgpTokens()
    {
        // First chunk (m=1 means more coming)
        var input = "\x1b_Ga=T,f=24,s=2,v=2,m=1;AAAA\x1b\\" +
                    "\x1b_Gm=1;BBBB\x1b\\" +
                    "\x1b_Gm=0;CCCC\x1b\\";

        var tokens = AnsiTokenizer.Tokenize(input);

        var kgpTokens = tokens.OfType<KgpToken>().ToList();
        Assert.AreEqual(3, kgpTokens.Count);

        Assert.AreEqual("a=T,f=24,s=2,v=2,m=1", kgpTokens[0].ControlData);
        Assert.AreEqual("AAAA", kgpTokens[0].Payload);

        Assert.AreEqual("m=1", kgpTokens[1].ControlData);
        Assert.AreEqual("BBBB", kgpTokens[1].Payload);

        Assert.AreEqual("m=0", kgpTokens[2].ControlData);
        Assert.AreEqual("CCCC", kgpTokens[2].Payload);
    }

    [TestMethod]
    public void Tokenize_KgpWithAllKeys_ParsesControlData()
    {
        var tokens = AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,t=d,s=10,v=20,i=1,p=2,m=0,q=1;AAAA\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("a=T,f=32,t=d,s=10,v=20,i=1,p=2,m=0,q=1", kgpToken.ControlData);
    }

    [TestMethod]
    public void Tokenize_KgpWith8BitApcStart_ProducesKgpToken()
    {
        // 0x9F is the 8-bit APC start, 0x9C is the 8-bit ST
        var tokens = AnsiTokenizer.Tokenize("\x9fGa=T;AAAA\x9c");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("a=T", kgpToken.ControlData);
        Assert.AreEqual("AAAA", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpMissingTerminator_DoesNotProduceToken()
    {
        // APC without ST should not produce a token (TryParseApcSequence returns false)
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=T;AAAA");

        Assert.IsFalse(tokens.Any(t => t is KgpToken));
    }

    [TestMethod]
    public void Tokenize_KgpQueryAction_ProducesCorrectToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("i=31,s=1,v=1,a=q,t=d,f=24", kgpToken.ControlData);
        Assert.AreEqual("AAAA", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpDeleteAction_ProducesCorrectToken()
    {
        var tokens = AnsiTokenizer.Tokenize("\x1b_Ga=d,d=i,i=10\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual("a=d,d=i,i=10", kgpToken.ControlData);
        Assert.AreEqual("", kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_KgpLargePayload_PreservesFullPayload()
    {
        var largePayload = new string('A', 4096);
        var tokens = AnsiTokenizer.Tokenize($"\x1b_Gf=24,s=1,v=1;{largePayload}\x1b\\");

        var kgpToken = TestSeq.Single(tokens.OfType<KgpToken>());
        Assert.AreEqual(4096, kgpToken.Payload.Length);
        Assert.AreEqual(largePayload, kgpToken.Payload);
    }

    [TestMethod]
    public void Tokenize_MultipleKgpSequences_ProducesMultipleTokens()
    {
        var input = "\x1b_Gi=1,a=T,f=24,s=1,v=1;AAAA\x1b\\" +
                    "\x1b_Ga=p,i=1\x1b\\";

        var tokens = AnsiTokenizer.Tokenize(input);
        var kgpTokens = tokens.OfType<KgpToken>().ToList();

        Assert.AreEqual(2, kgpTokens.Count);
        Assert.Contains("a=T", kgpTokens[0].ControlData);
        Assert.Contains("a=p", kgpTokens[1].ControlData);
    }
}
