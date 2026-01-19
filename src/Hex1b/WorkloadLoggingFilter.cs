using System.Text;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// A workload filter that logs all data flowing through the workload pipeline to a file.
/// </summary>
/// <remarks>
/// <para>
/// This filter is useful for debugging terminal issues by capturing:
/// <list type="bullet">
///   <item>Output FROM the workload with timestamps</item>
///   <item>Input TO the workload with timestamps</item>
///   <item>Resize events</item>
///   <item>Frame completion signals</item>
/// </list>
/// </para>
/// <para>
/// The log format includes elapsed time since session start and a hex dump of
/// the raw bytes for detailed analysis.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithWorkloadLogging("/tmp/terminal.log")
///     .WithProcess("bash")
///     .Build();
/// </code>
/// </example>
public sealed class WorkloadLoggingFilter : IHex1bTerminalWorkloadFilter, IAsyncDisposable, IDisposable
{
    private readonly string _filePath;
    private readonly bool _includeHexDump;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private bool _disposed;

    /// <summary>
    /// Creates a new workload logging filter.
    /// </summary>
    /// <param name="filePath">Path to the log file.</param>
    /// <param name="includeHexDump">Whether to include hex dumps of raw data.</param>
    public WorkloadLoggingFilter(string filePath, bool includeHexDump = true)
    {
        _filePath = filePath;
        _includeHexDump = includeHexDump;
    }

    private void EnsureWriter()
    {
        if (_writer != null) return;
        
        lock (_lock)
        {
            if (_writer != null) return;
            
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            _writer = new StreamWriter(_filePath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }
    }

    private void Log(string message)
    {
        if (_disposed) return;
        
        EnsureWriter();
        
        lock (_lock)
        {
            _writer?.WriteLine(message);
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return $"[{elapsed.TotalSeconds:F3}s]";
    }

    private string FormatTokens(IReadOnlyList<AnsiToken> tokens)
    {
        var sb = new StringBuilder();
        sb.Append($"{tokens.Count} token(s): ");
        
        foreach (var token in tokens)
        {
            sb.Append(FormatToken(token));
            sb.Append(' ');
        }
        
        if (_includeHexDump)
        {
            var serialized = AnsiTokenSerializer.Serialize(tokens);
            var bytes = Encoding.UTF8.GetBytes(serialized);
            sb.AppendLine();
            sb.Append("    Hex: ");
            sb.Append(FormatHex(bytes));
            sb.AppendLine();
            sb.Append("    Text: ");
            sb.Append(FormatPrintable(bytes));
        }
        
        return sb.ToString();
    }

    private static string FormatToken(AnsiToken token)
    {
        return token switch
        {
            TextToken t => $"Text({Escape(t.Text.Length > 20 ? t.Text[..20] + "..." : t.Text)})",
            CursorPositionToken c => $"Cursor({c.Row},{c.Column})",
            CursorMoveToken m => $"Move({m.Direction},{m.Count})",
            SgrToken s => $"SGR({s.Parameters})",
            ClearScreenToken cs => $"Clear({cs.Mode})",
            ClearLineToken cl => $"ClearLine({cl.Mode})",
            PrivateModeToken pm => $"Mode({pm.Mode},{(pm.Enable ? "on" : "off")})",
            OscToken o => $"OSC({o.Command})",
            SpecialKeyToken sk => $"SpecialKey({sk.KeyCode})",
            ControlCharacterToken cc => $"Ctrl(0x{(int)cc.Character:X2})",
            FrameBeginToken => "FrameBegin",
            FrameEndToken => "FrameEnd",
            _ => token.GetType().Name
        };
    }

    private static string Escape(string s)
    {
        return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\x1b", "\\e");
    }

    private static string FormatHex(byte[] bytes)
    {
        var sb = new StringBuilder();
        var limit = Math.Min(bytes.Length, 64);
        
        for (int i = 0; i < limit; i++)
        {
            sb.Append($"{bytes[i]:X2} ");
        }
        
        if (bytes.Length > limit)
        {
            sb.Append($"... ({bytes.Length} bytes total)");
        }
        
        return sb.ToString();
    }

    private static string FormatPrintable(byte[] bytes)
    {
        var sb = new StringBuilder();
        var limit = Math.Min(bytes.Length, 64);
        
        for (int i = 0; i < limit; i++)
        {
            var b = bytes[i];
            if (b >= 0x20 && b < 0x7F)
                sb.Append((char)b);
            else if (b == 0x1B)
                sb.Append("␛");
            else if (b == 0x0A)
                sb.Append("␊");
            else if (b == 0x0D)
                sb.Append("␍");
            else
                sb.Append('·');
        }
        
        if (bytes.Length > limit)
        {
            sb.Append("...");
        }
        
        return sb.ToString();
    }

    /// <inheritdoc />
    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        Log($"=== SESSION START === {timestamp:O} size={width}x{height}");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
    {
        Log($"{FormatElapsed(elapsed)} OUTPUT: {FormatTokens(tokens)}");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        Log($"{FormatElapsed(elapsed)} FRAME_COMPLETE");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
    {
        Log($"{FormatElapsed(elapsed)} INPUT: {FormatTokens(tokens)}");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
    {
        Log($"{FormatElapsed(elapsed)} RESIZE: {width}x{height}");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        Log($"{FormatElapsed(elapsed)} === SESSION END ===");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        StreamWriter? writer;
        lock (_lock)
        {
            writer = _writer;
            _writer = null;
        }
        
        if (writer != null)
        {
            await writer.DisposeAsync();
        }
    }
}
