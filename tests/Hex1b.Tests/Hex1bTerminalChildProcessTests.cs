using System.Text;
using Hex1b.Input;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="Hex1bTerminalChildProcess"/>.
/// These tests require Linux (for PTY support via forkpty).
/// Tests that require Linux use runtime checks to skip on other platforms.
/// </summary>
public class Hex1bTerminalChildProcessTests
{
    /// <summary>
    /// Verifies that when we launch bash with "tty" command, it reports
    /// a valid TTY device path (e.g., /dev/pts/X), proving a PTY is attached.
    /// </summary>
    [Fact(Skip = "Flaky test - needs investigation")]
    [Trait("Category", "Unix")]
    public async Task BashWithTty_ReportsPtyDevice()
    {
        
        // Launch bash with interactive flag (-i) to ensure it thinks it's a terminal
        // The command runs "tty" which outputs the TTY device path
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/bash",
            ["-ic", "tty; exit"]
        );
        
        await process.StartAsync();
        
        // Collect output
        var output = new StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        try
        {
            while (!cts.Token.IsCancellationRequested && !process.HasExited)
            {
                var data = await process.ReadOutputAsync(cts.Token);
                if (data.IsEmpty)
                {
                    await Task.Delay(50, cts.Token);
                    continue;
                }
                
                var text = Encoding.UTF8.GetString(data.Span);
                output.Append(text);
                
                // Check if we got a TTY path
                if (output.ToString().Contains("/dev/"))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }
        
        var result = output.ToString();
        
        // The tty command should output something like /dev/pts/0 or /dev/ttyp0
        Assert.Contains("/dev/", result);
        Assert.True(
            result.Contains("/dev/pts/") || result.Contains("/dev/tty"),
            $"Expected PTY device path, got: {result}"
        );
    }
    
    /// <summary>
    /// Verifies that we can launch bash and it stays running at a prompt.
    /// </summary>
    [Fact(Skip = "Flaky test - needs investigation")]
    [Trait("Category", "Unix")]
    public async Task BashInteractive_StaysAtPrompt()
    {
        
        // Launch: bash -ic "tty;bash"
        // This prints the TTY and then drops to an interactive bash prompt
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/bash",
            ["-ic", "tty;bash"]
        );
        
        await process.StartAsync();
        
        Assert.True(process.HasStarted);
        Assert.True(process.ProcessId > 0, $"Expected positive PID, got {process.ProcessId}");
        
        // Read initial output (should include the tty path)
        var output = new StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var data = await process.ReadOutputAsync(cts.Token);
                if (data.IsEmpty)
                {
                    await Task.Delay(50, cts.Token);
                    continue;
                }
                
                var text = Encoding.UTF8.GetString(data.Span);
                output.Append(text);
                
                // Once we see the /dev/pts path, we've got our confirmation
                if (output.ToString().Contains("/dev/"))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout is acceptable
        }
        
        var result = output.ToString();
        
        // Verify PTY is attached
        Assert.Contains("/dev/", result);
        
        // Process should still be running (bash is waiting for input)
        Assert.False(process.HasExited, "Process should still be running");
        
        // Send exit command
        await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"));
        await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n")); // Exit nested bash too
        
        // Wait for exit with timeout
        var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exitCode = await process.WaitForExitAsync(exitCts.Token);
        
        Assert.True(process.HasExited);
        Assert.Equal(0, exitCode);
    }
    
    /// <summary>
    /// Verifies that echo command works through the PTY.
    /// </summary>
    [Fact(Skip = "Test hangs - needs investigation")]
    [Trait("Category", "Unix")]
    public async Task Echo_WritesToOutput()
    {
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/bash",
            ["-c", "echo 'Hello from PTY!'"]
        );
        
        await process.StartAsync();
        
        var output = new StringBuilder();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        // Read output until we get our expected text or process exits
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var data = await process.ReadOutputAsync(cts.Token);
                if (data.IsEmpty)
                {
                    // Empty read typically means process exited
                    break;
                }
                
                output.Append(Encoding.UTF8.GetString(data.Span));
                
                // Early exit once we have the expected output
                if (output.ToString().Contains("Hello from PTY!"))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }
        
        // Wait for exit
        var exitCode = await process.WaitForExitAsync();
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Hello from PTY!", output.ToString());
    }
    
    /// <summary>
    /// Verifies that input can be written to the process and is echoed back.
    /// </summary>
    [Fact]
    [Trait("Category", "Unix")]
    public async Task WriteInput_IsEchoedBack()
    {
        // Skip on non-Linux platforms - PTY support requires Linux
        if (!OperatingSystem.IsLinux())
            return;
        
        // Launch cat which will echo input back
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/cat",
            []
        );
        
        await process.StartAsync();
        
        // Write some input
        var input = "test input\n";
        await process.WriteInputAsync(Encoding.UTF8.GetBytes(input));
        
        // Read output
        var output = new StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var data = await process.ReadOutputAsync(cts.Token);
                if (data.IsEmpty)
                {
                    await Task.Delay(50, cts.Token);
                    continue;
                }
                
                output.Append(Encoding.UTF8.GetString(data.Span));
                
                // Cat should echo back our input
                if (output.ToString().Contains("test input"))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }
        
        Assert.Contains("test input", output.ToString());
        
        // Send EOF (Ctrl+D)
        await process.WriteInputAsync(new byte[] { 0x04 });
        
        var exitCode = await process.WaitForExitAsync(CancellationToken.None);
        Assert.Equal(0, exitCode);
    }
    
    /// <summary>
    /// Verifies that terminal resize works via SIGWINCH.
    /// </summary>
    [Fact(Skip = "Flaky test - needs investigation")]
    [Trait("Category", "Unix")]
    public async Task Resize_UpdatesTerminalSize()
    {
        
        // Launch bash with a command that prints terminal size
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/bash",
            ["-ic", "stty size; read -t 2; stty size; exit"],
            workingDirectory: null,
            environment: null,
            inheritEnvironment: true,
            initialWidth: 80,
            initialHeight: 24
        );
        
        await process.StartAsync();
        
        // Read initial size output
        var output = new StringBuilder();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var data = await process.ReadOutputAsync(cts.Token);
                if (!data.IsEmpty)
                    output.Append(Encoding.UTF8.GetString(data.Span));
                else
                    await Task.Delay(50, cts.Token);
                    
                if (output.ToString().Contains("24 80"))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }
        
        // Should show initial size (rows cols format from stty)
        Assert.Contains("24 80", output.ToString());
        
        // Resize
        await process.ResizeAsync(120, 40);
        
        // Send enter to continue (releases the read)
        await process.WriteInputAsync(Encoding.UTF8.GetBytes("\n"));
        
        // Read new size output
        var output2 = new StringBuilder();
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        try
        {
            while (!cts2.Token.IsCancellationRequested && !process.HasExited)
            {
                var data = await process.ReadOutputAsync(cts2.Token);
                if (!data.IsEmpty)
                    output2.Append(Encoding.UTF8.GetString(data.Span));
                else
                    await Task.Delay(50, cts2.Token);
                    
                if (output2.ToString().Contains("40 120"))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }
        
        // Should show new size
        Assert.Contains("40 120", output2.ToString());
    }
    
    /// <summary>
    /// Verifies that the process integrates with Hex1bTerminal as a workload adapter.
    /// </summary>
    [Fact(Skip = "Flaky test - needs investigation")]
    [Trait("Category", "Unix")]
    public async Task Integration_WithHex1bTerminal()
    {
        
        // Launch bash that prints something recognizable
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/bash",
            ["-c", "echo 'PTY_TEST_MARKER'; exit 0"]
        );
        
        await process.StartAsync();
        
        // Create a capturing presentation adapter
        var presentation = new CapturingTestPresentationAdapter();
        
        // Create a Hex1bTerminal with both workload and presentation
        // This will auto-start the pump
        using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = process,
            Width = 80,
            Height = 24
        });
        
        // Wait for output to appear in the captured output
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        while (!cts.Token.IsCancellationRequested)
        {
            if (presentation.CapturedOutput.Contains("PTY_TEST_MARKER"))
                break;
            
            if (process.HasExited)
            {
                // Give it a little more time to pump remaining data
                await Task.Delay(200, cts.Token);
                break;
            }
                
            await Task.Delay(50, cts.Token);
        }
        
        // Verify the output appeared in the presentation
        Assert.Contains("PTY_TEST_MARKER", presentation.CapturedOutput);
    }
    
    /// <summary>
    /// Simple presentation adapter that captures output for testing.
    /// </summary>
    private class CapturingTestPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly StringBuilder _output = new();
        private readonly object _lock = new();
        private readonly int _width;
        private readonly int _height;
        
        public CapturingTestPresentationAdapter(int width = 80, int height = 24)
        {
            _width = width;
            _height = height;
        }
        
        public string CapturedOutput
        {
            get { lock (_lock) return _output.ToString(); }
        }
        
        public int Width => _width;
        public int Height => _height;
        public TerminalCapabilities Capabilities => new() { SupportsMouse = false };
        
