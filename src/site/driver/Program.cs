using System.Diagnostics;
using System.Text.Json;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using HwtRecordingDriver;

if (args.Length != 2)
    throw new ArgumentException("Usage: SiteRecordingDriver <prepared-catalog.json> <output-directory>");

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var samples = JsonSerializer.Deserialize<Sample[]>(await File.ReadAllTextAsync(args[0]), options)
    ?? throw new InvalidDataException("Missing recording catalog");
var failures = new List<Exception>();
foreach (var sample in samples)
{
    try { await RecordAsync(sample, Path.GetFullPath(args[1]), options); }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Recording {sample.Id} failed: {error}");
        failures.Add(new InvalidOperationException(sample.Id, error));
    }
}
if (failures.Count > 0) throw new AggregateException("Sample recording failures", failures);

static async Task RecordAsync(Sample sample, string output, JsonSerializerOptions options)
{
    if (!File.Exists(sample.Dll) || sample.Columns is < 20 or > 200 || sample.Rows is < 10 or > 100)
        throw new InvalidDataException($"Invalid recording input for {sample.Id}");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var processStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
    using var captureStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
    await using var presentation = new Hwt1PresentationAdapter(sample.Columns, sample.Rows);
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPresentation(presentation)
        .WithPtyProcess(process =>
        {
            process.FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            process.Arguments = [sample.Dll];
            process.WorkingDirectory = Path.GetDirectoryName(sample.Dll)!;
        }).Build();
    var run = terminal.RunAsync(processStop.Token);
    Task? capture = null;
    var frames = new List<Frame>();
    var inspector = new HwtFrameInspector();
    var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var clock = new Stopwatch();
    try
    {
        var readiness = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(screen => sample.ReadyText is { Length: > 0 } marker
                    ? screen.ContainsText(marker)
                    : HasVisualContent(screen),
                TimeSpan.FromSeconds(12), $"initial content for {sample.Id}")
            .Build().ApplyAsync(terminal, deadline.Token);
        await readiness;
        capture = CaptureAsync(presentation, frames, clock, firstFrame, inspector, captureStop.Token);
        await Task.WhenAny(firstFrame.Task, capture).WaitAsync(deadline.Token);
        if (capture.IsCompleted) await capture;
        await firstFrame.Task.WaitAsync(deadline.Token);

        // Reading time is part of the recording, not a readiness assertion.
        await Task.Delay(750, deadline.Token);
        foreach (var action in sample.Actions ?? [])
        {
            var sequence = new Hex1bTerminalInputSequenceBuilder();
            if (action.Text is not null) sequence.Type(action.Text);
            if (action.Key is not null)
            {
                var name = action.Key switch
                {
                    "Space" => "Spacebar", "Up" => "UpArrow", "Down" => "DownArrow",
                    "Left" => "LeftArrow", "Right" => "RightArrow", _ => action.Key
                };
                if (!Enum.TryParse<Hex1bKey>(name, out var key))
                    throw new InvalidDataException($"Unknown sample key: {action.Key}");
                sequence.Key(key);
            }
            if (action.WaitFor is { Length: > 0 } text)
                sequence.WaitUntil(screen => screen.ContainsText(text), TimeSpan.FromSeconds(5), text);
            await sequence.Build().ApplyAsync(terminal, deadline.Token);
            await Task.Delay(750, deadline.Token);
            if (run.IsCompleted) throw new InvalidOperationException("Sample exited during its recording scenario");
            if (capture.IsCompleted) await capture;
        }
        if (sample.CapturePolicy == "until-exit")
        {
            var exitCode = await run.WaitAsync(deadline.Token);
            if (exitCode != 0) throw new InvalidOperationException($"Sample exited with code {exitCode}");
        }
        captureStop.Cancel();
        await ObserveAsync(capture, captureStop.Token);
        if (sample.CapturePolicy == "until-exit")
        {
            // After output has drained, serialize a final full state with no competing reader.
            await presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = "resync" }));
            var final = await presentation.ReadFrameAsync(deadline.Token);
            var revision = inspector.Apply(final);
            if (frames.Count >= 10_000) throw new InvalidDataException("Recording frame limit exceeded");
            frames.Add(new(clock.Elapsed.TotalMilliseconds, Convert.ToBase64String(final.Span)));
            await presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = "ack", revision }));
        }
        var recording = new { format = "hex1b-hwt-recording", version = 1,
            durationMs = Math.Max(clock.Elapsed.TotalMilliseconds, frames[^1].TimeMs), frames };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(recording, options);
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidDataException("Recording exceeds 64 MiB");
        var directory = Path.Combine(output, sample.Id);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "recording.hwt.json"), bytes, deadline.Token);
        Console.WriteLine($"Recorded {sample.Id}: {frames.Count} frames, {bytes.Length:N0} bytes");
    }
    finally
    {
        captureStop.Cancel();
        processStop.Cancel();
        if (capture is not null) await ObserveAsync(capture, captureStop.Token);
        await ObserveAsync(run, processStop.Token);
    }
}

static async Task CaptureAsync(Hwt1PresentationAdapter presentation, List<Frame> frames, Stopwatch clock,
    TaskCompletionSource firstFrame, HwtFrameInspector inspector, CancellationToken cancellationToken)
{
    long estimatedBytes = 0;
    while (true)
    {
        var bytes = await presentation.ReadFrameAsync(cancellationToken);
        var revision = inspector.Apply(bytes);
        if (frames.Count == 0)
        {
            clock.Start();
        }
        var data = Convert.ToBase64String(bytes.Span);
        estimatedBytes += data.Length + 128;
        if (estimatedBytes > 64 * 1024 * 1024 || frames.Count >= 10_000)
            throw new InvalidDataException("Recording size limit exceeded");
        frames.Add(new(frames.Count == 0 ? 0 : clock.Elapsed.TotalMilliseconds, data));
        await presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = "ack", revision }));
        firstFrame.TrySetResult();
    }
}

static bool HasVisualContent(Hex1bTerminalSnapshot screen)
{
    if (screen.GetScreenText().Any(character => !char.IsWhiteSpace(character)) ||
        screen.KgpPlacements.Count > 0 || screen.SixelPlacements.Any(placement => placement.HasVisiblePaintedCells))
        return true;
    for (var y = 0; y < screen.Height; y++)
        for (var x = 0; x < screen.Width; x++)
            if (screen.GetCell(x, y).Background is not null) return true;
    return false;
}

static async Task ObserveAsync(Task task, CancellationToken cancellationToken)
{
    try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
}

internal sealed record Sample(string Id, string Dll, int Columns, int Rows, string? ReadyText, SampleAction[]? Actions, string? CapturePolicy);
internal sealed record SampleAction(string? Key, string? Text, string? WaitFor);
internal sealed record Frame(double TimeMs, string Data);
