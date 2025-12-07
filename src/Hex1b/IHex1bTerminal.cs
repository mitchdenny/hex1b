using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Abstraction for terminal output operations.
/// Implementations can target Console, websockets, test harnesses, etc.
/// </summary>
public interface IHex1bTerminalOutput
{
    void Write(string text);
    void Clear();
    void SetCursorPosition(int left, int top);
    void EnterAlternateScreen();
    void ExitAlternateScreen();
    int Width { get; }
    int Height { get; }
}

/// <summary>
/// Abstraction for terminal input operations.
/// Implementations can target Console, websockets, test harnesses, etc.
/// </summary>
public interface IHex1bTerminalInput
{
    /// <summary>
    /// Reads input events asynchronously. The channel completes when the terminal closes.
    /// </summary>
    ChannelReader<Hex1bInputEvent> InputEvents { get; }
}

/// <summary>
/// Combined terminal interface for convenience.
/// </summary>
public interface IHex1bTerminal : IHex1bTerminalOutput, IHex1bTerminalInput
{
}
