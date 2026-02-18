using System.Text;

namespace Hex1b.Tests;

/// <summary>
/// Exploratory tests to debug the nano rendering issue in embedded terminals.
/// </summary>
/// <remarks>
/// <para>
/// When nano runs inside an embedded TerminalWidget, typing characters appears
/// to overwrite the nano header bar instead of appearing in the edit area.
/// </para>
/// <para>
/// These tests help diagnose whether:
/// 1. The TerminalWidgetHandle's internal buffer state is correct
/// 2. Or if it's a rendering issue in TerminalNode
/// </para>
/// </remarks>
public class NanoExploratoryTests
{
    /// <summary>
    /// Test context that creates a real PTY bash session with TerminalWidgetHandle.
    /// </summary>
    private sealed class PtyTerminalTestContext : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private Task? _runTask;
        
        public Hex1bTerminal Terminal { get; }
        public TerminalWidgetHandle Handle { get; }
        
        private PtyTerminalTestContext(Hex1bTerminal terminal, TerminalWidgetHandle handle)
        {
            Terminal = terminal;
            Handle = handle;
        }
        
        /// <summary>
        /// Creates a test context with a real bash PTY process.
        /// </summary>
        public static PtyTerminalTestContext Create(int width = 80, int height = 24)
        {
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(width, height)
                .WithPtyProcess("bash", "--norc", "--noprofile")
                .WithTerminalWidget(out var handle)
                .Build();
            
            return new PtyTerminalTestContext(terminal, handle);
        }
        
