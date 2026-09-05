using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class CloudWorkloadAdapterTests
{
    [TestMethod]
    [DataRow("resize", 40, 10, 400, 200)]
    [DataRow("cell-size", 80, 24, 1280, 768)]
    [DataRow("text-area", 80, 24, 1280, 768)]
    public async Task ReadOutputAsync_FieldChangesDuringRendering_WaitForFrameToFinish(
        string change, int columns, int rows, int pixelWidth, int pixelHeight)
    {
        using var releaseFrame = new ManualResetEventSlim();
        using var changeCompleted = new ManualResetEventSlim();
        var frameEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;

        await using var workload = new CloudWorkloadAdapter(
            100, 10, 20, TimeSpan.Zero, null, 42,
            (_, _) => new CallbackRenderer((cloud, frameColumns, frameRows) =>
            {
                if (++frames == 1)
                {
                    using var motes = cloud.Motes.GetEnumerator();
                    Assert.IsTrue(motes.MoveNext());
                    frameEntered.SetResult();
                    Assert.IsTrue(releaseFrame.Wait(TimeSpan.FromSeconds(10)), "Frame was not released.");

                    Assert.AreEqual(80, frameColumns);
                    Assert.AreEqual(24, frameRows);
                    Assert.AreEqual(800, cloud.PixelWidth);
                    Assert.AreEqual(480, cloud.PixelHeight);
                    var count = 1;
                    while (motes.MoveNext())
                        count++;
                    Assert.AreEqual(100, count);
                }
                else
                {
                    Assert.AreEqual(columns, frameColumns);
                    Assert.AreEqual(rows, frameRows);
                    Assert.AreEqual(pixelWidth, cloud.PixelWidth);
                    Assert.AreEqual(pixelHeight, cloud.PixelHeight);
                    Assert.HasCount(100, cloud.Motes);
                }
            }));

        await workload.ResizeAsync(80, 24);
        _ = await workload.ReadOutputAsync(); // Enter alternate screen before the first frame.
        var renderTask = Task.Run(async () => await workload.ReadOutputAsync());
        Task changeTask = Task.CompletedTask;
        try
        {
            await frameEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            changeTask = Task.Run(async () =>
            {
                changeStarted.SetResult();
                if (change == "resize")
                    await workload.ResizeAsync(columns, rows);
                else
                    await workload.WriteInputAsync(Encoding.ASCII.GetBytes(
                        change == "cell-size" ? "\x1b[6;32;16t" : "\x1b[4;768;1280t"));
                changeCompleted.Set();
            });

            await changeStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.IsFalse(changeCompleted.Wait(TimeSpan.FromMilliseconds(100)),
                "A field change must not complete while a renderer is reading the cloud.");
        }
        finally
        {
            releaseFrame.Set();
            await Task.WhenAll(renderTask, changeTask).WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.IsTrue(changeCompleted.IsSet);
        Assert.IsFalse((await workload.ReadOutputAsync()).IsEmpty);
        Assert.AreEqual(2, workload.FramesRendered);
    }

    [TestMethod]
    [DataRow("kgp")]
    [DataRow("sixel")]
    [DataRow("sixel-raster")]
    public async Task ReadOutputAsync_ConcurrentResizes_ContinuesRendering(string renderer)
    {
        using var stopResizing = new CancellationTokenSource();
        var firstResize = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var workload = new CloudWorkloadAdapter(
            700, 10, 20, TimeSpan.Zero, null, 42,
            (width, height) => renderer == "kgp"
                ? new KgpCloudRenderer(width, height)
                : new SixelCloudRenderer(width, height, useRaster: renderer == "sixel-raster"));
        await workload.ResizeAsync(80, 24);
        _ = await workload.ReadOutputAsync();

        var resizeTask = Task.Run(async () =>
        {
            var iteration = 0;
            while (!stopResizing.IsCancellationRequested)
            {
                await workload.ResizeAsync(iteration % 2 == 0 ? 40 : 80, iteration % 2 == 0 ? 12 : 24);
                firstResize.TrySetResult();
                iteration++;
                await Task.Yield();
            }
        });
        try
        {
            await firstResize.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            for (var frame = 0; frame < 30; frame++)
            {
                var output = Encoding.ASCII.GetString((await workload.ReadOutputAsync()).Span);
                Assert.StartsWith("\x1b[?2026h", output);
                Assert.EndsWith("\x1b[?2026l", output);
                Assert.Contains(renderer == "kgp" ? "\x1b_Ga=p," : "\x1bP", output);
            }
        }
        finally
        {
            stopResizing.Cancel();
            await resizeTask.WaitAsync(TimeSpan.FromSeconds(10));
        }

        await workload.ResizeAsync(80, 24);
        Assert.IsFalse((await workload.ReadOutputAsync()).IsEmpty);
        Assert.AreEqual(31, workload.FramesRendered);
        await workload.WriteInputAsync("q"u8.ToArray());
        Assert.Contains("\x1b[?1049l", Encoding.ASCII.GetString((await workload.ReadOutputAsync()).Span));
        Assert.IsTrue((await workload.ReadOutputAsync()).IsEmpty);
    }

    private sealed class CallbackRenderer(Action<DustCloud, int, int> render) : ICloudRenderer
    {
        public byte[] RenderFrame(DustCloud cloud, int columns, int rows)
        {
            render(cloud, columns, rows);
            return "frame"u8.ToArray();
        }
    }
}
