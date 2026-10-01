using System.Diagnostics;
using System.Text.Json;
using Hex1b;
using Hex1b.Automation;

namespace HwtRecordingDriver;

internal sealed record RecordedFrame(double TimeMs, string Data);
internal sealed record Recording(string Format, int Version, double DurationMs, IReadOnlyList<RecordedFrame> Frames);

internal sealed class HwtRecorder
{
    public const int MaximumJsonBytes = 64 * 1024 * 1024;
    private readonly List<RecordedFrame> _frames = [];
    private readonly TaskCompletionSource[] _states = Enumerable.Range(0, 3)
        .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
    private long _firstTimestamp;
    private long _estimatedJsonBytes = 1024;

    public HwtFrameInspector Inspector { get; } = new();
    public TaskCompletionSource Baseline { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task StateAsync(int count) => _states[count].Task;

    public static bool IsInitialScreenReady(Hex1bTerminalSnapshot screen)
        => screen.ContainsText("Count: 0    Scene:") &&
           screen.ContainsText("KGP / mountain light") &&
           screen.ContainsText("Sixel / ocean currents") &&
           screen.KgpPlacements.Any(placement =>
               screen.KgpImages.TryGetValue(placement.ImageId, out var image) &&
               image.Width >= 100 && image.Height >= 60) &&
           screen.SixelPlacements.Any(placement => placement.HasVisiblePaintedCells &&
               placement.GetPaintedPixels() is { Width: >= 100, Height: >= 60 });

    public async Task CaptureAsync(Hwt1PresentationAdapter presentation, CancellationToken cancellationToken)
    {
        while (true)
        {
            // Cancellation only interrupts the next read; a returned frame is always saved and acknowledged.
            var bytes = await presentation.ReadFrameAsync(cancellationToken);
            var now = Stopwatch.GetTimestamp();
            if (_frames.Count == 0)
                _firstTimestamp = now;
            var time = Stopwatch.GetElapsedTime(_firstTimestamp, now).TotalMilliseconds;
            if (_frames.Count >= 10_000 || time > 600_000)
                throw new InvalidDataException("Recording exceeds the spike's frame or duration limit.");
            var revision = Inspector.Apply(bytes);
            if (_frames.Count == 0 && !HasState(0))
                throw new InvalidDataException("The first full baseline must contain the ready initial screen and both graphics.");
            var data = Convert.ToBase64String(bytes.Span);
            _estimatedJsonBytes += data.Length + 128;
            if (_estimatedJsonBytes > MaximumJsonBytes)
                throw new InvalidDataException("Recording exceeds the spike's 64 MiB JSON limit.");
            _frames.Add(new(time, data));
            await presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = "ack", revision }));
            Baseline.TrySetResult();
            for (var count = 0; count < _states.Length; count++)
            {
                if (HasState(count))
                    _states[count].TrySetResult();
            }
        }
    }

    private bool HasState(int count)
        => Inspector.HasBothGraphics &&
           Inspector.Text.Contains($"Count: {count}    Scene:", StringComparison.Ordinal) &&
           Inspector.Text.Contains("KGP / mountain light", StringComparison.Ordinal) &&
           Inspector.Text.Contains("Sixel / ocean currents", StringComparison.Ordinal);

    // Called only after the reader has stopped, before the sample exits its alternate buffer.
    public Recording Finish()
    {
        if (_frames.Count == 0 || _states.Any(state => !state.Task.IsCompletedSuccessfully))
            throw new InvalidDataException("Recording did not capture all three text states with both graphics protocols.");
        var duration = Stopwatch.GetElapsedTime(_firstTimestamp).TotalMilliseconds;
        if (!double.IsFinite(duration) || duration > 600_000)
            throw new InvalidDataException("Recording duration is outside the spike limits.");
        return new("hex1b-hwt-recording", 1, Math.Max(duration, _frames[^1].TimeMs), _frames.ToArray());
    }
}