        /// <summary>
        /// Starts the terminal in the background.
        /// </summary>
        public void Start()
        {
            _runTask = Task.Run(async () =>
            {
                try
                {
                    await Terminal.RunAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });
        }
        
        /// <summary>
        /// Sends text to the terminal as if typed.
        /// </summary>
        public async Task TypeAsync(string text, int delayBetweenCharsMs = 10)
        {
            foreach (var c in text)
            {
                await Handle.SendEventAsync(new Input.Hex1bKeyEvent(Input.Hex1bKey.None, c, Input.Hex1bModifiers.None));
                if (delayBetweenCharsMs > 0)
                {
                    await Task.Delay(delayBetweenCharsMs);
                }
            }
        }
        
        /// <summary>
        /// Sends a special key to the terminal.
        /// </summary>
        public async Task SendKeyAsync(Input.Hex1bKey key, Input.Hex1bModifiers modifiers = Input.Hex1bModifiers.None)
        {
            await Handle.SendEventAsync(new Input.Hex1bKeyEvent(key, '\0', modifiers));
        }
        
        /// <summary>
        /// Gets the current screen buffer as a string representation.
        /// </summary>
        public string GetScreenText()
        {
            var (buffer, width, height) = Handle.GetScreenBufferSnapshot();
            var sb = new StringBuilder();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(buffer[y, x].Character);
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Gets a detailed dump of the screen buffer including cursor position.
        /// </summary>
        public string GetDetailedDump()
        {
            var (buffer, width, height) = Handle.GetScreenBufferSnapshot();
            var sb = new StringBuilder();
            
            sb.AppendLine($"=== Screen Buffer Dump ===");
            sb.AppendLine($"Size: {width}x{height}");
            sb.AppendLine($"Cursor: ({Handle.CursorX}, {Handle.CursorY})");
            sb.AppendLine($"Cursor Visible: {Handle.CursorVisible}");
            sb.AppendLine();
            
            // Add line numbers and column ruler
            sb.Append("     ");
            for (int x = 0; x < Math.Min(width, 80); x++)
            {
                sb.Append(x % 10);
            }
            sb.AppendLine();
            sb.Append("     ");
            for (int x = 0; x < Math.Min(width, 80); x++)
            {
                sb.Append(x % 10 == 0 ? '|' : ' ');
            }
            sb.AppendLine();
            
            for (int y = 0; y < height; y++)
            {
                sb.Append($"{y,3}: ");
                for (int x = 0; x < Math.Min(width, 80); x++)
                {
                    var cell = buffer[y, x];
                    // Show cursor position with a marker
                    if (y == Handle.CursorY && x == Handle.CursorX)
                    {
                        sb.Append('█'); // Cursor marker
                    }
                    else
                    {
                        sb.Append(cell.Character);
                    }
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Waits for the terminal to output text containing the specified string.
        /// </summary>
        public async Task WaitForTextAsync(string text, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var screen = GetScreenText();
                if (screen.Contains(text))
                {
                    return;
                }
                await Task.Delay(50);
            }
            
            throw new TimeoutException($"Timeout waiting for text: {text}\n\nActual screen:\n{GetScreenText()}");
        }
        
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            if (_runTask != null)
            {
                try
                {
                    await _runTask.WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch
                {
                    // Ignore timeout/cancellation
                }
            }
            await Terminal.DisposeAsync();
            _cts.Dispose();
        }
    }
    
    /// <summary>
    /// Exploratory test: Launch nano and observe initial screen state.
    /// </summary>
    /// <remarks>
    /// This test helps us understand what the buffer looks like after nano starts.
    /// Expected: Nano header at top, empty edit area, help at bottom.
    /// </remarks>
    [Fact(Skip = "Exploratory test - run manually")]
    public async Task Nano_InitialScreen_ShowsCorrectLayout()
    {
        // Arrange
        await using var ctx = PtyTerminalTestContext.Create(80, 24);
        ctx.Start();
        
        // Wait for bash prompt
        await Task.Delay(500);
        
        // Act - Launch nano with a temp file
        await ctx.TypeAsync("nano /tmp/test.txt\n");
        
        // Wait for nano to start
        await Task.Delay(1000);
        
        // Dump the screen state
        var dump = ctx.GetDetailedDump();
        
        // Output for debugging
        Console.WriteLine(dump);
        
        // Assert - Check for nano UI elements
        var screen = ctx.GetScreenText();
        
        // Nano should show "GNU nano" in the header
        Assert.Contains("nano", screen.ToLowerInvariant());
        
        // Should have the help hints at bottom (^G Get Help, etc.)
        Assert.Contains("^G", screen);
    }
    
    /// <summary>
    /// Exploratory test: Type text in nano and observe where it appears.
    /// </summary>
    /// <remarks>
    /// This is the key test - we type "hello" and see if it appears in the correct location.
    /// The bug is that typed text appears to overwrite the header bar.
    /// </remarks>
    [Fact(Skip = "Exploratory test - run manually")]
    public async Task Nano_TypeText_AppearsInEditArea()
    {
        // Arrange
        await using var ctx = PtyTerminalTestContext.Create(80, 24);
        ctx.Start();
        
        // Wait for bash prompt
        await Task.Delay(500);
        
        // Launch nano
        await ctx.TypeAsync("nano /tmp/test.txt\n");
        await Task.Delay(1000);
        
        // Dump initial state
        Console.WriteLine("=== BEFORE TYPING ===");
        Console.WriteLine(ctx.GetDetailedDump());
        
        // Act - Type some text
        await ctx.TypeAsync("hello world");
        await Task.Delay(500);
        
        // Dump after typing
        Console.WriteLine("=== AFTER TYPING ===");
        Console.WriteLine(ctx.GetDetailedDump());
        
        // Assert - The typed text should be in the edit area (row 2+), not the header (row 0-1)
        var (buffer, width, height) = ctx.Handle.GetScreenBufferSnapshot();
        
        // Extract row 0 (header) and row 2 (first edit line)
        var row0 = new StringBuilder();
        var row2 = new StringBuilder();
        for (int x = 0; x < width; x++)
        {
            row0.Append(buffer[0, x].Character);
            if (height > 2)
            {
                row2.Append(buffer[2, x].Character);
            }
        }
        
        Console.WriteLine($"Row 0 (header): '{row0}'");
        Console.WriteLine($"Row 2 (edit):   '{row2}'");
        Console.WriteLine($"Cursor position: ({ctx.Handle.CursorX}, {ctx.Handle.CursorY})");
        
        // The header should still contain "nano" or "GNU nano"
        Assert.Contains("nano", row0.ToString().ToLowerInvariant());
        
        // The typed text should be in the edit area, NOT the header
        Assert.DoesNotContain("hello", row0.ToString().ToLowerInvariant());
        Assert.Contains("hello", row2.ToString().ToLowerInvariant());
    }
    
    /// <summary>
    /// Exploratory test: Test origin mode behavior in isolation.
    /// Origin mode works correctly - row 1 in origin mode maps to scroll_top.
    /// </summary>
    [Fact]
    public void OriginMode_PositionsRelativeToScrollRegion()
    {
        // Create terminal
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        
        // Set scroll region rows 3-22 (0-based: 2-21), enable origin mode, position cursor at 1,1
        var sequence = 
            "\x1b[H\x1b[2J" +  // Clear screen and home cursor
            "\x1b[1;1HTitle at row 0" +  // Write title at absolute row 0 (before origin mode)
            "\x1b[3;22r" +   // Set scroll region rows 3-22 (0-based: 2-21)
            "\x1b[?6h" +     // Enable origin mode
            "\x1b[1;1HEdit area text";  // Write at origin mode row 1 -> should be row 2 (0-based)
        
        terminal.ApplyTokens(Tokens.AnsiTokenizer.Tokenize(sequence));
        
        var buffer = terminal.GetScreenBuffer();
        var row0 = GetRowText(buffer, 0);
        var row2 = GetRowText(buffer, 2);
        
        Assert.Equal("Title at row 0", row0);
        Assert.Contains("Edit area text", row2);
        
        static string GetRowText(TerminalCell[,] buffer, int row)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < buffer.GetLength(1); x++)
            {
                var c = buffer[row, x].Character;
                sb.Append(string.IsNullOrEmpty(c) ? ' ' : c[0]);
            }
            return sb.ToString().TrimEnd();
        }
    }
    
    /// <summary>
    /// PTY test: Use printf to test escape sequence handling through the PTY pipeline.
    /// This tests if the full PTY -> Hex1bTerminal -> TerminalWidgetHandle pipeline works.
    /// </summary>
    [Fact]
    public async Task Pty_Printf_RendersCorrectly()
    {
        // Arrange
        await using var ctx = PtyTerminalTestContext.Create(80, 24);
        ctx.Start();
        
        await Task.Delay(500);
        
        // Use printf to write text at specific positions
        // Position cursor at row 5, column 1, write "Hello"
        await ctx.TypeAsync("printf '\\e[5;1HHello at row 5'\n");
        await Task.Delay(500);
        
        // Get the buffer
        var (buffer, width, height) = ctx.Handle.GetScreenBufferSnapshot();
        
        // Build output for all non-empty rows
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== BUFFER STATE ===");
        for (int y = 0; y < height; y++)
        {
            var rowContent = new System.Text.StringBuilder();
            for (int x = 0; x < width; x++)
            {
                var c = buffer[y, x].Character;
                rowContent.Append(string.IsNullOrEmpty(c) ? ' ' : c[0]);
            }
            var trimmed = rowContent.ToString().TrimEnd();
            if (!string.IsNullOrEmpty(trimmed))
            {
                sb.AppendLine($"Row {y,2}: [{trimmed}]");
            }
        }
        
        // Verify "Hello at row 5" appears at row 4 (0-based)
        var row4Text = GetRowText(buffer, 4);
        
        // Throw diagnostics if assertion fails
        Assert.True(row4Text.Contains("Hello at row 5"), 
            $"Expected row 4 to contain 'Hello at row 5', but got:\n{sb}");
        
        static string GetRowText(TerminalCell[,] buffer, int row)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < buffer.GetLength(1); x++)
            {
                var c = buffer[row, x].Character;
                sb.Append(string.IsNullOrEmpty(c) ? ' ' : c[0]);
            }
            return sb.ToString().TrimEnd();
        }
    }
    
    /// <summary>
    /// PTY test: Nano buffer content diagnostic.
    /// </summary>
    [Fact]
    public async Task Nano_BufferContent_Diagnostic()
    {
        // Arrange
        await using var ctx = PtyTerminalTestContext.Create(80, 24);
        ctx.Start();
        
        await Task.Delay(500);
        
        // Launch nano
        await ctx.TypeAsync("nano /tmp/nanotest.txt\n");
        await Task.Delay(2000);
        
        // Get both the terminal's internal buffer and the handle's buffer
        var terminalBuffer = ctx.Terminal.GetScreenBuffer();
        var (handleBuffer, _, height) = ctx.Handle.GetScreenBufferSnapshot();
        
        // Build diagnostic output
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== TERMINAL INTERNAL BUFFER ===");
        for (int y = 0; y < height; y++)
        {
            var rowContent = new System.Text.StringBuilder();
            for (int x = 0; x < 70; x++)
            {
                var c = terminalBuffer[y, x].Character;
                rowContent.Append(string.IsNullOrEmpty(c) ? ' ' : c[0]);
            }
            var trimmed = rowContent.ToString().TrimEnd();
            if (!string.IsNullOrEmpty(trimmed) || y < 3 || y >= height - 3)
            {
                sb.AppendLine($"Row {y,2}: [{trimmed}]");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("=== HANDLE BUFFER ===");
        for (int y = 0; y < height; y++)
        {
            var rowContent = new System.Text.StringBuilder();
            for (int x = 0; x < 70; x++)
            {
                var c = handleBuffer[y, x].Character;
                rowContent.Append(string.IsNullOrEmpty(c) ? ' ' : c[0]);
            }
            var trimmed = rowContent.ToString().TrimEnd();
            if (!string.IsNullOrEmpty(trimmed) || y < 3 || y >= height - 3)
            {
                sb.AppendLine($"Row {y,2}: [{trimmed}]");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine($"Cursor: ({ctx.Handle.CursorX}, {ctx.Handle.CursorY})");
        
        // Nano should have the title at row 0 and help at the bottom
        var row0 = GetRowText(terminalBuffer, 0);
        var row22 = GetRowText(terminalBuffer, 22);
        var row23 = GetRowText(terminalBuffer, 23);
        
        sb.AppendLine();
        sb.AppendLine($"Row 0: '{row0}'");
        sb.AppendLine($"Row 22: '{row22}'");
        sb.AppendLine($"Row 23: '{row23}'");
        
        // Verify nano's UI is in the correct positions
        // Title should be at row 0 and should contain "nano" or "GNU"
        Assert.True(
            row0.Contains("nano", StringComparison.OrdinalIgnoreCase) || 
            row0.Contains("GNU", StringComparison.OrdinalIgnoreCase),
            $"Expected row 0 to contain nano title, but got:\n{sb}");
        
        static string GetRowText(TerminalCell[,] buffer, int row)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < buffer.GetLength(1); x++)
            {
                var c = buffer[row, x].Character;
                sb.Append(string.IsNullOrEmpty(c) ? ' ' : c[0]);
            }
            return sb.ToString().TrimEnd();
        }
    }
    
    /// <summary>
    /// Exploratory test: Compare DECSTBM (scroll region) handling.
    /// </summary>
    /// <remarks>
    /// Nano uses scroll regions (DECSTBM) to limit scrolling to the edit area.
    /// If scroll regions aren't handled correctly, content might render in wrong places.
    /// </remarks>
    [Fact(Skip = "Exploratory test - run manually")]
    public async Task Nano_ScrollRegion_IsRespected()
    {
        // Arrange
        await using var ctx = PtyTerminalTestContext.Create(80, 24);
        ctx.Start();
        
        await Task.Delay(500);
        
        // Launch nano
        await ctx.TypeAsync("nano /tmp/test.txt\n");
        await Task.Delay(1000);
        
        // Type enough lines to cause scrolling
        for (int i = 0; i < 25; i++)
        {
            await ctx.TypeAsync($"Line {i}\n", delayBetweenCharsMs: 5);
        }
        await Task.Delay(500);
        
        Console.WriteLine("=== AFTER TYPING MANY LINES ===");
        Console.WriteLine(ctx.GetDetailedDump());
        
        // The header should still be intact
        var (buffer, width, height) = ctx.Handle.GetScreenBufferSnapshot();
        var row0 = new StringBuilder();
        for (int x = 0; x < width; x++)
        {
            row0.Append(buffer[0, x].Character);
        }
        
        Assert.Contains("nano", row0.ToString().ToLowerInvariant());
    }
    
    /// <summary>
    /// Quick test to verify bash works correctly (baseline).
    /// </summary>
    [Fact(Skip = "Exploratory test - run manually")]
    public async Task Bash_Echo_WorksCorrectly()
    {
        // Arrange
        await using var ctx = PtyTerminalTestContext.Create(80, 24);
        ctx.Start();
        
        await Task.Delay(500);
        
        // Act
        await ctx.TypeAsync("echo 'Hello World'\n");
        await Task.Delay(500);
        
        // Assert
        var screen = ctx.GetScreenText();
        Console.WriteLine(ctx.GetDetailedDump());
        
        Assert.Contains("Hello World", screen);
    }
}
