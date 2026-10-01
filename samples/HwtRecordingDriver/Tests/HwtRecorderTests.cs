#pragma warning disable HEX1B_SIXEL

using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Surfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HwtRecordingDriver.Tests;

[TestClass]
public sealed class HwtRecorderTests
{
    [TestMethod]
    public async Task CaptureAsync_IndependentReader_PreservesTextGraphicsAndFullBaseline()
    {
        var count = 0;
        var rgba = new byte[120 * 80 * 4];
        var pixels = new SixelPixelBuffer(120, 80);
        for (var i = 0; i < 120 * 80; i++)
        {
            rgba[i * 4] = 200;
            rgba[i * 4 + 3] = 255;
            pixels[i % 120, i / 120] = Rgba32.FromRgb(10, 160, 200);
        }
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var captureStop = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        await using var presentation = new Hwt1PresentationAdapter(80, 24);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithHex1bApp(context => context.VStack(root =>
            [
                root.Text($"Count: {count}    Scene: Test"),
                root.Text("KGP / mountain light"),
                root.KgpImage(rgba, 120, 80, fallback => fallback.Text("KGP missing")).Width(12).Height(4),
                root.Text("Sixel / ocean currents"),
                root.Sixel(pixels, fallback => fallback.Text("Sixel missing")).Width(12).Height(4),
                root.Button("Next").OnClick(_ => count++)
            ]))
            .Build();
        var recorder = new HwtRecorder();
        Task? capture = null;
        var run = terminal.RunAsync(lifetime.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(HwtRecorder.IsInitialScreenReady, TimeSpan.FromSeconds(5))
                .Build().ApplyAsync(terminal, lifetime.Token);
            capture = recorder.CaptureAsync(presentation, captureStop.Token);
            for (var expected = 0; expected <= 2; expected++)
            {
                await recorder.StateAsync(expected).WaitAsync(TimeSpan.FromSeconds(5), lifetime.Token);
                if (expected < 2)
                    await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.Enter)
                        .Build().ApplyAsync(terminal, lifetime.Token);
            }
            captureStop.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => capture);
            var recording = recorder.Finish();
            Assert.AreEqual("hex1b-hwt-recording", recording.Format);
            Assert.AreEqual(0d, recording.Frames[0].TimeMs);
            Assert.IsTrue(recording.DurationMs >= recording.Frames[^1].TimeMs);
            Assert.IsTrue(recording.Frames.Count >= 3);
            Assert.AreEqual(2, recorder.Inspector.ImagePayloads);
            Assert.IsTrue(recorder.Inspector.ImageBytes >= 120 * 80 * 4 * 2L);

            var replay = new HwtFrameInspector();
            replay.Apply(Convert.FromBase64String(recording.Frames[0].Data));
            Assert.IsTrue(replay.Text.Contains("Count: 0", StringComparison.Ordinal));
            Assert.IsTrue(replay.HasBothGraphics, "Paused playback must open on the ready gallery.");
            foreach (var frame in recording.Frames.Skip(1))
                replay.Apply(Convert.FromBase64String(frame.Data));
            Assert.IsTrue(replay.Text.Contains("Count: 2", StringComparison.Ordinal));
            Assert.IsTrue(replay.HasBothGraphics);
            for (var i = 1; i < recording.Frames.Count; i++)
                Assert.IsTrue(recording.Frames[i].TimeMs >= recording.Frames[i - 1].TimeMs);

            await new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(Hex1bKey.C)
                .Build().ApplyAsync(terminal, lifetime.Token);
            Assert.AreEqual(0, await run.WaitAsync(TimeSpan.FromSeconds(5), lifetime.Token));
        }
        finally
        {
            captureStop.Cancel();
            lifetime.Cancel();
            if (capture is not null)
            {
                try { await capture; }
                catch (OperationCanceledException) when (captureStop.IsCancellationRequested) { }
            }
            try { await run; }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
    }

    [TestMethod]
    public async Task CaptureAsync_BlankFirstFrame_RejectsRecording()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithHex1bApp(context => context.Text("unused"))
            .Build();
        var recorder = new HwtRecorder();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            recorder.CaptureAsync(presentation, TestContext.Current.CancellationToken));
    }

    [TestMethod]
    [DataRow("magic")]
    [DataRow("truncated")]
    [DataRow("first-delta")]
    [DataRow("wrong-base")]
    [DataRow("incomplete-baseline")]
    public async Task Apply_MalformedBaseline_RejectsRecording(string fault)
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithHex1bApp(context => context.Text("unused"))
            .Build();
        var frame = (await presentation.ReadFrameAsync(TestContext.Current.CancellationToken)).ToArray();
        switch (fault)
        {
            case "magic":
                frame[0] = 0;
                break;
            case "truncated":
                frame = frame[..^1];
                break;
            case "first-delta":
                frame = ChangeMetadata(frame, metadata => metadata["full"] = false);
                break;
            case "wrong-base":
                frame = ChangeMetadata(frame, metadata => metadata["baseRevision"] = 99);
                break;
            case "incomplete-baseline":
                var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(4));
                BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(8 + metadataLength), 0);
                break;
        }

        Assert.ThrowsExactly<InvalidDataException>(() => new HwtFrameInspector().Apply(frame));
    }

    [TestMethod]
    public async Task Apply_DuplicateRevision_RejectsReorderedFrame()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(presentation)
            .WithHex1bApp(context => context.Text("unused"))
            .Build();
        var frame = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        var inspector = new HwtFrameInspector();
        inspector.Apply(frame);

        Assert.ThrowsExactly<InvalidDataException>(() => inspector.Apply(frame));
    }

    [TestMethod]
    public void Finish_MissingCapturedMilestones_RejectsRecording()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new HwtRecorder().Finish());
    }

    private static byte[] ChangeMetadata(byte[] frame, Action<JsonNode> change)
    {
        var oldLength = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(4));
        var metadata = JsonNode.Parse(frame.AsSpan(8, oldLength))!;
        change(metadata);
        var replacement = JsonSerializer.SerializeToUtf8Bytes(metadata);
        var result = new byte[frame.Length - oldLength + replacement.Length];
        frame.AsSpan(0, 4).CopyTo(result);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), replacement.Length);
        replacement.CopyTo(result.AsSpan(8));
        frame.AsSpan(8 + oldLength).CopyTo(result.AsSpan(8 + replacement.Length));
        return result;
    }
}
