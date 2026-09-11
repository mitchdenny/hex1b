using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class DcsByteStreamParserTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Process_EverySplitBoundary_ProducesEquivalentStructuredFrame(bool useC1)
    {
        var standard = Encoding.ASCII.GetBytes("\x1bP1;2qABC\x1b\\");
        var c1 = new byte[] { 0x90, (byte)'1', (byte)';', (byte)'2', (byte)'q', (byte)'A', (byte)'B', (byte)'C', 0x9c };
        var bytes = useC1 ? c1 : standard;
        var baseline = Parse(bytes);

        for (var split = 0; split <= bytes.Length; split++)
        {
            var parser = new DcsByteStreamParser();
            var observation = new ParserObservation();
            if (split > 0)
            {
                observation.Add(parser.Process(bytes.AsSpan(0, split)));
            }
            if (split < bytes.Length)
            {
                observation.Add(parser.Process(bytes.AsSpan(split)));
            }
            observation.Add(parser.Complete());

            AssertObservationEqual(baseline, observation, $"split {split}");
        }
    }

    [TestMethod]
    public void Process_C1ValuesInsideUtf8CodePoints_RemainTextAtEverySplit()
    {
        var bytes = Encoding.UTF8.GetBytes("A┐\u0090B");

        for (var split = 0; split <= bytes.Length; split++)
        {
            var parser = new DcsByteStreamParser();
            var observation = new ParserObservation();
            if (split > 0)
            {
                observation.Add(parser.Process(bytes.AsSpan(0, split)));
            }
            if (split < bytes.Length)
            {
                observation.Add(parser.Process(bytes.AsSpan(split)));
            }
            observation.Add(parser.Complete());

            TestSeq.AreEqual(bytes, observation.Text, $"split {split}");
            Assert.IsEmpty(observation.Frames, $"split {split}");
        }
    }

    [TestMethod]
    public void Process_OneByteAtATime_PreservesConsecutiveFramesAndFollowingControls()
    {
        var bytes = Encoding.ASCII.GetBytes(
            "A\x1bPq@\x1b\\B\x1bP1+r544e\x1b\\\x1b[2CX");
        var parser = new DcsByteStreamParser();
        var observation = new ParserObservation();

        foreach (var value in bytes)
            observation.Add(parser.Process(new[] { value }));
        observation.Add(parser.Complete());

        Assert.AreEqual("AB\x1b[2CX", Encoding.ASCII.GetString(observation.Text.ToArray()));
        Assert.HasCount(2, observation.Frames);
        Assert.IsTrue(observation.Frames[0].Introducer.IsSixel);
        Assert.IsFalse(observation.Frames[1].Introducer.IsSixel);
        Assert.AreEqual((byte)'r', observation.Frames[1].Introducer.FinalByte);
    }

    [TestMethod]
    public void Process_C0ControlsInIntroducerAndPayload_PreserveFraming()
    {
        var bytes = new byte[]
        {
            0x1b, (byte)'P',
            (byte)'1', 0x00, (byte)';', 0x11, (byte)'2', (byte)'q',
            (byte)'A', 0x0a, (byte)'B',
            0x1b, (byte)'\\', (byte)'X',
        };

        var observation = Parse(bytes);

        Assert.AreEqual("X", Encoding.ASCII.GetString(observation.Text.ToArray()));
        var frame = TestSeq.Single(observation.Frames);
        Assert.AreEqual(DcsSequenceStatus.Complete, frame.Status);
        Assert.IsTrue(frame.Introducer.IsSixel);
        TestSeq.AreEqual(new int?[] { 1, 2 }, frame.Introducer.Parameters);
        TestSeq.AreEqual(
            new byte[] { (byte)'1', 0x00, (byte)';', 0x11, (byte)'2', (byte)'q', (byte)'A', 0x0a, (byte)'B' },
            frame.RetainedContent.ToArray());
    }

    [TestMethod]
    [DataRow("\x1bP", 0x18)]
    [DataRow("\x1bP1;", 0x1a)]
    [DataRow("\x1bPqABC", 0x18)]
    [DataRow("\x1bPqABC\x1b", 0x1a)]
    public void Process_CanOrSubAtEveryParserPhase_CancelsAndResumesText(
        string prefix,
        int cancellation)
    {
        var bytes = Encoding.ASCII.GetBytes(prefix)
            .Append((byte)cancellation)
            .Concat("X"u8.ToArray())
            .ToArray();

        var observation = Parse(bytes);

        Assert.AreEqual("X", Encoding.ASCII.GetString(observation.Text.ToArray()));
        Assert.AreEqual(DcsSequenceStatus.Cancelled, TestSeq.Single(observation.Frames).Status);
    }

    [TestMethod]
    public void Process_NestedLookingDcs_RemainsPayloadAndDoesNotConsumeFollowingText()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1bPqA\x1bPqB\x1b\\X");

        var observation = Parse(bytes);

        Assert.AreEqual("X", Encoding.ASCII.GetString(observation.Text.ToArray()));
        var frame = TestSeq.Single(observation.Frames);
        Assert.AreEqual(DcsSequenceStatus.Complete, frame.Status);
        Assert.AreEqual("qA\x1bPqB", Encoding.Latin1.GetString(frame.RetainedContent.Span));
    }

    [TestMethod]
    [DataRow("\x1bP100qA\x1b\\")]
    [DataRow("\x1bP1;2;3qA\x1b\\")]
    [DataRow("\x1bP1 2qA\x1b\\")]
    [DataRow("\x1bP1?qA\x1b\\")]
    [DataRow("\x1bP1;\x1b\\")]
    public void Process_MalformedIntroducer_RecoversAtTerminatorAndParsesNextFrame(
        string malformed)
    {
        var parser = new DcsByteStreamParser(
            retentionLimit: 128,
            maximumParameterCount: 2,
            maximumParameterValue: 99);
        var bytes = Encoding.ASCII.GetBytes($"{malformed}X\x1bPq@\x1b\\Y");
        var observation = new ParserObservation();

        observation.Add(parser.Process(bytes));
        observation.Add(parser.Complete());

        Assert.AreEqual("XY", Encoding.ASCII.GetString(observation.Text.ToArray()));
        Assert.HasCount(2, observation.Frames);
        Assert.AreEqual(DcsSequenceStatus.Malformed, observation.Frames[0].Status);
        Assert.AreEqual(DcsSequenceStatus.Complete, observation.Frames[1].Status);
        Assert.IsTrue(observation.Frames[1].Introducer.IsSixel);
    }

    [TestMethod]
    public void Process_PrivateMarkerIntermediateAndUnknownFinal_AreStructuredButNotSixel()
    {
        var privateFrame = TestSeq.Single(Parse(Encoding.ASCII.GetBytes("\x1bP?1qA\x1b\\")).Frames);
        var intermediateFrame = TestSeq.Single(Parse(Encoding.ASCII.GetBytes("\x1bP1 qA\x1b\\")).Frames);
        var unknownFrame = TestSeq.Single(Parse(Encoding.ASCII.GetBytes("\x1bP1pA\x1b\\")).Frames);

        Assert.AreEqual((byte)'?', privateFrame.Introducer.PrivateMarker);
        Assert.IsFalse(privateFrame.Introducer.IsSixel);
        TestSeq.AreEqual(new byte[] { (byte)' ' }, intermediateFrame.Introducer.Intermediates);
        Assert.IsFalse(intermediateFrame.Introducer.IsSixel);
        Assert.AreEqual((byte)'p', unknownFrame.Introducer.FinalByte);
        Assert.IsFalse(unknownFrame.Introducer.IsSixel);
    }

    [TestMethod]
    public void Process_RetentionLimitExceeded_BoundsAllocationAndStaysSynchronized()
    {
        const int payloadSize = 1_000_000;
        var bytes = new byte[payloadSize + 6];
        bytes[0] = 0x1b;
        bytes[1] = (byte)'P';
        bytes[2] = (byte)'q';
        bytes.AsSpan(3, payloadSize).Fill((byte)'~');
        bytes[^3] = 0x1b;
        bytes[^2] = (byte)'\\';
        bytes[^1] = (byte)'X';

        // Warm the parser code before measuring allocations.
        _ = new DcsByteStreamParser(32).Process("\x1bPqA\x1b\\"u8);
        var parser = new DcsByteStreamParser(retentionLimit: 32);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var batch = parser.Process(bytes);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsLessThan(128 * 1024, allocated);
        Assert.AreEqual("X", Encoding.ASCII.GetString(batch.TextBytes.Span));
        var frame = TestSeq.Single(batch.Frames).Frame;
        Assert.IsTrue(frame.RetentionLimitExceeded);
        Assert.AreEqual(32, frame.RetainedContent.Length);
        Assert.AreEqual(payloadSize + 1L, frame.ByteCount);
        Assert.IsFalse(parser.IsInDcs);
    }

    [TestMethod]
    public void Complete_OpenDcs_ReportsUnterminatedWithoutDispatchingSixel()
    {
        var parser = new DcsByteStreamParser();
        _ = parser.Process("\x1bPqABC"u8);

        var frame = TestSeq.Single(parser.Complete().Frames).Frame;

        Assert.AreEqual(DcsSequenceStatus.Unterminated, frame.Status);
        Assert.IsTrue(frame.Introducer.IsSixel);
        Assert.IsFalse(parser.IsInDcs);
    }

    [TestMethod]
    public void Process_DeterministicRandomChunkBoundaries_MatchSingleChunkOutcome()
    {
        var bytes = Encoding.Latin1.GetBytes(
            "A\x1bP1;2qABC\x1b\\B\u0090q~\u009cC\x1bP1+r544e\x1b\\D");
        var baseline = Parse(bytes);
        var random = new Random(446);

        for (var run = 0; run < 100; run++)
        {
            var parser = new DcsByteStreamParser();
            var observation = new ParserObservation();
            var offset = 0;
            while (offset < bytes.Length)
            {
                var length = Math.Min(random.Next(1, 8), bytes.Length - offset);
                observation.Add(parser.Process(bytes.AsSpan(offset, length)));
                offset += length;
            }
            observation.Add(parser.Complete());

            AssertObservationEqual(baseline, observation, $"run {run}");
        }
    }

    private static ParserObservation Parse(ReadOnlySpan<byte> bytes)
    {
        var parser = new DcsByteStreamParser();
        var observation = new ParserObservation();
        observation.Add(parser.Process(bytes));
        observation.Add(parser.Complete());
        return observation;
    }

    private static void AssertObservationEqual(
        ParserObservation expected,
        ParserObservation actual,
        string message)
    {
        TestSeq.AreEqual(expected.Text, actual.Text, message);
        Assert.AreEqual(expected.Frames.Count, actual.Frames.Count, message);
        for (var index = 0; index < expected.Frames.Count; index++)
        {
            var expectedFrame = expected.Frames[index];
            var actualFrame = actual.Frames[index];
            Assert.AreEqual(expectedFrame.Status, actualFrame.Status, message);
            Assert.AreEqual(expectedFrame.Introducer.PrivateMarker, actualFrame.Introducer.PrivateMarker, message);
            TestSeq.AreEqual(expectedFrame.Introducer.Parameters, actualFrame.Introducer.Parameters, message);
            TestSeq.AreEqual(expectedFrame.Introducer.Intermediates, actualFrame.Introducer.Intermediates, message);
            Assert.AreEqual(expectedFrame.Introducer.FinalByte, actualFrame.Introducer.FinalByte, message);
            Assert.AreEqual(expectedFrame.Introducer.IsValid, actualFrame.Introducer.IsValid, message);
            TestSeq.AreEqual(expectedFrame.RetainedContent.ToArray(), actualFrame.RetainedContent.ToArray(), message);
            Assert.AreEqual(expectedFrame.ByteCount, actualFrame.ByteCount, message);
            Assert.AreEqual(expectedFrame.RetentionLimitExceeded, actualFrame.RetentionLimitExceeded, message);
        }
    }

    private sealed class ParserObservation
    {
        public List<byte> Text { get; } = [];
        public List<DcsFrame> Frames { get; } = [];

        public void Add(DcsByteStreamBatch batch)
        {
            Text.AddRange(batch.TextBytes.Span);
            Frames.AddRange(batch.Frames.Select(boundary => boundary.Frame));
        }
    }
}
