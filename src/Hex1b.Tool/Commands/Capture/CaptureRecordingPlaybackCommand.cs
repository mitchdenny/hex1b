using System.CommandLine;
using System.Text;
using System.Text.Json;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Plays back an asciinema .cast recording file in the current terminal.
/// </summary>
internal sealed class CaptureRecordingPlaybackCommand : BaseCommand
{
    private static readonly Option<string> s_fileOption = new("--file") { Description = "Path to .cast file", Required = true };
    private static readonly Option<double> s_speedOption = new("--speed") { DefaultValueFactory = _ => 1.0, Description = "Playback speed multiplier (default: 1.0)" };
    private static readonly Option<bool> s_playerOption = new("--player") { Description = "Launch interactive TUI player with controls" };

    public CaptureRecordingPlaybackCommand(
        OutputFormatter formatter,
        ILogger<CaptureRecordingPlaybackCommand> logger)
        : base("playback", "Play back an asciinema recording in the terminal", formatter, logger)
    {
        Options.Add(s_fileOption);
        Options.Add(s_speedOption);
        Options.Add(s_playerOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var filePath = parseResult.GetValue(s_fileOption)!;
        var speed = parseResult.GetValue(s_speedOption);
        var usePlayer = parseResult.GetValue(s_playerOption);

        if (!File.Exists(filePath))
        {
            Formatter.WriteError($"File not found: {filePath}");
            return 1;
        }

        if (speed <= 0)
        {
            Formatter.WriteError("Speed must be greater than 0");
            return 1;
        }

        if (usePlayer)
        {
            return await PlaybackPlayerApp.RunAsync(filePath, speed, cancellationToken);
        }

        return await RunSimplePlaybackAsync(filePath, speed, cancellationToken);
    }

    private static async Task<int> RunSimplePlaybackAsync(string filePath, double speed, CancellationToken cancellationToken)
    {
        var stdout = Console.OpenStandardOutput();

        double previousTime = 0;
        bool headerRead = false;

        await foreach (var line in File.ReadLinesAsync(filePath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!headerRead)
            {
                headerRead = true;
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var arr = doc.RootElement;
                if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() < 3)
                    continue;

                var time = arr[0].GetDouble();
                var code = arr[1].GetString();
                var data = arr[2].GetString();

                if (code != "o" || data == null)
                    continue;

                var delay = (time - previousTime) / speed;
                if (delay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
                previousTime = time;

                var bytes = Encoding.UTF8.GetBytes(data);
                await stdout.WriteAsync(bytes, cancellationToken);
                await stdout.FlushAsync(cancellationToken);
            }
            catch (JsonException)
            {
                // Skip malformed event lines
            }
        }

        Console.WriteLine();
        return 0;
    }
}
