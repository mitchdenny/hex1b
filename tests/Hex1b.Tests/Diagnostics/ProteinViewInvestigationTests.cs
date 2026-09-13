using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hex1b.Tests.Diagnostics;

[TestClass]
[DoNotParallelize]
public class ProteinViewInvestigationTests
{
    [TestMethod]
    [DataRow("plain", false, false)]
    [DataRow("braille", false, false)]
    [DataRow("fullhd", false, false)]
    [DataRow("toggle", false, false)]
    [DataRow("fullhd", true, false)]
    [DataRow("fullhd", false, true)]
    public async Task Capture_ExplicitApplicationRun_PreservesDuplexEvidence(string mode, bool normalizedEnvironment, bool direct)
    {
        var executable = Environment.GetEnvironmentVariable("HEX1B_PROTEINVIEW_EXECUTABLE");
        var model = Environment.GetEnvironmentVariable("HEX1B_PROTEINVIEW_MODEL");
        var root = Environment.GetEnvironmentVariable("HEX1B_GRAPHICS_EVIDENCE");
        if (string.IsNullOrEmpty(executable) || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(root))
            Assert.Inconclusive("Opt-in: set HEX1B_PROTEINVIEW_EXECUTABLE, HEX1B_PROTEINVIEW_MODEL and HEX1B_GRAPHICS_EVIDENCE.");
        Assert.IsTrue(File.Exists(executable), "Explicit ProteinView executable must exist.");
        Assert.IsTrue(File.Exists(model), "Explicit local model must exist.");
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("This initial real-PTY investigation runner is Unix-only.");
            return;
        }

