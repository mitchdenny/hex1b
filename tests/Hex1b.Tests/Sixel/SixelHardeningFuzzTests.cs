using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelHardeningFuzzTests
{
    private static readonly string[] RegressionFixtureNames =
    [
        "regression-numeric-overflow",
        "regression-command-replacement",
        "regression-extent-overflow",
        "regression-palette-raster-repeat",
    ];

    [TestMethod]
    [DynamicData(nameof(StableSeeds))]
    public void ParserCorpus_ArbitraryChunkingMatchesSingleChunk(int seed)
    {
        var random = new Random(seed);
        foreach (var fixtureName in RegressionFixtureNames)
        {
            var fixture = SixelFixture.Load(fixtureName, "Minimized Sixel hardening regression.");
            AssertChunkingEquivalent(fixture.StandardBytes, random, fixtureName);
            AssertChunkingEquivalent(fixture.C1Bytes, random, fixtureName + "-c1");
        }

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var bytes = CreateMutation(random);
            AssertChunkingEquivalent(bytes, random, $"seed {seed}, iteration {iteration}");
        }
    }

    [TestMethod]
    [DynamicData(nameof(StableSeeds))]
    public void LifecycleCorpus_MixedOperationsRemainBoundedAndSerializable(int seed)
    {
        var random = new Random(seed);
        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumPlacementsPerScreen = 16,
            MaximumHistoryPlacements = 16,
            MaximumImagesPerScreen = 8,
            MaximumRetainedLogicalPixelsPerScreen = 16 * 1024,
        };
        var graphics = CreateGraphicsOptions(policy);
        using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = 12,
            Height = 6,
            ScrollbackCapacity = 8,
            WorkloadAdapter = new NullWorkloadAdapter(),
            PresentationAdapter = new HeadlessPresentationAdapter(
                12,
                6,
                new TerminalCapabilities { SupportsSixel = true }),
            SixelPolicy = policy,
            Graphics = graphics,
        });

        for (var iteration = 0; iteration < 100; iteration++)
        {
            switch (random.Next(8))
            {
                case 0:
                    terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                        $"\x1b[{random.Next(1, 7)};{random.Next(1, 13)}H" +
                        $"\x1bPq#{random.Next(1, 8)};2;{random.Next(101)};{random.Next(101)};{random.Next(101)}" +
                        $"#{random.Next(1, 8)}!{random.Next(1, 65)}~\x1b\\"));
                    break;
                case 1:
                    terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                        $"\x1b[{random.Next(1, 7)};{random.Next(1, 13)}HX"));
                    break;
                case 2:
                    terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2K"));
                    break;
                case 3:
                    terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[S"));
                    break;
                case 4:
                    terminal.ApplyTokens(AnsiTokenizer.Tokenize(
                        random.Next(2) == 0 ? "\x1b[?1049h" : "\x1b[?1049l"));
                    break;
                case 5:
                    terminal.Resize(random.Next(4, 17), random.Next(2, 9));
                    break;
                case 6:
                    terminal.ApplyTokens([RisToken.Instance]);
                    break;
                default:
                    using (var snapshot = terminal.CreateSnapshot())
                    {
                        var recording = Hmp1SixelRecording.Serialize(snapshot.SixelPlacements);
                        var decoded = Hmp1SixelRecording.Deserialize(recording);
                        using var viewer = new Hex1bTerminal(new Hex1bTerminalOptions
                        {
                            Width = terminal.Width,
                            Height = terminal.Height,
                            WorkloadAdapter = new NullWorkloadAdapter(),
                            PresentationAdapter = new HeadlessPresentationAdapter(
                                terminal.Width,
                                terminal.Height,
                                new TerminalCapabilities { SupportsSixel = true }),
                            SixelPolicy = policy,
                            Graphics = graphics,
                        });
                        decoded.ReplayInto(viewer);
                        Assert.IsLessThanOrEqualTo(
                            graphics.MaximumPlacementsPerScreen,
                            viewer.SixelPlacementCount);
                        Assert.IsLessThanOrEqualTo(
                            graphics.MaximumImagesPerScreen,
                            viewer.TrackedSixelCount);
                        Assert.IsLessThanOrEqualTo(
                            graphics.MaximumRetainedBytesPerScreen,
                            viewer.SixelRetainedByteCount);
                        using var viewerSnapshot = viewer.CreateSnapshot();
                        Assert.IsLessThanOrEqualTo(
                            graphics.MaximumPlacementsPerScreen +
                                graphics.MaximumHistoryPlacements,
                            viewerSnapshot.SixelPlacements.Count);
                    }
                    break;
            }

            Assert.IsLessThanOrEqualTo(policy.MaximumPlacementsPerScreen, terminal.SixelPlacementCount);
            Assert.IsLessThanOrEqualTo(policy.MaximumImagesPerScreen, terminal.TrackedSixelCount);
            Assert.IsLessThanOrEqualTo(
                graphics.MaximumRetainedBytesPerScreen,
                terminal.SixelRetainedByteCount);
            using var current = terminal.CreateSnapshot();
            Assert.IsLessThanOrEqualTo(
                policy.MaximumPlacementsPerScreen + policy.MaximumHistoryPlacements,
                current.SixelPlacements.Count);
        }
    }

    private static Hex1bTerminalGraphicsOptions CreateGraphicsOptions(
        SixelCompatibilityPolicy policy) => new()
    {
        MaximumRetainedInputBytesPerImage = policy.MaximumRetainedDcsBytes,
        MaximumRasterPixelsPerImage = policy.MaximumRasterPixels,
        MaximumRasterOperationsPerImage = policy.MaximumRasterOperations,
        MaximumImagesPerScreen = policy.MaximumImagesPerScreen,
        MaximumPlacementsPerScreen = policy.MaximumPlacementsPerScreen,
        MaximumHistoryPlacements = policy.MaximumHistoryPlacements,
        MaximumRetainedLogicalPixelsPerScreen =
            policy.MaximumRetainedLogicalPixelsPerScreen,
    };

    public static TheoryData<int> StableSeeds()
    {
        var data = new TheoryData<int>();
        data.Add(445);
        data.Add(454);
        data.Add(473);
        data.Add(0x51E1);
        return data;
    }

    private static void AssertChunkingEquivalent(byte[] bytes, Random random, string message)
    {
        var baseline = Observe(bytes, chunked: false, random);
        for (var run = 0; run < 20; run++)
        {
            var chunked = Observe(bytes, chunked: true, random);
            TestSeq.AreEqual(baseline.Text, chunked.Text, message);
            Assert.AreEqual(baseline.Frames.Count, chunked.Frames.Count, message);
            for (var index = 0; index < baseline.Frames.Count; index++)
            {
                var expected = baseline.Frames[index];
                var actual = chunked.Frames[index];
                Assert.AreEqual(expected.Status, actual.Status, message);
                Assert.AreEqual(expected.ByteCount, actual.ByteCount, message);
                Assert.AreEqual(expected.RetentionLimitExceeded, actual.RetentionLimitExceeded, message);
                TestSeq.AreEqual(expected.ContentHash, actual.ContentHash, message);
                Assert.AreEqual(expected.SixelResult.Outcome, actual.SixelResult.Outcome, message);
                Assert.AreEqual(expected.SixelResult.LogicalCanvasExtent, actual.SixelResult.LogicalCanvasExtent, message);
                Assert.IsLessThanOrEqualTo(
                    SixelCompatibilityPolicy.Default.MaximumDiagnostics,
                    actual.SixelResult.Diagnostics.Count,
                    message);
            }
        }
    }

    private static Observation Observe(byte[] bytes, bool chunked, Random random)
    {
        var parser = new DcsByteStreamParser();
        var observation = new Observation();
        if (!chunked)
        {
            observation.Add(parser.Process(bytes));
        }
        else
        {
            var offset = 0;
            while (offset < bytes.Length)
            {
                var length = Math.Min(random.Next(1, 9), bytes.Length - offset);
                observation.Add(parser.Process(bytes.AsSpan(offset, length)));
                offset += length;
            }
        }

        observation.Add(parser.Complete());
        return observation;
    }

    private static byte[] CreateMutation(Random random)
    {
        var payload = new StringBuilder("prefix");
        payload.Append(random.Next(2) == 0 ? "\x1bP" : "\u0090");
        payload.Append(random.Next(2) == 0 ? "0;1q" : "q");
        var commandCount = random.Next(1, 40);
        for (var index = 0; index < commandCount; index++)
        {
            payload.Append(random.Next(7) switch
            {
                0 => $"!{random.NextInt64(0, 10_000_000_000)}~",
                1 => $"\"1;1;{random.NextInt64(0, 10_000_000_000)};{random.NextInt64(0, 10_000_000_000)}",
                2 => $"#{random.Next(0, 5000)};2;{random.Next(0, 200)};{random.Next(0, 200)};{random.Next(0, 200)}",
                3 => "#",
                4 => "$-",
                5 => ((char)random.Next(0, 32)).ToString(),
                _ => ((char)random.Next('?', '~' + 1)).ToString(),
            });
        }

        if (random.Next(4) == 0)
        {
            payload.Append(random.Next(2) == 0 ? '\x18' : '\x1a');
        }
        else if (random.Next(4) != 0)
        {
            payload.Append(random.Next(2) == 0 ? "\x1b\\" : "\u009c");
        }
        payload.Append("suffix");
        return Encoding.Latin1.GetBytes(payload.ToString());
    }

    private sealed class Observation
    {
        internal List<byte> Text { get; } = [];
        internal List<DcsFrame> Frames { get; } = [];

        internal void Add(DcsByteStreamBatch batch)
        {
            Text.AddRange(batch.TextBytes.Span);
            Frames.AddRange(batch.Frames.Select(item => item.Frame));
        }
    }

    private sealed class NullWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
