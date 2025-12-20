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
