using Hex1b.Theming;

namespace Hex1b;

public class Hex1bRenderContext
{
    private readonly IHex1bTerminalOutput _output;

    public Hex1bRenderContext(IHex1bTerminalOutput output, Hex1bTheme? theme = null)
    {
        _output = output;
        Theme = theme ?? Hex1bThemes.Default;
    }

    public Hex1bTheme Theme { get; set; }

    public void EnterAlternateScreen() => _output.EnterAlternateScreen();
    public void ExitAlternateScreen() => _output.ExitAlternateScreen();
    public void Write(string text) => _output.Write(text);
    public void Clear() => _output.Clear();
    public void SetCursorPosition(int left, int top) => _output.SetCursorPosition(left, top);
    public int Width => _output.Width;
    public int Height => _output.Height;
}
