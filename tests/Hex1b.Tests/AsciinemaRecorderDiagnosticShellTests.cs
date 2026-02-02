using System.Text.Json;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for AsciinemaRecorder using DiagnosticShellWorkloadAdapter as a controlled, deterministic workload.
/// These tests validate both backward compatibility (recording from session start) and
/// the new dynamic recording functionality.
/// </summary>
public class AsciinemaRecorderDiagnosticShellTests : IAsyncDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"asciinema_test_{Guid.NewGuid():N}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(100); // Give time for file handles to close
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    /// <summary>
    /// Encapsulates the test context for running DiagnosticShell with asciinema recording.
    /// Manages lifecycle of terminal, shell, recorder, and the I/O loop.
    /// </summary>
    private sealed class DiagnosticShellTestContext : IAsyncDisposable
    {
        public Hex1bTerminal Terminal { get; }
        public DiagnosticShellWorkloadAdapter Shell { get; }
        public AsciinemaRecorder Recorder { get; }
        private Task? _runTask;
        private readonly CancellationTokenSource _cts = new();
        private readonly bool _startRecording;
        private readonly string? _recordingPath;

        private DiagnosticShellTestContext(
            Hex1bTerminal terminal,
            DiagnosticShellWorkloadAdapter shell,
            AsciinemaRecorder recorder,
            bool startRecording,
            string? recordingPath)
        {
            Terminal = terminal;
            Shell = shell;
            Recorder = recorder;
            _startRecording = startRecording;
            _recordingPath = recordingPath;
        }

        public static DiagnosticShellTestContext Create(string? recordingPath = null, bool startRecording = true)
        {
            var recorder = new AsciinemaRecorder();
            var shell = new DiagnosticShellWorkloadAdapter();
            var options = new Hex1bTerminalOptions
            {
                Width = 80,
                Height = 24,
                WorkloadAdapter = shell
            };
            options.WorkloadFilters.Add(recorder);
            var terminal = new Hex1bTerminal(options);

            if (startRecording && recordingPath != null)
            {
                recorder.StartRecording(recordingPath, 80, 24, new AsciinemaRecorderOptions
                {
                    AutoFlush = true,
                    Title = "Diagnostic Shell Test"
                });
            }

            return new DiagnosticShellTestContext(terminal, shell, recorder, startRecording, recordingPath);
        }

        public async Task StartAsync()
        {
            // Start the diagnostic shell (writes welcome message and prompt)
            Shell.Start();

            // Start the terminal I/O loop to pump input/output
            _runTask = Task.Run(async () =>
            {
                try
                {
                    await Terminal.RunAsync(_cts.Token);
                }
                catch (OperationCanceledException) { }
            });

            // Give terminal time to initialize and process output
            await Task.Delay(100);

            // Wait for the initial prompt to appear
            await WaitForPromptAsync();
        }

        public async Task WaitForPromptAsync(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(3);
            using var cts = new CancellationTokenSource(timeout.Value);
            
            while (!cts.Token.IsCancellationRequested)
            {
                using var snapshot = Terminal.CreateSnapshot();
                var text = snapshot.GetDisplayText();
                if (text.Contains("diag>"))
                    return;
                await Task.Delay(50);
            }
            throw new TimeoutException("Prompt did not appear within timeout");
        }

        public async Task SendCommandAsync(string command)
        {
            // Type the command character by character through the workload adapter
            foreach (var c in command)
            {
                await Shell.WriteInputAsync(new byte[] { (byte)c });
                await Task.Delay(5); // Small delay between chars
            }
            
            // Press Enter
            await Shell.WriteInputAsync(new byte[] { 0x0D }); // CR
            
            await Task.Delay(100); // Give time for output
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            if (_runTask != null)
            {
                try { await _runTask.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch { }
            }

            Terminal.Dispose();
            await Shell.DisposeAsync();
            await Recorder.DisposeAsync();
            _cts.Dispose();
        }
    }

    // ==========================================
    // Phase 3.5.1: Backward Compatibility Tests
    // ==========================================

    [Fact]
    public async Task AsciinemaRecorder_WithFilePath_RecordsFromSessionStart()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();

        // Act
        await ctx.Recorder.FlushAsync();

        // Assert
        var content = await File.ReadAllTextAsync(tempFile);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        Assert.True(lines.Length >= 2, "Should have header and at least one event");
        
        // Check header
        var header = JsonDocument.Parse(lines[0]);
        Assert.Equal(2, header.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(80, header.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(24, header.RootElement.GetProperty("height").GetInt32());
        
        // First output event should have timestamp close to 0
        var firstEvent = JsonDocument.Parse(lines[1]);
        var firstTime = firstEvent.RootElement[0].GetDouble();
        Assert.True(firstTime < 1.0, $"First event should be near time 0, was {firstTime}");
    }

    [Fact]
    public async Task AsciinemaRecorder_WithDiagnosticShell_CapturesInitialPrompt()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();

        // Act
        await ctx.Recorder.FlushAsync();

        // Assert - check that the prompt was captured
        // The prompt is: \x1b[1;32mdiag\x1b[0m> (green "diag" followed by ">")
        // So we just check for "diag" since the escape codes will be in the file
        var content = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("diag", content);
    }

    [Fact]
    public async Task AsciinemaRecorder_WithDiagnosticShell_CapturesCommandOutput()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();
        
        // Act - send echo command
        await ctx.SendCommandAsync("echo hello world");
        await Task.Delay(200);
        await ctx.Recorder.FlushAsync();

        // Assert
        var content = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("hello world", content);
    }

    // ==========================================
    // Phase 3.5.2: Command Recording Tests
    // ==========================================

    [Fact]
    public async Task AsciinemaRecorder_DiagnosticShell_HelpCommand_RecordsAllOutput()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();
        
        // Act
        await ctx.SendCommandAsync("help");
        await Task.Delay(300);
        await ctx.Recorder.FlushAsync();

        // Assert - help output should contain command names
        var content = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("echo", content);
        Assert.Contains("ping", content);
    }

    [Fact]
    public async Task AsciinemaRecorder_DiagnosticShell_EchoCommand_RecordsEchoedText()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();
        
        // Act
        await ctx.SendCommandAsync("echo test123");
        await Task.Delay(200);
        await ctx.Recorder.FlushAsync();

        // Assert
        var content = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("test123", content);
    }

    // ==========================================
    // Phase 3.5.3: Dynamic Recording Tests
    // ==========================================

    [Fact]
    public async Task AsciinemaRecorder_StartRecordingMidSession_CreatesFile()
    {
        // Arrange - create without starting recording
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile, startRecording: false);
        await ctx.StartAsync();
        
        // Act - start recording mid-session
        ctx.Recorder.StartRecording(tempFile, 80, 24, new AsciinemaRecorderOptions
        {
            AutoFlush = true,
            Title = "Mid-Session Recording"
        });

        // Send a command that should be recorded
        await ctx.SendCommandAsync("ping");
        await Task.Delay(200);
        await ctx.Recorder.FlushAsync();

        // Assert
        Assert.True(File.Exists(tempFile), "Recording file should exist");
        var content = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("PONG", content);
    }

    [Fact]
    public async Task AsciinemaRecorder_StopRecording_FinalizesFile()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();
        await ctx.SendCommandAsync("ping");
        await Task.Delay(200);

        // Act
        var completedPath = await ctx.Recorder.StopRecordingAsync();

        // Assert
        Assert.Equal(tempFile, completedPath);
        Assert.False(ctx.Recorder.IsRecording);
        
        // File should be finalized and readable
        var content = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("PONG", content);
    }

    [Fact]
    public async Task AsciinemaRecorder_StopAndStartNewRecording_CreatesSeparateFiles()
    {
        // Arrange
        var tempFile1 = GetTempFile();
        var tempFile2 = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile1);
        await ctx.StartAsync();
        
        // First recording
        await ctx.SendCommandAsync("echo first");
        await Task.Delay(200);
        await ctx.Recorder.StopRecordingAsync();

        // Second recording
        ctx.Recorder.StartRecording(tempFile2, 80, 24, new AsciinemaRecorderOptions { AutoFlush = true });
        await ctx.SendCommandAsync("echo second");
        await Task.Delay(200);
        await ctx.Recorder.StopRecordingAsync();

        // Assert
        var content1 = await File.ReadAllTextAsync(tempFile1);
        var content2 = await File.ReadAllTextAsync(tempFile2);
        
        Assert.Contains("first", content1);
        Assert.DoesNotContain("second", content1);
        
        Assert.Contains("second", content2);
    }

    // ==========================================
    // Phase 3.5.4: Non-overlapping Recording Tests
    // ==========================================

    [Fact]
    public async Task AsciinemaRecorder_StartWhileRecording_ThrowsInvalidOperationException()
    {
        // Arrange
        var tempFile1 = GetTempFile();
        var tempFile2 = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile1);
        await ctx.StartAsync();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ctx.Recorder.StartRecording(tempFile2, 80, 24, new AsciinemaRecorderOptions()));
        
        Assert.Contains("already recording", ex.Message.ToLower());
    }

    [Fact]
    public async Task AsciinemaRecorder_StopWhileNotRecording_ReturnsNull()
    {
        // Arrange - create without starting recording
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile, startRecording: false);
        await ctx.StartAsync();

        // Act
        var result = await ctx.Recorder.StopRecordingAsync();

        // Assert
        Assert.Null(result);
    }
}
