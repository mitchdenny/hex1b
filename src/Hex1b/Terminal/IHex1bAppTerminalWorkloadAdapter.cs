using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// App-side interface: Adds the higher-level APIs that Hex1bApp needs.
/// Extends the terminal-side interface so the same adapter serves both.
/// </summary>
/// <remarks>
/// This interface represents the "app side" of the workload adapter.
/// It provides the convenient APIs that Hex1bApp and its components use:
/// Write(), InputEvents, Width/Height, etc.
/// 
/// The inheritance from <see cref="IHex1bTerminalWorkloadAdapter"/> ensures
/// that any implementation can also be consumed by the future Hex1bTerminal.
/// </remarks>
public interface IHex1bAppTerminalWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    // === Output (App → Terminal) ===
    
    /// <summary>
    /// Write ANSI-encoded output to the terminal.
    /// </summary>
    void Write(string text);
    
    /// <summary>
    /// Write raw bytes to the terminal.
    /// </summary>
    void Write(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// Flush any buffered output.
    /// </summary>
    void Flush();
    
    // === Input (Terminal → App) ===
    
    /// <summary>
    /// Channel of input events from the terminal.
    /// </summary>
    ChannelReader<Hex1bEvent> InputEvents { get; }
    
    // === Terminal Info ===
    
    /// <summary>
    /// Current terminal width.
    /// </summary>
    int Width { get; }
    
    /// <summary>
    /// Current terminal height.
    /// </summary>
    int Height { get; }
    
    /// <summary>
    /// Terminal capabilities (mouse, sixel, colors, etc.).
    /// </summary>
    TerminalCapabilities Capabilities { get; }
    
    // === Lifecycle ===
    
    /// <summary>
    /// Enter TUI mode (alternate screen, hide cursor, enable mouse if supported, etc.).
    /// </summary>
    void EnterTuiMode();
    
    /// <summary>
    /// Exit TUI mode (show cursor, disable mouse, exit alternate screen, etc.).
    /// </summary>
    void ExitTuiMode();
    
    /// <summary>
    /// Clear the screen.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Set the cursor position.
    /// </summary>
    void SetCursorPosition(int left, int top);
}
