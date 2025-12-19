using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Channels;
using Hex1b;
using Hex1b.Input;

namespace BrowserWasm;

/// <summary>
/// Browser-based terminal implementation that bridges xterm.js to Hex1b.
/// All JS interop is encapsulated here - the main app code doesn't need to know about it.
/// 
/// This terminal works with the standard Hex1bApp.RunAsync() - the async/await model
/// allows the browser's event loop to pump JS events while .NET awaits on the channel.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed partial class BrowserTerminal : IHex1bTerminal
{
    private static BrowserTerminal? _instance;
    
    private readonly Channel<Hex1bEvent> _inputChannel = Channel.CreateUnbounded<Hex1bEvent>();
    
    public int Width { get; private set; } = 80;
    public int Height { get; private set; } = 24;
    
    /// <summary>
    /// Input events channel - consumed by Hex1bApp.RunAsync().
    /// </summary>
    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;
    
    public BrowserTerminal()
    {
        _instance = this;
    }
    
    // ─────────────────────────────────────────────────────────────────
    // IHex1bTerminalOutput implementation
    // ─────────────────────────────────────────────────────────────────
    
    public void Write(string text)
    {
        // Write directly to xterm.js via JS interop
        JsWriteTerminal(text);
    }
    
    public void Clear() => Write("\x1b[2J\x1b[H");
    
    public void SetCursorPosition(int left, int top) => Write($"\x1b[{top + 1};{left + 1}H");
    
    public void EnterAlternateScreen() => Write("\x1b[?1049h\x1b[?25l");
    
    public void ExitAlternateScreen() => Write("\x1b[?25h\x1b[?1049l");
    
    // ─────────────────────────────────────────────────────────────────
    // JS Interop - Called FROM JavaScript (static entry points)
    // ─────────────────────────────────────────────────────────────────
    
    /// <summary>
    /// Called from JavaScript when the terminal is resized.
    /// </summary>
    [JSExport]
    public static void OnResize(int cols, int rows)
    {
        if (_instance == null) return;
        
        _instance.Width = cols;
        _instance.Height = rows;
        
        // Queue a resize event for Hex1bApp to process
        _instance._inputChannel.Writer.TryWrite(new Hex1bResizeEvent(cols, rows));
    }
    
    /// <summary>
    /// Called from JavaScript when keyboard input is received from xterm.js.
    /// Parses the input and enqueues key events for Hex1bApp to process.
    /// </summary>
    [JSExport]
    public static void OnInput(string data)
    {
        if (_instance == null) return;
        
        var events = ParseInput(data);
        foreach (var evt in events)
        {
            _instance._inputChannel.Writer.TryWrite(evt);
        }
    }
    
    // ─────────────────────────────────────────────────────────────────
    // JS Interop - Called TO JavaScript  
    // ─────────────────────────────────────────────────────────────────
    
    [JSImport("writeTerminal", "main.js")]
    private static partial void JsWriteTerminal(string text);
    
    // ─────────────────────────────────────────────────────────────────
    // Input parsing - converts xterm.js sequences to Hex1bKeyEvents
    // ─────────────────────────────────────────────────────────────────
    
    private static List<Hex1bKeyEvent> ParseInput(string data)
    {
        var events = new List<Hex1bKeyEvent>();
        var i = 0;
        
        while (i < data.Length)
        {
            // Check for escape sequences
            if (data[i] == '\x1b' && i + 1 < data.Length)
            {
                var (evt, consumed) = ParseEscapeSequence(data, i);
                if (evt != null)
                {
                    events.Add(evt);
                    i += consumed;
                    continue;
                }
            }
            
            // Regular character
            var c = data[i];
            var key = CharToKey(c);
            var mods = char.IsUpper(c) ? Hex1bModifiers.Shift : Hex1bModifiers.None;
            events.Add(new Hex1bKeyEvent(key, c.ToString(), mods));
            i++;
        }
        
        return events;
    }
    
    private static (Hex1bKeyEvent? evt, int consumed) ParseEscapeSequence(string data, int start)
    {
        if (start + 1 >= data.Length) return (null, 1);
        
        // CSI sequences: ESC [
        if (data[start + 1] == '[')
        {
            if (start + 2 >= data.Length) return (null, 2);
            
            var seq = data.Length > start + 2 ? data[(start + 2)..] : "";
            
            // Arrow keys and other CSI sequences
            return seq switch
            {
                _ when seq.StartsWith("A") => (new Hex1bKeyEvent(Hex1bKey.UpArrow, "", Hex1bModifiers.None), 3),
                _ when seq.StartsWith("B") => (new Hex1bKeyEvent(Hex1bKey.DownArrow, "", Hex1bModifiers.None), 3),
                _ when seq.StartsWith("C") => (new Hex1bKeyEvent(Hex1bKey.RightArrow, "", Hex1bModifiers.None), 3),
                _ when seq.StartsWith("D") => (new Hex1bKeyEvent(Hex1bKey.LeftArrow, "", Hex1bModifiers.None), 3),
                _ when seq.StartsWith("H") => (new Hex1bKeyEvent(Hex1bKey.Home, "", Hex1bModifiers.None), 3),
                _ when seq.StartsWith("F") => (new Hex1bKeyEvent(Hex1bKey.End, "", Hex1bModifiers.None), 3),
                _ when seq.StartsWith("Z") => (new Hex1bKeyEvent(Hex1bKey.Tab, "\t", Hex1bModifiers.Shift), 3), // Shift+Tab
                _ when seq.StartsWith("1;5A") => (new Hex1bKeyEvent(Hex1bKey.UpArrow, "", Hex1bModifiers.Control), 5),
                _ when seq.StartsWith("1;5B") => (new Hex1bKeyEvent(Hex1bKey.DownArrow, "", Hex1bModifiers.Control), 5),
                _ when seq.StartsWith("1;5C") => (new Hex1bKeyEvent(Hex1bKey.RightArrow, "", Hex1bModifiers.Control), 5),
                _ when seq.StartsWith("1;5D") => (new Hex1bKeyEvent(Hex1bKey.LeftArrow, "", Hex1bModifiers.Control), 5),
                _ when seq.StartsWith("3~") => (new Hex1bKeyEvent(Hex1bKey.Delete, "", Hex1bModifiers.None), 4),
                _ when seq.StartsWith("5~") => (new Hex1bKeyEvent(Hex1bKey.PageUp, "", Hex1bModifiers.None), 4),
                _ when seq.StartsWith("6~") => (new Hex1bKeyEvent(Hex1bKey.PageDown, "", Hex1bModifiers.None), 4),
                _ => (new Hex1bKeyEvent(Hex1bKey.Escape, "\x1b", Hex1bModifiers.None), 1)
            };
        }
        
        // Alt+key: ESC followed by character
        if (start + 1 < data.Length && data[start + 1] != '[')
        {
            var c = data[start + 1];
            var key = CharToKey(c);
            return (new Hex1bKeyEvent(key, c.ToString(), Hex1bModifiers.Alt), 2);
        }
        
        return (new Hex1bKeyEvent(Hex1bKey.Escape, "\x1b", Hex1bModifiers.None), 1);
    }
    
    private static Hex1bKey CharToKey(char c) => c switch
    {
        '\r' or '\n' => Hex1bKey.Enter,
        '\t' => Hex1bKey.Tab,
        '\x7f' or '\b' => Hex1bKey.Backspace,
        ' ' => Hex1bKey.Spacebar,
        >= 'a' and <= 'z' => Hex1bKey.A + (c - 'a'),
        >= 'A' and <= 'Z' => Hex1bKey.A + (c - 'A'),
        >= '0' and <= '9' => Hex1bKey.D0 + (c - '0'),
        _ => Hex1bKey.None
    };
}