#pragma warning disable CS0067 // Events required by interface but not used in test implementation
        public event Action<int, int>? Resized;
        public event Action? Disconnected;
#pragma warning restore CS0067
        
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            lock (_lock) _output.Append(Encoding.UTF8.GetString(data.Span));
            return ValueTask.CompletedTask;
        }
        
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
            => new(Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => ReadOnlyMemory<byte>.Empty));
        
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    
    /// <summary>
    /// Verifies that killing the process works.
    /// </summary>
    [Fact(Skip = "Flaky test - needs investigation")]
    [Trait("Category", "Unix")]
    public async Task Kill_TerminatesProcess()
    {
        
        // Launch a long-running process
        await using var process = new Hex1bTerminalChildProcess(
            "/bin/bash",
            ["-c", "sleep 60"]
        );
        
        await process.StartAsync();
        
        Assert.True(process.HasStarted);
        Assert.False(process.HasExited);
        
        // Give it a moment to start
        await Task.Delay(200);
        
        // Kill it
        process.Kill();
        
        // Wait for exit
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exitCode = await process.WaitForExitAsync(cts.Token);
        
        Assert.True(process.HasExited);
        // SIGTERM (15) results in exit code 128 + 15 = 143
        Assert.True(exitCode != 0, $"Expected non-zero exit code from killed process, got {exitCode}");
    }
    
    /// <summary>
    /// Verifies proper cleanup when disposing without waiting for exit.
    /// </summary>
    [Fact]
    [Trait("Category", "Unix")]
    public async Task Dispose_CleansUpRunningProcess()
    {
        // Skip on non-Linux platforms - PTY support requires Linux
        if (!OperatingSystem.IsLinux())
            return;
        
        int pid;
        
        {
            var process = new Hex1bTerminalChildProcess(
                "/bin/bash",
                ["-c", "sleep 60"]
            );
            
            await process.StartAsync();
            pid = process.ProcessId;
            
            Assert.True(pid > 0);
            
            // Dispose without waiting
            await process.DisposeAsync();
        }
        
        // Give the OS a moment to clean up
        await Task.Delay(200);
        
        // Verify process is gone (kill with signal 0 checks if process exists)
        // This will throw or return error if process doesn't exist
        var exists = ProcessExists(pid);
        Assert.False(exists, $"Process {pid} should have been terminated");
    }
    
    private static bool ProcessExists(int pid)
    {
        try
        {
            // Sending signal 0 just checks if process exists
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Stress test that launches mapscii via npx and captures the output.
    /// This test exercises the full PTY -> Hex1bTerminal pipeline with a real interactive
    /// application that uses advanced terminal features (Unicode, 256 color, cursor positioning).
    /// This test is primarily for visual validation and is not intended to be kept long-term.
    /// </summary>
    [Fact(Skip = "Stress test - run manually for visual validation")]
    [Trait("Category", "Unix")]
    [Trait("Category", "StressTest")]
    public async Task StressTest_MapsciiViaInteractiveBash()
    {
        // Setup temp file for asciinema recording
        var castFile = Path.Combine(Path.GetTempPath(), $"mapscii_{Guid.NewGuid()}.cast");
        
        try
        {
            // Launch interactive bash without user profile for predictable prompt
            await using var process = new Hex1bTerminalChildProcess(
                "/bin/bash",
                ["--norc", "--noprofile"],
                inheritEnvironment: true,
                initialWidth: 120,
                initialHeight: 40
            );
            
            await process.StartAsync();
            
            // Create terminal options with Asciinema recorder
            var options = new Hex1bTerminalOptions
            {
                Width = 120,
                Height = 40,
                WorkloadAdapter = process,
                PresentationAdapter = new CapturingTestPresentationAdapter(120, 40)
            };
            var recorder = new AsciinemaRecorder(castFile, new AsciinemaRecorderOptions
            {
                Title = "Mapscii Stress Test",
                CaptureInput = true
            });
            options.WorkloadFilters.Add(recorder);
            
            // Create Hex1bTerminal - the presence of PresentationAdapter auto-starts the pump
            using var terminal = new Hex1bTerminal(options);
            
            // Step 1: Wait for bash prompt (look for $ or # or PS1 indicator)
            var promptFound = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#") ||
                      terminal.CreateSnapshot().ContainsText(">"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            if (!promptFound)
            {
                // Capture what we have so far for debugging
                TestCaptureHelper.Capture(terminal, "prompt-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "mapscii-prompt-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for bash prompt. Check captured snapshot.");
            }
            
            // Step 2: Send the mapscii command
            var mapsciiCommand = Encoding.UTF8.GetBytes("npx mapscii\n");
            await process.WriteInputAsync(mapsciiCommand);
            
            // Step 3: Wait for mapscii to load (detect "center:" in output which appears in the status bar)
            var mapLoaded = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("center:"),
                TimeSpan.FromSeconds(60), // mapscii/npm can take a while to start
                TestContext.Current.CancellationToken
            );
            
            if (!mapLoaded)
            {
                // Capture for debugging
                TestCaptureHelper.Capture(terminal, "mapscii-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "mapscii-load-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for mapscii to load. Check captured snapshot.");
            }
            
            // Wait for the display to stabilize - mapscii may still be rendering after "center:" appears
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            
            // Validate that the map is fully rendered by checking corner cells for braille characters
            // Braille characters are in Unicode range U+2800-U+28FF
            // The map should fill the entire screen except the status bar (last row)
            var buffer = terminal.GetScreenBuffer();
            var height = 40; // Terminal height
            var width = 120; // Terminal width
            var statusBarRow = height - 1; // Last row is status bar
            var mapLastRow = statusBarRow - 1; // Second to last row is the last row of the map
            
            // Helper to check if a character is a braille pattern
            static bool IsBraille(string ch) => 
                !string.IsNullOrEmpty(ch) && ch.Length > 0 && ch[0] >= '\u2800' && ch[0] <= '\u28FF';
            
            // Check corner positions
            var topLeft = buffer[0, 0].Character;
            var topRight = buffer[0, width - 1].Character;
            var bottomLeft = buffer[mapLastRow, 0].Character;
            var bottomRight = buffer[mapLastRow, width - 1].Character;
            
            // Log corner values for diagnostics
            var cornerDiagnostics = new StringBuilder();
            cornerDiagnostics.AppendLine($"Corner diagnostics at {DateTime.UtcNow}:");
            cornerDiagnostics.AppendLine($"  Top-left [0,0]: '{topLeft}' (U+{(topLeft.Length > 0 ? ((int)topLeft[0]).ToString("X4") : "EMPTY")}) IsBraille={IsBraille(topLeft)}");
            cornerDiagnostics.AppendLine($"  Top-right [0,{width-1}]: '{topRight}' (U+{(topRight.Length > 0 ? ((int)topRight[0]).ToString("X4") : "EMPTY")}) IsBraille={IsBraille(topRight)}");
            cornerDiagnostics.AppendLine($"  Bottom-left [{mapLastRow},0]: '{bottomLeft}' (U+{(bottomLeft.Length > 0 ? ((int)bottomLeft[0]).ToString("X4") : "EMPTY")}) IsBraille={IsBraille(bottomLeft)}");
            cornerDiagnostics.AppendLine($"  Bottom-right [{mapLastRow},{width-1}]: '{bottomRight}' (U+{(bottomRight.Length > 0 ? ((int)bottomRight[0]).ToString("X4") : "EMPTY")}) IsBraille={IsBraille(bottomRight)}");
            
            // Also log the first few cells of the top row for additional context
            cornerDiagnostics.AppendLine("  First 10 cells of row 0:");
            for (int x = 0; x < 10 && x < width; x++)
            {
                var ch = buffer[0, x].Character;
                cornerDiagnostics.AppendLine($"    [0,{x}]: '{ch}' (U+{(ch.Length > 0 ? ((int)ch[0]).ToString("X4") : "EMPTY")})");
            }
            
            // Log the last 10 cells of the top row
            cornerDiagnostics.AppendLine($"  Last 10 cells of row 0:");
            for (int x = Math.Max(0, width - 10); x < width; x++)
            {
                var ch = buffer[0, x].Character;
                cornerDiagnostics.AppendLine($"    [0,{x}]: '{ch}' (U+{(ch.Length > 0 ? ((int)ch[0]).ToString("X4") : "EMPTY")})");
            }
            
            // Find where the blank area starts on row 0
            int blankStart = -1;
            for (int x = 0; x < width; x++)
            {
                if (!IsBraille(buffer[0, x].Character))
                {
                    blankStart = x;
                    break;
                }
            }
            cornerDiagnostics.AppendLine($"  Blank area on row 0 starts at column: {blankStart}");
            
            // Look at sequence numbers around the transition point
            if (blankStart > 0)
            {
                cornerDiagnostics.AppendLine($"  Sequence numbers around blank start (col {blankStart}):");
                for (int x = Math.Max(0, blankStart - 3); x < Math.Min(width, blankStart + 5); x++)
                {
                    var cell = buffer[0, x];
                    cornerDiagnostics.AppendLine($"    [0,{x}]: '{cell.Character}' Seq={cell.Sequence} Written={cell.WrittenAt:HH:mm:ss.fff}");
                }
                
                // Compare to sequence numbers in row 1 at same positions
                cornerDiagnostics.AppendLine($"  For comparison, row 1 at same positions:");
                for (int x = Math.Max(0, blankStart - 3); x < Math.Min(width, blankStart + 5); x++)
                {
                    var cell = buffer[1, x];
                    cornerDiagnostics.AppendLine($"    [1,{x}]: '{cell.Character}' Seq={cell.Sequence}");
                }
            }
            
            // Check if this is consistent across rows - find first non-braille on rows 1-5
            for (int row = 1; row <= 5 && row < height - 1; row++)
            {
                int rowBlankStart = -1;
                for (int x = 0; x < width; x++)
                {
                    if (!IsBraille(buffer[row, x].Character))
                    {
                        rowBlankStart = x;
                        break;
                    }
                }
                cornerDiagnostics.AppendLine($"  Row {row} first non-braille at column: {rowBlankStart}");
            }
            
            TestContext.Current.TestOutputHelper?.WriteLine(cornerDiagnostics.ToString());
            
            // Check if all corners have braille characters
            var allCornersHaveBraille = IsBraille(topLeft) && IsBraille(topRight) && 
                                        IsBraille(bottomLeft) && IsBraille(bottomRight);
            
            // Capture the map display
            TestCaptureHelper.Capture(terminal, "mapscii-map");
            
            if (!allCornersHaveBraille)
            {
                // Capture additional diagnostics
                await TestCaptureHelper.CaptureCastAsync(recorder, "mapscii-incomplete-render", TestContext.Current.CancellationToken);
                Assert.Fail($"Map render incomplete - corners missing braille characters.\n{cornerDiagnostics}");
            }
            
            // Step 4: Send 'q' to quit mapscii
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("q"));
            
            // Wait a moment for mapscii to exit
            await Task.Delay(500, TestContext.Current.CancellationToken);
            
            // Step 5: Wait for bash prompt to return
            var promptReturned = await WaitForConditionAsync(
                () => !terminal.CreateSnapshot().ContainsText("center:") &&
                      (terminal.CreateSnapshot().ContainsText("$") || 
                       terminal.CreateSnapshot().ContainsText("#")),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            // Capture final state
            TestCaptureHelper.Capture(terminal, "mapscii-after-quit");
            
            // Send exit to bash
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"));
            
            // Wait for process to exit
            var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(exitCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
            }
            
            // Flush and capture the Asciinema recording
            await TestCaptureHelper.CaptureCastAsync(recorder, "mapscii", TestContext.Current.CancellationToken);
            
            // Assert that we at least saw the map
            Assert.True(mapLoaded, "Mapscii should have loaded and displayed the map");
        }
        finally
        {
            // Cleanup temp file
            try { File.Delete(castFile); } catch { }
        }
    }
    
    /// <summary>
    /// Stress test that launches btop and captures the output.
    /// This test exercises the full PTY -> Hex1bTerminal pipeline with a real interactive
    /// application that uses advanced terminal features (Unicode box drawing, 24-bit color, 
    /// braille graphs, cursor positioning).
    /// This test is primarily for visual validation and is not intended to be kept long-term.
    /// </summary>
    [Fact(Skip = "Stress test - run manually for visual validation")]
    [Trait("Category", "Unix")]
    [Trait("Category", "StressTest")]
    public async Task StressTest_BtopViaInteractiveBash()
    {
        // Setup temp file for asciinema recording
        var castFile = Path.Combine(Path.GetTempPath(), $"btop_{Guid.NewGuid()}.cast");
        
        try
        {
            // Launch interactive bash without user profile for predictable prompt
            await using var process = new Hex1bTerminalChildProcess(
                "/bin/bash",
                ["--norc", "--noprofile"],
                inheritEnvironment: true,
                initialWidth: 120,
                initialHeight: 40
            );
            
            await process.StartAsync();
            
            // Create terminal options with Asciinema recorder
            var options = new Hex1bTerminalOptions
            {
                Width = 120,
                Height = 40,
                WorkloadAdapter = process,
                PresentationAdapter = new CapturingTestPresentationAdapter(120, 40)
            };
            var recorder = new AsciinemaRecorder(castFile, new AsciinemaRecorderOptions
            {
                Title = "Btop Stress Test",
                CaptureInput = true
            });
            options.WorkloadFilters.Add(recorder);
            
            // Create Hex1bTerminal - the presence of PresentationAdapter auto-starts the pump
            using var terminal = new Hex1bTerminal(options);
            
            // Step 1: Wait for bash prompt (look for $ or # or PS1 indicator)
            var promptFound = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#") ||
                      terminal.CreateSnapshot().ContainsText(">"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            if (!promptFound)
            {
                // Capture what we have so far for debugging
                TestCaptureHelper.Capture(terminal, "btop-prompt-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "btop-prompt-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for bash prompt. Check captured snapshot.");
            }
            
            // Step 2: Send the btop command
            var btopCommand = Encoding.UTF8.GetBytes("btop\n");
            await process.WriteInputAsync(btopCommand);
            
            // Step 3: Wait for btop to load (detect "CPU" which appears in the header)
            var btopLoaded = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("CPU") ||
                      terminal.CreateSnapshot().ContainsText("cpu"),
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken
            );
            
            if (!btopLoaded)
            {
                // Capture for debugging
                TestCaptureHelper.Capture(terminal, "btop-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "btop-load-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for btop to load. Check captured snapshot.");
            }
            
            // Wait a bit for the display to stabilize and show data
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            
            // Capture the btop display
            TestCaptureHelper.Capture(terminal, "btop-display");
            
            // Step 4: Send 'q' to quit btop
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("q"));
            
            // Wait a moment for btop to exit
            await Task.Delay(500, TestContext.Current.CancellationToken);
            
            // Step 5: Wait for bash prompt to return
            var promptReturned = await WaitForConditionAsync(
                () => !terminal.CreateSnapshot().ContainsText("CPU") &&
                      (terminal.CreateSnapshot().ContainsText("$") || 
                       terminal.CreateSnapshot().ContainsText("#")),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            // Capture final state
            TestCaptureHelper.Capture(terminal, "btop-after-quit");
            
            // Send exit to bash
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"));
            
            // Wait for process to exit
            var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(exitCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
            }
            
            // Flush and capture the Asciinema recording
            await TestCaptureHelper.CaptureCastAsync(recorder, "btop", TestContext.Current.CancellationToken);
            
            // Assert that we at least saw btop
            Assert.True(btopLoaded, "Btop should have loaded and displayed the interface");
        }
        finally
        {
            // Cleanup temp file
            try { File.Delete(castFile); } catch { }
        }
    }
    
    /// <summary>
    /// Stress test that launches the sl (steam locomotive) program and captures the animation.
    /// This test is different from mapscii/btop because sl runs an animation and then terminates
    /// on its own without requiring user input to quit.
    /// Exercises cursor movement, Unicode art, and proper process termination handling.
    /// This test is primarily for visual validation and is not intended to be kept long-term.
    /// </summary>
    [Fact(Skip = "Stress test - run manually for visual validation")]
    [Trait("Category", "Unix")]
    [Trait("Category", "StressTest")]
    public async Task StressTest_SlSteamLocomotive()
    {
        // Setup temp file for asciinema recording
        var castFile = Path.Combine(Path.GetTempPath(), $"sl_{Guid.NewGuid()}.cast");
        
        try
        {
            // Launch sl directly (not through bash) - it will run and terminate on its own
            // sl is typically in /usr/games on Debian/Ubuntu systems
            var slPath = File.Exists("/usr/games/sl") ? "/usr/games/sl" : "/usr/bin/sl";
            
            if (!File.Exists(slPath))
            {
                // Skip if sl is not installed
                return;
            }
            
            await using var process = new Hex1bTerminalChildProcess(
                slPath,
                [], // No arguments - just run the default locomotive
                inheritEnvironment: true,
                initialWidth: 120,
                initialHeight: 40
            );
            
            await process.StartAsync();
            
            // Create terminal options with Asciinema recorder
            var options = new Hex1bTerminalOptions
            {
                Width = 120,
                Height = 40,
                WorkloadAdapter = process,
                PresentationAdapter = new CapturingTestPresentationAdapter(120, 40)
            };
            var recorder = new AsciinemaRecorder(castFile, new AsciinemaRecorderOptions
            {
                Title = "SL Steam Locomotive Stress Test",
                CaptureInput = false // sl doesn't take input
            });
            options.WorkloadFilters.Add(recorder);
            
            // Create Hex1bTerminal - the presence of PresentationAdapter auto-starts the pump
            using var terminal = new Hex1bTerminal(options);
            
            // sl draws ASCII art of a steam locomotive - look for common characters
            // The locomotive uses characters like ( ) _ | and various others
            var locomotiveDetected = false;
            var snapshotsCaptured = 0;
            
            // Wait for sl to run and capture snapshots during the animation
            var startTime = DateTime.UtcNow;
            var maxWait = TimeSpan.FromSeconds(30);
            
            while (!process.HasExited && DateTime.UtcNow - startTime < maxWait)
            {
                await Task.Delay(200, TestContext.Current.CancellationToken);
                
                var snapshot = terminal.CreateSnapshot();
                
                // Check for locomotive characters (the train uses lots of underscores, pipes, and parentheses)
                if (snapshot.ContainsText("____") || snapshot.ContainsText("(@)") || 
                    snapshot.ContainsText("|") || snapshot.ContainsText("==="))
                {
                    locomotiveDetected = true;
                    
                    // Capture a few frames of the animation
                    if (snapshotsCaptured < 5)
                    {
                        TestCaptureHelper.Capture(terminal, $"sl-frame-{snapshotsCaptured}");
                        snapshotsCaptured++;
                    }
                }
            }
            
            // Wait for the process to exit naturally
            var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int exitCode;
            try
            {
                exitCode = await process.WaitForExitAsync(exitCts.Token);
            }
            catch (OperationCanceledException)
            {
                // If it's still running after timeout, kill it
                // This can happen if the terminal is slow or sl animation takes longer than expected
                process.Kill();
                exitCode = -1;
            }
            
            // Capture final state
            TestCaptureHelper.Capture(terminal, "sl-final");
            
            // Flush and capture the Asciinema recording
            await TestCaptureHelper.CaptureCastAsync(recorder, "sl", TestContext.Current.CancellationToken);
            
            // Assert that sl ran successfully - the key assertion is detecting the locomotive
            // The exit code may vary: 0 if completed, -1 if killed due to timeout, or other values
            // depending on the system. The important thing is we saw the animation.
            Assert.True(locomotiveDetected, "Should have detected locomotive ASCII art");
            Assert.True(process.HasExited, "sl should have terminated (either naturally or killed)");
        }
        finally
        {
            // Cleanup temp file
            try { File.Delete(castFile); } catch { }
        }
    }
    
    /// <summary>
    /// Stress test that clones the adamsky/globe repo, builds it with Docker, and runs the globe.
    /// This test exercises a complex multi-step terminal workflow including:
    /// - Git clone
    /// - Docker build
    /// - Running an interactive containerized application
    /// - Mouse drag gestures for globe rotation
    /// This test is primarily for visual validation and is not intended to be kept long-term.
    /// Requires Docker to be installed and running.
    /// </summary>
    [Fact(Skip = "Stress test - run manually for visual validation")]
    [Trait("Category", "Unix")]
    [Trait("Category", "StressTest")]
    [Trait("Category", "Docker")]
    public async Task StressTest_GlobeWithDockerAndMouse()
    {
        // Check if docker is available
        var dockerCheck = await RunCommandAsync("which", ["docker"]);
        if (string.IsNullOrEmpty(dockerCheck) || !File.Exists(dockerCheck.Trim()))
        {
            // Skip if docker is not installed
            return;
        }
        
        // Create temp directory for the clone
        var tempDir = Path.Combine(Path.GetTempPath(), $"globe_test_{Guid.NewGuid()}");
        var castFile = Path.Combine(Path.GetTempPath(), $"globe_{Guid.NewGuid()}.cast");
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Launch interactive bash without user profile for predictable prompt
            await using var process = new Hex1bTerminalChildProcess(
                "/bin/bash",
                ["--norc", "--noprofile"],
                workingDirectory: tempDir,
                inheritEnvironment: true,
                initialWidth: 120,
                initialHeight: 40
            );
            
            await process.StartAsync();
            
            // Create terminal options with Asciinema recorder
            var options = new Hex1bTerminalOptions
            {
                Width = 120,
                Height = 40,
                WorkloadAdapter = process,
                PresentationAdapter = new CapturingTestPresentationAdapter(120, 40)
            };
            var recorder = new AsciinemaRecorder(castFile, new AsciinemaRecorderOptions
            {
                Title = "Globe Docker Stress Test with Mouse Interaction",
                CaptureInput = true
            });
            options.WorkloadFilters.Add(recorder);
            
            // Create Hex1bTerminal
            using var terminal = new Hex1bTerminal(options);
            
            // Step 1: Wait for bash prompt
            var promptFound = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            if (!promptFound)
            {
                TestCaptureHelper.Capture(terminal, "globe-prompt-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "globe-prompt-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for bash prompt.");
            }
            
            // Step 2: Clone the globe repo
            await process.WriteInputAsync(Encoding.UTF8.GetBytes($"git clone https://github.com/adamsky/globe {tempDir}/globe\n"));
            
            var cloneComplete = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("Cloning into") ||
                      terminal.CreateSnapshot().ContainsText("done") ||
                      terminal.CreateSnapshot().ContainsText("fatal:") ||
                      terminal.CreateSnapshot().ContainsText("$"),
                TimeSpan.FromSeconds(60),
                TestContext.Current.CancellationToken
            );
            
            // Wait for prompt to return after clone
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            
            TestCaptureHelper.Capture(terminal, "globe-after-clone");
            
            // Check if clone succeeded
            if (!Directory.Exists(Path.Combine(tempDir, "globe")))
            {
                await TestCaptureHelper.CaptureCastAsync(recorder, "globe-clone-failed", TestContext.Current.CancellationToken);
                Assert.Fail("Git clone failed. Check captured snapshot.");
            }
            
            // Step 3: cd into the globe directory
            await process.WriteInputAsync(Encoding.UTF8.GetBytes($"cd {tempDir}/globe\n"));
            await Task.Delay(500, TestContext.Current.CancellationToken);
            
            // Step 4: Build the Docker image
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("docker build -t globe .\n"));
            
            var buildComplete = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("Successfully built") ||
                      terminal.CreateSnapshot().ContainsText("Successfully tagged") ||
                      terminal.CreateSnapshot().ContainsText("naming to docker.io") ||
                      terminal.CreateSnapshot().ContainsText("ERROR") ||
                      terminal.CreateSnapshot().ContainsText("error:"),
                TimeSpan.FromSeconds(300), // Docker builds can take a while
                TestContext.Current.CancellationToken
            );
            
            // Wait for prompt to return
            await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#"),
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            
            TestCaptureHelper.Capture(terminal, "globe-after-build");
            
            // Step 5: Run the globe container with interactive terminal and mouse support
            // -i for interactive mode, -c 1 for single color (more compatible)
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("docker run -it --rm globe -i -c 1\n"));
            
            // Wait for globe to start (look for globe-specific output)
            var globeStarted = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("●") ||
                      terminal.CreateSnapshot().ContainsText("○") ||
                      terminal.CreateSnapshot().ContainsText("*") ||
                      terminal.CreateSnapshot().ContainsText(".") &&
                      !terminal.CreateSnapshot().ContainsText("docker"),
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            
            // Give the globe a moment to render
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            
            TestCaptureHelper.Capture(terminal, "globe-initial");
            
            // Step 6: Send mouse drag gestures to rotate the globe
            // Enable mouse tracking in the terminal first
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("\x1b[?1000h\x1b[?1002h\x1b[?1003h\x1b[?1006h"));
            await Task.Delay(200, TestContext.Current.CancellationToken);
            
            // Perform several drag gestures across the globe (center of screen)
            var centerX = 60;
            var centerY = 20;
            
            for (int drag = 0; drag < 5; drag++)
            {
                // Mouse down at start position
                var startX = centerX - 10 + (drag * 5);
                var startY = centerY;
                await SendMouseEventAsync(process, MouseButton.Left, MouseAction.Down, startX, startY);
                await Task.Delay(50, TestContext.Current.CancellationToken);
                
                // Drag across the globe
                for (int step = 0; step < 10; step++)
                {
                    var currentX = startX + (step * 3);
                    await SendMouseEventAsync(process, MouseButton.Left, MouseAction.Drag, currentX, startY);
                    await Task.Delay(30, TestContext.Current.CancellationToken);
                }
                
                // Mouse up
                await SendMouseEventAsync(process, MouseButton.Left, MouseAction.Up, startX + 30, startY);
                await Task.Delay(200, TestContext.Current.CancellationToken);
                
                // Capture a frame of the animation
                TestCaptureHelper.Capture(terminal, $"globe-drag-{drag}");
            }
            
            // Step 7: Let the globe run for a while
            await Task.Delay(3000, TestContext.Current.CancellationToken);
            
            TestCaptureHelper.Capture(terminal, "globe-after-interaction");
            
            // Step 8: Send 'q' to quit the globe
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("q"));
            
            // Wait for the container to exit
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            
            // Wait for bash prompt to return
            await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            TestCaptureHelper.Capture(terminal, "globe-after-quit");
            
            // Exit bash
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"));
            
            // Wait for process to exit
            var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(exitCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
            }
            
            // Flush and capture the Asciinema recording
            await TestCaptureHelper.CaptureCastAsync(recorder, "globe", TestContext.Current.CancellationToken);
            
            // Assert success - we got through the whole workflow
            Assert.True(globeStarted || buildComplete, "Globe workflow should have progressed");
        }
        finally
        {
            // Cleanup temp directory
            try 
            { 
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true); 
            } 
            catch { }
            
            // Cleanup temp file
            try { File.Delete(castFile); } catch { }
            
            // Cleanup the docker image (don't fail if this fails)
            try 
            {
                await RunCommandAsync("docker", ["rmi", "-f", "globe"]);
            }
            catch { }
        }
    }
    
    /// <summary>
    /// Stress test that launches bash, starts tmux, splits the pane vertically,
    /// runs globe on one side and mapscii on the other.
    /// This exercises:
    /// - Tmux multiplexer integration
    /// - Multiple concurrent TUI applications
    /// - Vertical pane splitting
    /// - Complex terminal rendering with multiple viewports
    /// This test is primarily for visual validation and is not intended to be kept long-term.
    /// Requires: tmux, telnet (for mapscii), docker (for globe), and git.
    /// </summary>
    [Fact(Skip = "Stress test - run manually for visual validation")]
    [Trait("Category", "Unix")]
    [Trait("Category", "StressTest")]
    [Trait("Category", "Docker")]
    public async Task StressTest_TmuxSplitGlobeAndMapscii()
    {
        // Check required tools
        var tmuxPath = await RunCommandAsync("which", ["tmux"]);
        if (string.IsNullOrWhiteSpace(tmuxPath) || !File.Exists(tmuxPath.Trim()))
        {
            // Skip if tmux is not installed
            return;
        }
        
        var telnetPath = await RunCommandAsync("which", ["telnet"]);
        if (string.IsNullOrWhiteSpace(telnetPath) || !File.Exists(telnetPath.Trim()))
        {
            // Skip if telnet is not installed (needed for mapscii)
            return;
        }
        
        var dockerPath = await RunCommandAsync("which", ["docker"]);
        if (string.IsNullOrWhiteSpace(dockerPath) || !File.Exists(dockerPath.Trim()))
        {
            // Skip if docker is not installed (needed for globe)
            return;
        }
        
        // Create temp directory for the globe clone
        var tempDir = Path.Combine(Path.GetTempPath(), $"tmux_split_test_{Guid.NewGuid()}");
        var castFile = Path.Combine(Path.GetTempPath(), $"tmux_split_{Guid.NewGuid()}.cast");
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Launch interactive bash - tmux will be started inside
            await using var process = new Hex1bTerminalChildProcess(
                "/bin/bash",
                ["--norc", "--noprofile"],
                workingDirectory: tempDir,
                inheritEnvironment: true,
                initialWidth: 160, // Wide enough for split panes
                initialHeight: 50
            );
            
            await process.StartAsync();
            
            // Create terminal options with Asciinema recorder
            var options = new Hex1bTerminalOptions
            {
                Width = 160,
                Height = 50,
                WorkloadAdapter = process,
                PresentationAdapter = new CapturingTestPresentationAdapter(160, 50)
            };
            var recorder = new AsciinemaRecorder(castFile, new AsciinemaRecorderOptions
            {
                Title = "Tmux Split: Globe + Mapscii Stress Test",
                CaptureInput = true
            });
            options.WorkloadFilters.Add(recorder);
            
            // Create Hex1bTerminal
            using var terminal = new Hex1bTerminal(options);
            
            // Step 1: Wait for initial bash prompt
            var promptFound = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            if (!promptFound)
            {
                TestCaptureHelper.Capture(terminal, "tmux-split-prompt-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "tmux-split-prompt-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for initial bash prompt.");
            }
            
            TestCaptureHelper.Capture(terminal, "tmux-split-01-initial-prompt");
            
            // Step 2: Clone globe repo first (so it's ready when we need it)
            await process.WriteInputAsync(Encoding.UTF8.GetBytes($"git clone --depth 1 https://github.com/adamsky/globe {tempDir}/globe 2>&1\n"));
            
            var cloneComplete = await WaitForConditionAsync(
                () => Directory.Exists(Path.Combine(tempDir, "globe")) ||
                      terminal.CreateSnapshot().ContainsText("fatal:"),
                TimeSpan.FromSeconds(60),
                TestContext.Current.CancellationToken
            );
            
            // Wait for prompt to return
            await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            TestCaptureHelper.Capture(terminal, "tmux-split-02-after-clone");
            
            if (!Directory.Exists(Path.Combine(tempDir, "globe")))
            {
                await TestCaptureHelper.CaptureCastAsync(recorder, "tmux-split-clone-failed", TestContext.Current.CancellationToken);
                Assert.Fail("Failed to clone globe repository.");
            }
            
            // Step 3: Build the globe docker image
            await process.WriteInputAsync(Encoding.UTF8.GetBytes($"cd {tempDir}/globe && docker build -t globe . 2>&1\n"));
            
            var buildComplete = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("Successfully") ||
                      terminal.CreateSnapshot().ContainsText("naming to docker.io") ||
                      terminal.CreateSnapshot().ContainsText("ERROR") ||
                      terminal.CreateSnapshot().ContainsText("error:"),
                TimeSpan.FromSeconds(300),
                TestContext.Current.CancellationToken
            );
            
            // Wait for prompt
            await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#"),
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            
            TestCaptureHelper.Capture(terminal, "tmux-split-03-after-docker-build");
            
            // Step 4: Start tmux with a new session and attach in one command
            // Using -A flag to attach-or-create to avoid issues with detached sessions
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("tmux new-session -s split_test 2>&1\n"));
            
            // Wait for tmux to fully initialize
            await Task.Delay(3000, TestContext.Current.CancellationToken);
            
            // Capture tmux state
            TestCaptureHelper.Capture(terminal, "tmux-split-04-tmux-started");
            
            // Debug: Check what's in the buffer after tmux starts
            var tmuxSnapshot = terminal.CreateSnapshot();
            var fullText = tmuxSnapshot.GetScreenText();
            TestContext.Current.TestOutputHelper?.WriteLine($"After tmux start, screen content (first 500 chars): {fullText[..Math.Min(500, fullText.Length)]}");
            
            // Check if tmux is running (should see status bar or fresh prompt)
            var hasTmuxStatusBar = tmuxSnapshot.ContainsText("[split_test]") ||
                                    tmuxSnapshot.ContainsText("[0]") || 
                                    tmuxSnapshot.ContainsText("tmux");
            var hasOldContent = tmuxSnapshot.ContainsText("docker build") ||
                                tmuxSnapshot.ContainsText("git clone");
            
            TestContext.Current.TestOutputHelper?.WriteLine($"Has tmux status: {hasTmuxStatusBar}, Has old content: {hasOldContent}");
            
            // Step 5: Split pane vertically (creates left and right panes)
            // Ctrl+b % is the default key binding for vertical split
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B (tmux prefix)
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("%")); // Vertical split
            
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-split-05-after-split");
            
            // Step 6: In the right pane (current), start mapscii
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("telnet mapscii.me\n"));
            
            // Wait for mapscii to start loading
            var mapsciiStarting = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("Trying") ||
                      terminal.CreateSnapshot().ContainsText("Connected") ||
                      terminal.CreateSnapshot().ContainsText("center:"),
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            
            // Wait for the map to load (look for zoom level indicator)
            var mapsciiLoaded = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("center:"),
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-split-06-mapscii-loaded");
            
            // Step 7: Switch to left pane (Ctrl+b Left Arrow)
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B (tmux prefix)
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("\x1b[D")); // Left arrow
            
            await Task.Delay(500, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-split-07-switched-to-left");
            
            // Step 8: Run globe in the left pane
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("docker run -it --rm globe -i -c 1\n"));
            
            // Wait for globe to start (look for dots or circles)
            var globeStarted = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("●") ||
                      terminal.CreateSnapshot().ContainsText("○") ||
                      terminal.CreateSnapshot().ContainsText("*"),
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken
            );
            
            // Give everything time to render
            await Task.Delay(3000, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-split-08-both-running");
            
            // Step 9: Take multiple snapshots of the split view
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(2000, TestContext.Current.CancellationToken);
                TestCaptureHelper.Capture(terminal, $"tmux-split-09-frame-{i}");
            }
            
            // Step 10: Verify we have content from both applications
            var finalSnapshot = terminal.CreateSnapshot();
            var hasMapContent = finalSnapshot.ContainsText("center:") || 
                                finalSnapshot.ContainsText("zoom:");
            var hasGlobeContent = finalSnapshot.ContainsText("●") || 
                                  finalSnapshot.ContainsText("○") ||
                                  finalSnapshot.ContainsText("*") ||
                                  finalSnapshot.ContainsText(".");
            
            TestCaptureHelper.Capture(terminal, "tmux-split-10-final");
            
            // Step 11: Cleanup - quit globe first (Ctrl+C)
            await process.WriteInputAsync(new byte[] { 0x03 }); // Ctrl+C
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            
            // Switch to right pane to quit mapscii
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("\x1b[C")); // Right arrow
            await Task.Delay(500, TestContext.Current.CancellationToken);
            
            // Quit mapscii
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("q"));
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            
            TestCaptureHelper.Capture(terminal, "tmux-split-11-after-quit-apps");
            
            // Step 12: Kill tmux session
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes(":")); // Command mode
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("kill-session\n"));
            
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            
            // Exit bash
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"));
            
            // Wait for process to exit
            var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(exitCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
            }
            
            // Flush and capture the Asciinema recording
            await TestCaptureHelper.CaptureCastAsync(recorder, "tmux-split", TestContext.Current.CancellationToken);
            
            // Assert that we saw content from both apps
            Assert.True(mapsciiLoaded || hasMapContent, "Mapscii should have loaded in the right pane");
            Assert.True(globeStarted || hasGlobeContent, "Globe should have started in the left pane");
        }
        finally
        {
            // Cleanup temp directory
            try 
            { 
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true); 
            } 
            catch { }
            
            // Cleanup temp file
            try { File.Delete(castFile); } catch { }
            
            // Cleanup docker image
            try 
            {
                await RunCommandAsync("docker", ["rmi", "-f", "globe"]);
            }
            catch { }
            
            // Kill any orphaned tmux sessions
            try
            {
                await RunCommandAsync("tmux", ["kill-session", "-t", "split_test"]);
            }
            catch { }
        }
    }
    
    /// <summary>
    /// Sends a mouse event to the PTY process using SGR extended mouse format.
    /// </summary>
    private static async Task SendMouseEventAsync(
        Hex1bTerminalChildProcess process,
        MouseButton button,
        MouseAction action,
        int x,
        int y,
        Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        // Build the SGR mouse sequence: ESC [ < Cb ; Cx ; Cy M/m
        // Cb = button code, Cx = 1-based column, Cy = 1-based row
        // M = press/motion, m = release
        
        int buttonCode = button switch
        {
            MouseButton.Left => 0,
            MouseButton.Middle => 1,
            MouseButton.Right => 2,
            _ => 0
        };
        
        // Add motion flag for drag events
        if (action == MouseAction.Drag || action == MouseAction.Move)
            buttonCode |= 32;
        
        // Add modifier flags
        if (modifiers.HasFlag(Hex1bModifiers.Shift)) buttonCode |= 4;
        if (modifiers.HasFlag(Hex1bModifiers.Alt)) buttonCode |= 8;
        if (modifiers.HasFlag(Hex1bModifiers.Control)) buttonCode |= 16;
        
        // Terminator: M for down/drag, m for up
        var terminator = action == MouseAction.Up ? 'm' : 'M';
        
        // 1-based coordinates
        var sequence = $"\x1b[<{buttonCode};{x + 1};{y + 1}{terminator}";
        await process.WriteInputAsync(Encoding.UTF8.GetBytes(sequence));
    }
    
    /// <summary>
    /// Helper to run a command and capture its output.
    /// </summary>
    private static async Task<string> RunCommandAsync(string command, string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return "";
            
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output;
        }
        catch
        {
            return "";
        }
    }
    
    /// <summary>
    /// Helper to wait for a condition with timeout.
    /// </summary>
    private static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (condition())
                    return true;
                    
                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancelled
        }
        
        return condition(); // One final check
    }
    
    /// <summary>
    /// Diagnostic test that launches bash (with profile disabled), starts tmux,
    /// splits the screen with CTRL-B %, and captures the terminal state for debugging.
    /// This test is designed to capture the internal terminal state after a tmux split
    /// to help diagnose rendering glitches.
    /// </summary>
    [Fact(Skip = "Diagnostic test - spawns PTY processes, slow, not suitable for CI")]
    [Trait("Category", "Unix")]
    [Trait("Category", "StressTest")]
    [Trait("Category", "Diagnostic")]
    public async Task Diagnostic_TmuxSplitScreen_CapturesTerminalState()
    {
        // Skip on non-Linux platforms - PTY support requires Linux
        if (!OperatingSystem.IsLinux())
            return;
        
        // Check if tmux is available
        var tmuxPath = await RunCommandAsync("which", ["tmux"]);
        if (string.IsNullOrWhiteSpace(tmuxPath) || !File.Exists(tmuxPath.Trim()))
        {
            // Skip if tmux is not installed
            return;
        }
        
        // Setup temp file for asciinema recording
        var castFile = Path.Combine(Path.GetTempPath(), $"tmux_split_diag_{Guid.NewGuid()}.cast");
        
        try
        {
            // Launch interactive bash without user profile for predictable prompt
            await using var process = new Hex1bTerminalChildProcess(
                "/bin/bash",
                ["--norc", "--noprofile"],
                inheritEnvironment: true,
                initialWidth: 120,
                initialHeight: 40
            );
            
            await process.StartAsync();
            
            // Create terminal options with Asciinema recorder
            var options = new Hex1bTerminalOptions
            {
                Width = 120,
                Height = 40,
                WorkloadAdapter = process,
                PresentationAdapter = new CapturingTestPresentationAdapter(120, 40)
            };
            var recorder = new AsciinemaRecorder(castFile, new AsciinemaRecorderOptions
            {
                Title = "Tmux Split Screen Diagnostic",
                CaptureInput = true
            });
            options.WorkloadFilters.Add(recorder);
            
            // Create Hex1bTerminal - the presence of PresentationAdapter auto-starts the pump
            using var terminal = new Hex1bTerminal(options);
            
            // Step 1: Wait for initial bash prompt
            var promptFound = await WaitForConditionAsync(
                () => terminal.CreateSnapshot().ContainsText("$") || 
                      terminal.CreateSnapshot().ContainsText("#") ||
                      terminal.CreateSnapshot().ContainsText(">"),
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            
            if (!promptFound)
            {
                TestCaptureHelper.Capture(terminal, "tmux-diag-01-prompt-timeout");
                await TestCaptureHelper.CaptureCastAsync(recorder, "tmux-diag-prompt-timeout", TestContext.Current.CancellationToken);
                Assert.Fail("Timed out waiting for initial bash prompt.");
            }
            
            TestCaptureHelper.Capture(terminal, "tmux-diag-01-initial-prompt");
            
            // Step 2: Start tmux with a new session
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("tmux new-session -s diag_test\n"));
            
            // Wait for tmux to fully initialize - look for the status bar or a fresh prompt
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            
            // Capture tmux state after launch
            TestCaptureHelper.Capture(terminal, "tmux-diag-02-tmux-started");
            
            // Debug output
            var tmuxSnapshot = terminal.CreateSnapshot();
            var fullText = tmuxSnapshot.GetScreenText();
            TestContext.Current.TestOutputHelper?.WriteLine($"After tmux start, screen content (first 500 chars): {fullText[..Math.Min(500, fullText.Length)]}");
            
            // Step 3: Split pane vertically with Ctrl+B %
            // Ctrl+B is the tmux prefix key (ASCII 0x02)
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B (tmux prefix)
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("%")); // Vertical split
            
            // Wait for the split to render
            await Task.Delay(1500, TestContext.Current.CancellationToken);
            
            // Capture the split state
            TestCaptureHelper.Capture(terminal, "tmux-diag-03-after-split");
            
            // Debug output after split
            var splitSnapshot = terminal.CreateSnapshot();
            var splitText = splitSnapshot.GetScreenText();
            TestContext.Current.TestOutputHelper?.WriteLine($"After split, screen content (first 500 chars): {splitText[..Math.Min(500, splitText.Length)]}");
            
            // Dump raw screen buffer info for debugging
            var buffer = terminal.GetScreenBuffer();
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine($"Screen buffer dimensions: {buffer.GetLength(1)}x{buffer.GetLength(0)} (WxH)");
            
            // Check for vertical split line (tmux typically uses │ or | characters)
            int splitLineCol = -1;
            for (int x = 0; x < buffer.GetLength(1); x++)
            {
                var ch = buffer[0, x].Character;
                if (ch == "│" || ch == "|" || ch == "┃")
                {
                    splitLineCol = x;
                    break;
                }
            }
            diagnostics.AppendLine($"Split line detected at column: {splitLineCol}");
            
            // Sample some cells around the middle of the screen
            int midY = buffer.GetLength(0) / 2;
            int midX = buffer.GetLength(1) / 2;
            diagnostics.AppendLine($"Sample cells around center ({midX}, {midY}):");
            for (int dx = -5; dx <= 5; dx++)
            {
                int x = Math.Clamp(midX + dx, 0, buffer.GetLength(1) - 1);
                var cell = buffer[midY, x];
                diagnostics.AppendLine($"  [{midY},{x}]: '{cell.Character}' Fg={cell.Foreground} Bg={cell.Background} Seq={cell.Sequence}");
            }
            
            TestContext.Current.TestOutputHelper?.WriteLine(diagnostics.ToString());
            
            // Step 4: Take additional snapshots after waiting
            await Task.Delay(500, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-diag-04-stable");
            
            // Step 5: Send some text to the right pane (which is now focused)
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("echo 'Right pane'\n"));
            await Task.Delay(500, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-diag-05-after-echo-right");
            
            // Step 6: Switch to left pane and send text
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("\x1b[D")); // Left arrow
            await Task.Delay(500, TestContext.Current.CancellationToken);
            
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("echo 'Left pane'\n"));
            await Task.Delay(500, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-diag-06-after-echo-left");
            
            // Final snapshot after some activity
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            TestCaptureHelper.Capture(terminal, "tmux-diag-07-final");
            
            // Cleanup: Kill tmux session
            await process.WriteInputAsync(new byte[] { 0x02 }); // Ctrl+B
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes(":")); // Command mode
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("kill-session\n"));
            
            await Task.Delay(500, TestContext.Current.CancellationToken);
            
            // Exit bash
            await process.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"));
            
            // Wait for process to exit
            var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(exitCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
            }
            
            // Flush and capture the Asciinema recording
            await TestCaptureHelper.CaptureCastAsync(recorder, "tmux-diag", TestContext.Current.CancellationToken);
            
            // This test is primarily for diagnostic capture, not assertions
            // If we got this far, the capture was successful
            Assert.True(true, "Diagnostic capture completed successfully");
        }
        finally
        {
            // Cleanup temp file
            try { File.Delete(castFile); } catch { }
            
            // Kill any orphaned tmux sessions
            try
            {
                await RunCommandAsync("tmux", ["kill-session", "-t", "diag_test"]);
            }
            catch { }
        }
    }
}
