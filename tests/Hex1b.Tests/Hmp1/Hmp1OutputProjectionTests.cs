using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class Hmp1OutputProjectionTests
{
    [TestMethod]
    [DataRow("\x1b[c")]
    [DataRow("\x1b[0c")]
    [DataRow("\x1b[14t")]
    [DataRow("\x1b[16t")]
    [DataRow("\x1b[18t")]
    [DataRow("\x1b[5n")]
    [DataRow("\x1b[6n")]
    [DataRow("\x1b_Ga=q,i=1,f=32,s=1,v=1;AAAAAA==\x1b\\")]
    [DataRow("\x1b_Ga=q,i=1\x1b\\")]
    [DataRow("\u009fGa=q,i=1;AAAAAA==\u009c")]
    public void Process_QueryAtEveryByteBoundary_ConsumesOnlyQuery(string query)
    {
        var bytes = Encoding.UTF8.GetBytes("before" + query + "after");
        for (var split = 0; split <= bytes.Length; split++)
        {
            var projection = new Hmp1OutputProjection();
            var first = projection.Process(bytes.AsSpan(0, split));
            var second = projection.Process(bytes.AsSpan(split));
            Assert.AreEqual("beforeafter", Encoding.UTF8.GetString([.. first.Span, .. second.Span]), $"Split {split}");
        }
        Assert.AreEqual("beforeafter", ProjectOneByteAtATime(bytes));
    }

    [TestMethod]
    [DataRow("\x1b[>c\x1b[?62;4c\x1b[22t\x1b[1;2H\x1b[0m")]
    [DataRow("\x1b]0;inside\x1b[c\x07after")]
    [DataRow("\x1b]8;;https://example.test\x1b[18t\x1b\\")]
    [DataRow("\x1bPqinside\x1b[c\x1b\\")]
    [DataRow("\x1b_other;inside\x1b[c\x1b\\")]
    [DataRow("\u009d0;inside\x1b[c\u009c")]
    [DataRow("\u0090qinside\x1b[c\u009c")]
    [DataRow("\u009fother;inside\x1b[c\u009c")]
    [DataRow("\u00dc\u00df\u041c\U0001f31c")]
    [DataRow("\x1b[123\x18ordinary\x1b[456\x1aordinary")]
    [DataRow("\x1b_Ga=invalid,i=1;AAAAAA==\x1b\\")]
    public void Process_NonQueries_PreservesBytes(string text)
    {
        Assert.AreEqual(text, ProjectOneByteAtATime(Encoding.UTF8.GetBytes(text)));
    }

    [TestMethod]
    [DataRow("a=T,f=32,s=1,v=1,i=1", "a=T,f=32,s=1,v=1,i=1,q=2")]
    [DataRow("a=p,i=1,q=0", "a=p,i=1,q=2")]
    [DataRow("q=1,a=d,d=I,i=1", "a=d,d=I,i=1,q=2")]
    [DataRow("a=a,i=1,s=2,q=2", "a=a,i=1,s=2,q=2")]
    [DataRow("a=f,i=1,m=1", "a=f,i=1,m=1,q=2")]
    [DataRow("m=0", "m=0,q=2")]
    [DataRow("", "q=2")]
    public void Process_GraphicsAtEveryByteBoundary_QuietsHeaderWithoutChangingPayload(string controls, string expected)
    {
        var input = $"\x1b_G{controls};AAECAw==\x1b\\";
        var projected = $"\x1b_G{expected};AAECAw==\x1b\\";
        var bytes = Encoding.UTF8.GetBytes(input);
        for (var split = 0; split <= bytes.Length; split++)
        {
            var projection = new Hmp1OutputProjection();
            var first = projection.Process(bytes.AsSpan(0, split));
            var second = projection.Process(bytes.AsSpan(split));
            Assert.AreEqual(projected, Encoding.UTF8.GetString([.. first.Span, .. second.Span]), $"Split {split}");
        }
        Assert.AreEqual(projected, ProjectOneByteAtATime(bytes));
        Assert.AreEqual(projected, ProjectOneByteAtATime(Encoding.UTF8.GetBytes(projected)), "Projection is idempotent.");
    }

    [TestMethod]
    [DataRow("\x1b[c", "")]
    [DataRow("\x1b[16t", "")]
    [DataRow("\x1b_Ga=q,i=1;AAAAAA==\x1b\\", "")]
    [DataRow("\x1b[4;8H", "\x1b[4;8H")]
    [DataRow("\x1b]0;hello\x1b\\", "\x1b]0;hello\x1b\\")]
    [DataRow("\x1b_Ga=p,i=1\x1b\\", "\x1b_Ga=p,i=1,q=2\x1b\\")]
    [DataRow("\x1b_Ga=p,i=1;AAAAAA==\x1b\\", "\x1b_Ga=p,i=1,q=2;AAAAAA==\x1b\\")]
    public void ProjectContinuation_AttachAtEveryBoundary_SeedsOnlyEmittedPrefix(string sequence, string expected)
    {
        var bytes = Encoding.UTF8.GetBytes(sequence);
        for (var split = 1; split < bytes.Length; split++)
        {
            var projection = new Hmp1OutputProjection();
            _ = projection.Process(bytes.AsSpan(0, split));
            var seed = Hmp1OutputProjection.ProjectContinuation(bytes.AsSpan(0, split));
            var remainder = projection.Process(bytes.AsSpan(split));
            Assert.AreEqual(expected, Encoding.UTF8.GetString([.. seed.Span, .. remainder.Span]), $"Split {split}");
        }
    }

    [TestMethod]
    public void Process_RawDcsWithQueryLookingPayload_PreservesBytes()
    {
        byte[] bytes = [0x90, (byte)'q', 0x1b, (byte)'[', (byte)'c', 0x9c];
        var projection = new Hmp1OutputProjection();
        TestSeq.AreEqual(bytes, projection.Process(bytes).ToArray());
    }

    [TestMethod]
    [DataRow(0xc2)]
    [DataRow(0xc3)]
    [DataRow(0xe0)]
    [DataRow(0xf0)]
    public void Process_RawDcsTerminatorAfterUtf8Lead_StillConsumesFollowingQuery(int lead)
    {
        byte[] prefix = [0x1b, (byte)'P', (byte)'q', (byte)lead, 0x9c];
        byte[] bytes = [.. prefix, .. "\x1b[cDONE"u8];
        byte[] expected = [.. prefix, .. "DONE"u8];
        for (var split = 0; split <= bytes.Length; split++)
        {
            var projection = new Hmp1OutputProjection();
            var first = projection.Process(bytes.AsSpan(0, split));
            var second = projection.Process(bytes.AsSpan(split));
            TestSeq.AreEqual(expected, (byte[])[.. first.Span, .. second.Span]);
        }
    }

    [TestMethod]
    public void Reset_PendingQuery_DoesNotConsumeNewBaseline()
    {
        var projection = new Hmp1OutputProjection();
        Assert.IsTrue(projection.Process("\x1b[16"u8).IsEmpty);
        projection.Reset();
        Assert.AreEqual("text", Encoding.UTF8.GetString(projection.Process("text"u8).Span));
    }

    [TestMethod]
    public void Process_OversizedHeader_ThrowsInsteadOfSilentlyDroppingBytes()
    {
        var projection = new Hmp1OutputProjection();
        _ = projection.Process("\x1b["u8);
        var header = Encoding.ASCII.GetBytes(new string('0', Hmp1OutputProjection.MaximumHeaderBytes));
        Assert.ThrowsExactly<InvalidDataException>(() => projection.Process(header));
    }

    [TestMethod]
    public void Process_HeaderExactlyAtLimit_PreservesBytes()
    {
        var projection = new Hmp1OutputProjection();
        var bytes = Encoding.ASCII.GetBytes("\x1b[" +
            new string('0', Hmp1OutputProjection.MaximumHeaderBytes - 3) + "m");
        TestSeq.AreEqual(bytes, projection.Process(bytes).ToArray());
    }

    [TestMethod]
    public void Process_LargeGraphicsPayload_DoesNotBufferPayload()
    {
        var projection = new Hmp1OutputProjection();
        Assert.AreEqual("\x1b_Ga=t,i=1,q=2;", Encoding.UTF8.GetString(projection.Process("\x1b_Ga=t,i=1;"u8).Span));
        var chunk = Encoding.ASCII.GetBytes(new string('A', Hmp1OutputProjection.MaximumHeaderBytes * 2));
        TestSeq.AreEqual(chunk, projection.Process(chunk).ToArray());
        TestSeq.AreEqual(chunk, projection.Process(chunk).ToArray());
        Assert.AreEqual("\x1b\\", Encoding.UTF8.GetString(projection.Process("\x1b\\"u8).Span));
    }

    private static string ProjectOneByteAtATime(byte[] bytes)
    {
        var projection = new Hmp1OutputProjection();
        var result = new List<byte>();
        foreach (var value in bytes)
            result.AddRange(projection.Process([value]).ToArray());
        return Encoding.UTF8.GetString(result.ToArray());
    }
}
