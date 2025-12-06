using System.Threading.Channels;

namespace Custard;

/// <summary>
/// Abstraction for terminal output operations.
/// Implementations can target Console, websockets, test harnesses, etc.
/// </summary>
public interface ICustardTerminalOutput
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
public interface ICustardTerminalInput
{
    /// <summary>
    /// Reads input events asynchronously. The channel completes when the terminal closes.
    /// </summary>
    ChannelReader<CustardInputEvent> InputEvents { get; }
}

/// <summary>
/// Combined terminal interface for convenience.
/// </summary>
public interface ICustardTerminal : ICustardTerminalOutput, ICustardTerminalInput
{
}
