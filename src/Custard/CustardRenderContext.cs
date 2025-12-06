namespace Custard;

public class CustardRenderContext
{
    private readonly ICustardTerminalOutput _output;

    public CustardRenderContext(ICustardTerminalOutput output)
    {
        _output = output;
    }

    public void EnterAlternateScreen() => _output.EnterAlternateScreen();
    public void ExitAlternateScreen() => _output.ExitAlternateScreen();
    public void Write(string text) => _output.Write(text);
    public void Clear() => _output.Clear();
    public void SetCursorPosition(int left, int top) => _output.SetCursorPosition(left, top);
    public int Width => _output.Width;
    public int Height => _output.Height;
}
