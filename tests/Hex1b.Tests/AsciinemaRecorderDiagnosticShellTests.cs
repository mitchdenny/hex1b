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
                recorder.StartRecording(recordingPath, new AsciinemaRecorderOptions
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

            // Wait for the initial prompt to appear
            await WaitForPromptAsync(TimeSpan.FromSeconds(5));
        }

        public async Task WaitForPromptAsync(TimeSpan? timeout = null)
        {
            await WaitForTextAsync("diag>", timeout);
        }

        public async Task WaitForTextAsync(string expected, TimeSpan? timeout = null)
        {
            await WaitUntilAsync(
                text => text.Contains(expected, StringComparison.Ordinal),
                timeout,
                $"terminal text '{expected}'");
        }

        public async Task WaitUntilAsync(Func<string, bool> predicate, TimeSpan? timeout = null, string? description = null)
        {
            timeout ??= TimeSpan.FromSeconds(3);
            var deadline = DateTime.UtcNow + timeout.Value;
            var lastText = string.Empty;

            while (DateTime.UtcNow < deadline)
            {
                using var snapshot = Terminal.CreateSnapshot();
                lastText = snapshot.GetDisplayText();
                if (predicate(lastText))
                {
                    return;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            throw new Xunit.Sdk.XunitException(
                $"Timed out waiting for {description ?? "terminal state"}.\nLast terminal text:\n{lastText}");
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
        }

        public async Task SendCommandAndWaitForOutputAsync(string command, string expectedOutput, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);

            await SendCommandAsync(command);

            await WaitUntilAsync(
                text => text.Contains(expectedOutput, StringComparison.Ordinal)
                    && text.TrimEnd().EndsWith("diag>", StringComparison.Ordinal),
                timeout,
                $"completed command '{command}' with output '{expectedOutput}'");
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

    private static async Task<string> ReadAllTextSharedAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    private static Task WaitForRecordingTextAsync(string path, string expected, TimeSpan? timeout = null)
    {
        return WaitForRecordingAsync(
            path,
            text => text.Contains(expected, StringComparison.Ordinal),
            $"recording text '{expected}'",
            timeout);
    }

    private static async Task WaitForRecordingAsync(
        string path,
        Func<string, bool> predicate,
        string description,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;
        var lastContent = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                lastContent = await ReadAllTextSharedAsync(path, TestContext.Current.CancellationToken);
                if (predicate(lastContent))
                {
                    return;
                }
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new Xunit.Sdk.XunitException(
            $"Timed out waiting for {description} in recording '{path}'.\nLast file content:\n{lastContent}");
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
        await WaitForRecordingTextAsync(tempFile, "diag");

        // Assert
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
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
        await WaitForRecordingTextAsync(tempFile, "diag");

        // Assert - check that the prompt was captured
        // The prompt is: \x1b[1;32mdiag\x1b[0m> (green "diag" followed by ">")
        // So we just check for "diag" since the escape codes will be in the file
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
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
        await ctx.SendCommandAndWaitForOutputAsync("echo hello world", "hello world");
        await ctx.Recorder.FlushAsync();
        await WaitForRecordingTextAsync(tempFile, "hello world");

        // Assert
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
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
        await ctx.SendCommandAndWaitForOutputAsync("help", "dump", TimeSpan.FromSeconds(5));
        await ctx.Recorder.FlushAsync();
        await WaitForRecordingAsync(
            tempFile,
            text => text.Contains("echo", StringComparison.Ordinal) && text.Contains("ping", StringComparison.Ordinal),
            "complete help output");

        // Assert - help output should contain command names
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
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
        await ctx.SendCommandAndWaitForOutputAsync("echo test123", "test123");
        await ctx.Recorder.FlushAsync();
        await WaitForRecordingTextAsync(tempFile, "test123");

        // Assert
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
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
        ctx.Recorder.StartRecording(tempFile, new AsciinemaRecorderOptions
        {
            AutoFlush = true,
            Title = "Mid-Session Recording"
        });

        // Send a command that should be recorded
        await ctx.SendCommandAndWaitForOutputAsync("ping", "PONG");
        await ctx.Recorder.FlushAsync();
        await WaitForRecordingTextAsync(tempFile, "PONG");

        // Assert
        Assert.True(File.Exists(tempFile), "Recording file should exist");
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
        Assert.Contains("PONG", content);
    }

    [Fact]
    public async Task AsciinemaRecorder_StopRecording_FinalizesFile()
    {
        // Arrange
        var tempFile = GetTempFile();
        await using var ctx = DiagnosticShellTestContext.Create(tempFile);
        await ctx.StartAsync();
        await ctx.SendCommandAndWaitForOutputAsync("ping", "PONG");

        // Act
        var completedPath = await ctx.Recorder.StopRecordingAsync();

        // Assert
        Assert.Equal(tempFile, completedPath);
        Assert.False(ctx.Recorder.IsRecording);

        // File should be finalized and readable
        await WaitForRecordingTextAsync(tempFile, "PONG");
        var content = await ReadAllTextSharedAsync(tempFile, TestContext.Current.CancellationToken);
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
        await ctx.SendCommandAndWaitForOutputAsync("echo first", "first");
        await ctx.Recorder.StopRecordingAsync();
        await WaitForRecordingTextAsync(tempFile1, "first");

        // Second recording
        ctx.Recorder.StartRecording(tempFile2, new AsciinemaRecorderOptions { AutoFlush = true });
        await ctx.SendCommandAndWaitForOutputAsync("echo second", "second");
        await ctx.Recorder.StopRecordingAsync();
        await WaitForRecordingTextAsync(tempFile2, "second");

        // Assert
        var content1 = await ReadAllTextSharedAsync(tempFile1, TestContext.Current.CancellationToken);
        var content2 = await ReadAllTextSharedAsync(tempFile2, TestContext.Current.CancellationToken);
        
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
            ctx.Recorder.StartRecording(tempFile2, new AsciinemaRecorderOptions()));
        
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