        var directory = Path.Combine(root, $"{mode}-{(normalizedEnvironment ? "normalized" : "observed")}-{(direct ? "direct" : "producer")}");
        Assert.IsFalse(Directory.Exists(directory), "Choose a new evidence directory; never overwrite a capture.");
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var environment = LaunchEnvironment(normalizedEnvironment);
        var logPath = Path.Combine(directory, "proteinview.log");
        var arguments = new[] { Path.GetFullPath(model), "--log", logPath }
            .Concat(mode == "plain" ? [] : mode == "fullhd" ? ["--fullhd"] : new[] { "--render", "braille" })
            .ToArray();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await using var child = new Hex1bTerminalChildProcess(Path.GetFullPath(executable), arguments,
            workingDirectory: directory, environment: environment, inheritEnvironment: false,
            initialWidth: 80, initialHeight: 24);
        await using var capture = new DuplexPtyCapture(child, 64 * 1024 * 1024, 100000);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, capture.FailureToken);
        await using var producer = new Hmp1PresentationAdapter(80, 24);
        await using var directPresentation = new Hwt1PresentationAdapter(80, 24);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(capture).WithPresentation(direct ? directPresentation : producer).WithDimensions(80, 24).Build();
        await using var presentation = direct
            ? directPresentation
            : await producer.CreateBrowserViewAsync("ProteinView investigation", stopped.Token);
        var observations = new List<object>();
        var completed = false;
        try
        {
            await child.StartAsync(stopped.Token);
            await ObserveAsync(mode == "toggle" ? "before-M" : mode);
            if (mode == "toggle")
            {
                await terminal.SendInputAsync("M"u8.ToArray(), stopped.Token);
                await ObserveAsync("after-M");
            }
            await terminal.SendInputAsync("q"u8.ToArray(), stopped.Token);
            var exitCode = await child.WaitForExitAsync(stopped.Token);
            Assert.AreEqual(0, exitCode, "ProteinView must exit normally; inspect log and transcript on failure.");
            // A read pending at exit must finish before the capture is declared complete.
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
            drain.CancelAfter(TimeSpan.FromSeconds(2));
            while (!capture.Records.Any(record => record.Kind == "output" && record.Data.Length == 0))
                await Task.Delay(10, drain.Token);
            capture.Complete("application-exited-and-output-drained");
            completed = true;
        }
        finally
        {
            try
            {
                if (child.HasStarted && !child.HasExited)
                {
                    child.Kill();
                    using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await child.WaitForExitAsync(exitTimeout.Token);
                }
            }
            finally
            {
                try { await terminal.DisposeAsync(); }
                finally
                {
                    await capture.SaveAsync(Path.Combine(directory, "transcript.json"), new
                    {
                        provenance = "real ProteinView process; verify requested baseline using executable hash, not the unknown reporter environment",
                        requestedBaselineRevision = "9b9a0790bc78f1d2a7c0923905049d27c67c05e2",
                        hexAssembly = typeof(Hex1bTerminal).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                        executableHash = Hash(executable),
                        modelName = Path.GetFileName(model), modelHash = Hash(model),
                        arguments, environment, normalizedEnvironment,
                        operatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                        topology = direct ? "direct HWT1" : "HMP1 producer-backed HWT1 (web demo default)",
                        columns = 80, rows = 24, cellPixelWidth = 10, cellPixelHeight = 20,
                        ptyPixelFields = "not independently queried; inspect CSI16 reply for effective protocol geometry",
                        deadlineSeconds = 20, maximumBytes = 64 * 1024 * 1024, maximumEvents = 100000,
                        completed, child.HasExited, exitCode = child.HasExited ? (int?)child.ExitCode : null,
                        observations
                    }, CancellationToken.None);
                }
            }
        }

        async Task ObserveAsync(string phase)
        {
            // This is an explicit sampling window, not a wait-for-render correctness assertion.
            await Task.Delay(TimeSpan.FromSeconds(4), stopped.Token);
            var frame = await presentation.ReadFrameAsync(stopped.Token);
            using var metadata = ProteinViewGraphicsStreamTests.Metadata(frame);
            using var snapshot = terminal.CreateSnapshot();
            await File.WriteAllBytesAsync(Path.Combine(directory, $"{phase}.hwt"), frame.ToArray(), stopped.Token);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{phase}.txt"),
                string.Join('\n', Enumerable.Range(0, snapshot.Height).Select(snapshot.GetLine)), stopped.Token);
            observations.Add(new
            {
                phase,
                images = snapshot.KgpImages.Count,
                placements = snapshot.KgpPlacements.Count,
                virtualPlacements = terminal.KgpVirtualPlacementCount,
                sixelPlacements = snapshot.SixelPlacements.Count,
                hwt = metadata.RootElement.Clone()
            });
            await presentation.HandleMessageAsync(Encoding.UTF8.GetBytes(
                $$"""{"type":"ack","revision":{{metadata.RootElement.GetProperty("revision").GetUInt32()}}}"""), stopped.Token);
        }
    }

    [TestMethod]
    public async Task Capture_ExplicitSustainedRun_BoundsCurrentOwnersAcrossRotation()
    {
        var executable = Environment.GetEnvironmentVariable("HEX1B_PROTEINVIEW_EXECUTABLE");
        var model = Environment.GetEnvironmentVariable("HEX1B_PROTEINVIEW_MODEL");
        var root = Environment.GetEnvironmentVariable("HEX1B_GRAPHICS_SUSTAINED_EVIDENCE");
        if (string.IsNullOrEmpty(executable) || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(root))
            Assert.Inconclusive("Opt-in: set HEX1B_PROTEINVIEW_EXECUTABLE, HEX1B_PROTEINVIEW_MODEL and HEX1B_GRAPHICS_SUSTAINED_EVIDENCE.");
        Assert.IsTrue(File.Exists(executable));
        Assert.IsTrue(File.Exists(model));
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("The real-PTY runner requires Unix.");
            return;
        }

        var acknowledgementIntervalMs = 0;
        if (Environment.GetEnvironmentVariable("HEX1B_GRAPHICS_ACK_INTERVAL_MS") is { } interval)
            Assert.IsTrue(int.TryParse(interval, out acknowledgementIntervalMs) &&
                acknowledgementIntervalMs is >= 0 and <= 1000, "ACK interval must be 0..1000 milliseconds.");
        Assert.IsFalse(Directory.Exists(root), "Use a new private evidence directory.");
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var environment = LaunchEnvironment(normalized: true);
        var arguments = new[] { Path.GetFullPath(model), "--fullhd", "--log", Path.Combine(root, "proteinview.log") };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        await using var child = new Hex1bTerminalChildProcess(Path.GetFullPath(executable), arguments,
            workingDirectory: root, environment: environment, inheritEnvironment: false,
            initialWidth: 80, initialHeight: 24);
        await using var capture = new DuplexPtyCapture(child, 64 * 1024 * 1024, 100000);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, capture.FailureToken);
        await using var producer = new Hmp1PresentationAdapter(80, 24);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(capture).WithPresentation(producer).WithDimensions(80, 24).Build();
        await using var presentation = await producer.CreateBrowserViewAsync("Sustained ProteinView", stopped.Token);
        var generations = new HashSet<string>(StringComparer.Ordinal);
        var observations = new List<object>();
        var frames = 0;
        var completed = false;
        var allocationStart = GC.GetTotalAllocatedBytes();
        long rotationAllocations = 0;
        KgpImageData? observedImage = null;
        long previousMaterializations = 0;
        long observedMaterializations = 0;
        long observedMaterializedBytes = 0;
        long rotationMaterializedBytes = 0;
        var observedVersions = 0;
        try
        {
            await child.StartAsync(stopped.Token);
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.KgpImages.Count == 1, TimeSpan.FromSeconds(10), "initial actual compressed image")
                .Build().ApplyAsync(terminal, stopped.Token);
            using var initial = terminal.CreateSnapshot();
            var first = TestSeq.Single(initial.KgpImages.Values);
            Assert.IsTrue(first.IsZlibCompressed);
            var initialHash = Convert.ToHexString(first.ContentHash);
            var initialPixels = first.Data;
            Assert.AreEqual(initialHash, Convert.ToHexString(SHA256.HashData(initialPixels)));
            await RecordFrameAsync("initial");
            await terminal.SendInputAsync(" "u8.ToArray(), stopped.Token);

            while (generations.Count < 200)
                await RecordFrameAsync(generations.Count == 100 ? "rotation" : null);

            ObserveMaterializations();
            rotationAllocations = GC.GetTotalAllocatedBytes() - allocationStart;
            rotationMaterializedBytes = observedMaterializedBytes;
            await terminal.SendInputAsync(" r"u8.ToArray(), stopped.Token);
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.KgpImages.Values.Any(image =>
                    Convert.ToHexString(image.ContentHash) == initialHash),
                    TimeSpan.FromSeconds(10), "paused camera reset to exact initial pixels")
                .Build().ApplyAsync(terminal, stopped.Token);
            using (var reset = terminal.CreateSnapshot())
                CollectionAssert.AreEqual(initialPixels, TestSeq.Single(reset.KgpImages.Values).Data);
            await RecordFrameAsync("reset");
            ObserveMaterializations();
            Assert.IsTrue(generations.Count >= 200);

            await terminal.SendInputAsync("q"u8.ToArray(), stopped.Token);
            Assert.AreEqual(0, await child.WaitForExitAsync(stopped.Token));
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
            drain.CancelAfter(TimeSpan.FromSeconds(2));
            while (!capture.Records.Any(record => record.Kind == "output" && record.Data.Length == 0))
                await Task.Delay(10, drain.Token);
            capture.Complete("sustained-application-exited-and-output-drained");
            completed = true;
        }
        finally
        {
            try
            {
                if (child.HasStarted && !child.HasExited)
                {
                    child.Kill();
                    using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await child.WaitForExitAsync(exitTimeout.Token);
                }
            }
            finally
            {
                try { await terminal.DisposeAsync(); }
                finally
                {
                    await capture.SaveAsync(Path.Combine(root, "transcript.json"), new
                    {
                        provenance = "real unmodified ProteinView; server-side producer/HWT1 ownership run, not browser presentation counts",
                        requestedBaselineRevision = "9b9a0790bc78f1d2a7c0923905049d27c67c05e2",
                        executableHash = Hash(executable), modelHash = Hash(model), arguments, environment,
                        deadlineSeconds = 60, maximumBytes = 64 * 1024 * 1024, maximumEvents = 100000,
                        observedDistinctAcceptedGenerations = generations.Count,
                        hwtFramesReadAndAcknowledged = frames,
                        acknowledgementIntervalMs,
                        completed,
                        rotationAllocationBytesIncludingCaptureAndProjection = rotationAllocations,
                        rotationMaterializedFormatBytesObserved = rotationMaterializedBytes,
                        observedImageVersions = observedVersions,
                        successfulDataReadsObserved = observedMaterializations,
                        materializedFormatBytesObserved = observedMaterializedBytes,
                        note = "Generations sampled from atomic terminal snapshots; source uploads can exceed observations. Current store gauges are sampled independently during rotation. The initial snapshot, one decoded array, and only the last observed image for counter deltas remain caller-owned; no per-generation image registry is retained.",
                        actualBytesAfterTerminalDisposal = terminal.KgpImageStore.TotalSize,
                        reservedBytesAfterTerminalDisposal = terminal.KgpImageStore.ReservedDecodedBytes,
                        observations
                    }, CancellationToken.None);
                }
            }
        }

        Assert.IsTrue(completed);
        Assert.AreEqual(0L, terminal.KgpImageStore.ChargedSize);

        async Task RecordFrameAsync(string? name)
        {
            var frame = await presentation.ReadFrameAsync(stopped.Token);
            using var metadata = ProteinViewGraphicsStreamTests.Metadata(frame);
            using var snapshot = terminal.CreateSnapshot();
            frames++;
            if (snapshot.KgpImages.Count > 0)
            {
                var image = TestSeq.Single(snapshot.KgpImages.Values);
                Assert.IsTrue(image.IsZlibCompressed);
                if (!ReferenceEquals(observedImage, image))
                {
                    ObserveMaterializations();
                    observedImage = image;
                    previousMaterializations = 0;
                    observedVersions++;
                }
                ObserveMaterializations();
                var hash = Convert.ToHexString(image.ContentHash);
                if (generations.Add(hash))
                {
                    observations.Add(new
                    {
                        generation = generations.Count, hash, image.Width, image.Height,
                        snapshotEncodedBytes = image.EncodedData.Length,
                        snapshotReservedBytes = image.ReservedDecodedBytes,
                        currentStoreActualBytes = terminal.KgpImageStore.TotalSize,
                        currentStoreReservedBytes = terminal.KgpImageStore.ReservedDecodedBytes,
                        currentStoreChargedBytes = terminal.KgpImageStore.ChargedSize,
                        frameBytes = frame.Length
                    });
                }
                Assert.IsTrue(terminal.KgpImageStore.ImageCount <= 1);
                Assert.IsTrue(terminal.KgpImageStore.ChargedSize <= 320L * 1024 * 1024);
            }
            if (name is not null && !File.Exists(Path.Combine(root, $"{name}.hwt")))
                await File.WriteAllBytesAsync(Path.Combine(root, $"{name}.hwt"), frame.ToArray(), stopped.Token);
            // Explicit presentation pacing for allocation comparisons, not a render-readiness delay.
            if (acknowledgementIntervalMs != 0)
                await Task.Delay(acknowledgementIntervalMs, stopped.Token);
            await presentation.HandleMessageAsync(Encoding.UTF8.GetBytes(
                $$"""{"type":"ack","revision":{{metadata.RootElement.GetProperty("revision").GetUInt32()}}}"""), stopped.Token);
        }

        void ObserveMaterializations()
        {
            if (observedImage is null)
                return;
            var count = observedImage.MaterializationCount;
            var delta = count - previousMaterializations;
            observedMaterializations = checked(observedMaterializations + delta);
            observedMaterializedBytes = checked(observedMaterializedBytes + delta * observedImage.ReservedDecodedBytes);
            previousMaterializations = count;
        }
    }

    private static Dictionary<string, string> LaunchEnvironment(bool normalized)
    {
        var environment = new Dictionary<string, string>
        {
            ["TERM"] = Environment.GetEnvironmentVariable("TERM") ?? "xterm-256color",
            ["COLORTERM"] = Environment.GetEnvironmentVariable("COLORTERM") ?? "truecolor",
            ["LANG"] = "en_US.UTF-8"
        };
        if (!normalized)
        {
            foreach (var key in new[] { "TERM_PROGRAM", "LC_TERMINAL" })
            {
                if (Environment.GetEnvironmentVariable(key) is { } value)
                    environment[key] = value;
            }
            foreach (var key in new[] { "SSH_CLIENT", "SSH_TTY", "SSH_CONNECTION", "KITTY_WINDOW_ID", "ITERM_SESSION_ID", "WEZTERM_EXECUTABLE" })
            {
                if (Environment.GetEnvironmentVariable(key) is not null)
                    environment[key] = "present";
            }
        }
        else
            environment["TERM"] = "xterm-256color";
        if (environment["TERM"].StartsWith("tmux", StringComparison.Ordinal) ||
            environment.GetValueOrDefault("TERM_PROGRAM") == "tmux")
            Assert.Inconclusive("Do not run this capture under tmux; upstream changes its passthrough setting.");
        return environment;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
