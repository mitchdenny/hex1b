using System.Text.Json;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using HwtRecordingDriver;

if (args.Length != 2 || args[0] != "--output" || string.IsNullOrWhiteSpace(args[1]))
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/HwtRecordingDriver -- --output <directory>");
    return 1;
}

try
{
    await RecordAsync(Path.GetFullPath(args[1]));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}

static async Task RecordAsync(string outputDirectory)
{
    var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "sample");
    var sampleDll = Path.Combine(sampleDirectory, "HwtRecordingSample.dll");
    if (!File.Exists(sampleDll))
        throw new FileNotFoundException("Build the driver first; its build also builds and copies the sample.", sampleDll);
    var sourceDirectory = Path.Combine(sampleDirectory, "source");
    string[] sourceFiles = ["HwtRecordingSample.csproj", "Program.cs", "GalleryState.cs", "GalleryArtwork.cs",
        "README.md", ".gitignore", "LICENSE"];
    var files = sourceFiles.Select(path => new
    {
        path,
        language = Path.GetExtension(path) switch { ".csproj" => "xml", ".cs" => "csharp", _ => "plaintext" },
        content = File.ReadAllText(Path.Combine(sourceDirectory, path))
    }).ToArray();

    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    using var processStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
    using var captureStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
    using var scenarioStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
    await using var presentation = new Hwt1PresentationAdapter(80, 24);
    var terminal = Hex1bTerminal.CreateBuilder()
        .WithPresentation(presentation)
        .WithPtyProcess(process =>
        {
            process.FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            process.Arguments = [sampleDll];
            process.WorkingDirectory = sampleDirectory;
        })
        .Build();

    var recorder = new HwtRecorder();
    Task? captureTask = null;
    Task<int>? runTask = null;
    Task? readinessTask = null;
    Task? scenarioTask = null;
    var failures = new List<Exception>();
    string? report = null;
    try
    {
        runTask = terminal.RunAsync(processStop.Token);
        readinessTask = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(HwtRecorder.IsInitialScreenReady, TimeSpan.FromSeconds(12),
                "the initial sample screen and both graphics to be ready")
            .Build()
            .ApplyAsync(terminal, scenarioStop.Token);
        await Task.WhenAny(readinessTask, runTask).WaitAsync(deadline.Token);
        if (runTask.IsCompleted)
            throw new InvalidOperationException($"The sample exited before readiness with code {await runTask}.");
        await readinessTask;

        // No frames have been read or discarded: the first projection is a ready full baseline.
        captureTask = recorder.CaptureAsync(presentation, captureStop.Token);
        await Task.WhenAny(recorder.Baseline.Task, captureTask).WaitAsync(deadline.Token);
        if (captureTask.IsCompleted)
            await captureTask;
        await recorder.Baseline.Task.WaitAsync(deadline.Token);

        scenarioTask = ExerciseAsync(terminal, recorder, scenarioStop.Token);
        await Task.WhenAny(scenarioTask, runTask, captureTask).WaitAsync(deadline.Token);
        if (captureTask.IsCompleted)
        {
            await captureTask;
            throw new InvalidOperationException("The HWT reader ended before the scenario completed.");
        }
        if (runTask.IsCompleted)
            throw new InvalidOperationException($"The sample exited early with code {await runTask}.");
        await scenarioTask;

        captureStop.Cancel();
        await ObserveAsync(captureTask, captureStop.Token, failures);
        if (failures.Count != 0)
            throw new InvalidOperationException("The HWT capture failed.");
        var recording = recorder.Finish();
        var recordingBytes = JsonSerializer.SerializeToUtf8Bytes(recording, jsonOptions);
        if (recordingBytes.Length > HwtRecorder.MaximumJsonBytes)
            throw new InvalidDataException("Serialized recording exceeds the 64 MiB JSON limit.");
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = "pixel-postcards",
            title = "Pixel postcards",
            description = "An ordinary multi-file .NET app: change the counter and swap KGP and Sixel artwork. " +
                "This offline recording and HWT1 are experimental, same-build-only state transfer, not a stable archive.",
            command = "dotnet run",
            recording = "graphics.hwt.json",
            files
        }, jsonOptions);

        // Freeze and persist playback before Quit causes alternate-screen teardown.
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "graphics.hwt.json"), recordingBytes, deadline.Token);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "sample.json"), manifestBytes, deadline.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, deadline.Token);
        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(5), deadline.Token);
        if (exitCode != 0)
            throw new InvalidOperationException($"The sample exited with code {exitCode}.");
        report = $"Saved {recording.Frames.Count} frames, {recording.DurationMs:F0} ms, " +
            $"{recordingBytes.Length:N0} JSON bytes; {recorder.Inspector.ImagePayloads} RGBA payloads " +
            $"({recorder.Inspector.ImageBytes:N0} bytes). " +
            $"Validated ready full baseline, revision chain, counts 0/1/2, and substantial KGP + Sixel placements.\n" +
            $"Sample exited cleanly (0). Output: {outputDirectory}";
    }
    catch (Exception error)
    {
        failures.Add(error);
    }
    finally
    {
        captureStop.Cancel();
        scenarioStop.Cancel();
        processStop.Cancel();
        if (captureTask is not null)
            await ObserveAsync(captureTask, captureStop.Token, failures);
        if (readinessTask is not null)
            await ObserveAsync(readinessTask, scenarioStop.Token, failures);
        if (scenarioTask is not null)
            await ObserveAsync(scenarioTask, scenarioStop.Token, failures);
        if (runTask is not null)
            await ObserveAsync(runTask, processStop.Token, failures);
        // Disposal owns the PTY and kills/reaps the child on failed or cancelled runs.
        try
        {
            await terminal.DisposeAsync();
        }
        catch (Exception error)
        {
            failures.Add(error);
        }
    }

    if (failures.Count != 0)
        throw new AggregateException("Recording failed; all reader/scenario/process tasks were observed.", failures.Distinct());
    Console.WriteLine(report);
}

static async Task ExerciseAsync(Hex1bTerminal terminal, HwtRecorder recorder, CancellationToken cancellationToken)
{
    for (var count = 0; count <= 2; count++)
    {
        var expected = $"Count: {count}    Scene:";
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(screen => screen.ContainsText(expected), TimeSpan.FromSeconds(12),
                $"the sample to display {expected}")
            .Build()
            .ApplyAsync(terminal, cancellationToken);
        await recorder.StateAsync(count).WaitAsync(TimeSpan.FromSeconds(12), cancellationToken);
        Console.WriteLine($"Captured count {count} with both graphics protocols.");
        // Intentional reading time for humans, not a readiness or synchronization delay.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        if (count < 2)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.Enter)
                .Build()
                .ApplyAsync(terminal, cancellationToken);
        }
    }
}

static async Task ObserveAsync(Task task, CancellationToken expectedCancellation, List<Exception> failures)
{
    try
    {
        await task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    catch (OperationCanceledException) when (expectedCancellation.IsCancellationRequested)
    {
    }
    catch (Exception error)
    {
        failures.Add(error);
    }
}
