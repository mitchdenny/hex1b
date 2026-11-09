using Hex1b.Surfaces;

namespace Hex1b.Tests;

[TestClass]
public class GraphemeStringCacheTests
{
    [TestMethod]
    [DataRow("─")]
    [DataRow("中")]
    [DataRow("é")]
    [DataRow("\U0001F600")]
    [DataRow("\U0001F468‍\U0001F469‍\U0001F467")]
    public void Get_RepeatedCluster_ReturnsSameInstanceWithSameContent(string grapheme)
    {
        var first = GraphemeStringCache.Get(grapheme.AsSpan());
        var second = GraphemeStringCache.Get(grapheme.AsSpan());

        Assert.AreEqual(grapheme, first);
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void Get_PrintableAscii_ReturnsSharedSingleCharacterStrings()
    {
        for (var c = (char)0x20; c < 0x7F; c++)
        {
            var viaGet = GraphemeStringCache.Get(new[] { c });
            Assert.AreEqual(c.ToString(), viaGet);
            Assert.AreSame(viaGet, GraphemeStringCache.GetAscii(c));
        }
    }

    [TestMethod]
    public void Get_ClusterLongerThanCachedLimit_ReturnsEqualString()
    {
        var grapheme = "a" + new string('́', 40);

        var result = GraphemeStringCache.Get(grapheme.AsSpan());

        Assert.AreEqual(grapheme, result);
    }

    [TestMethod]
    public void Get_EmptySpan_ReturnsEmptyString()
    {
        Assert.AreEqual("", GraphemeStringCache.Get(ReadOnlySpan<char>.Empty));
    }

    [TestMethod]
    public void Get_ManyDifferentClusters_AlwaysReturnsTheRequestedContent()
    {
        // Far more distinct clusters than cache slots, so slots are replaced and collide.
        for (var round = 0; round < 3; round++)
        {
            for (var code = 0x4E00; code < 0x4E00 + 3000; code++)
            {
                var grapheme = ((char)code).ToString();
                Assert.AreEqual(grapheme, GraphemeStringCache.Get(grapheme.AsSpan()));
            }
        }
    }

    [TestMethod]
    public void Get_FromManyThreads_AlwaysReturnsTheRequestedContent()
    {
        var failures = 0;

        Parallel.For(0, 16, worker =>
        {
            for (var i = 0; i < 20000; i++)
            {
                var grapheme = ((char)(0x4E00 + (i * 7 + worker) % 1500)).ToString();
                if (GraphemeStringCache.Get(grapheme.AsSpan()) != grapheme)
                    Interlocked.Increment(ref failures);
            }
        });

        Assert.AreEqual(0, failures);
    }
}
