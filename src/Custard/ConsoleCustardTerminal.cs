using System.Threading.Channels;

namespace Custard;

/// <summary>
/// Console-based terminal implementation.
/// </summary>
public sealed class ConsoleCustardTerminal : ICustardTerminal, IDisposable
{
    private const string EnterAlternateBuffer = "\x1b[?1049h";
    private const string ExitAlternateBuffer = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";
    private const string MoveCursorHome = "\x1b[H";
    private const string HideCursor = "\x1b[?25l";
    private const string ShowCursor = "\x1b[?25h";

    private readonly Channel<CustardInputEvent> _inputChannel;
    private readonly CancellationTokenSource _inputLoopCts;
    private readonly Task _inputLoopTask;

    public ConsoleCustardTerminal()
    {
        _inputChannel = Channel.CreateUnbounded<CustardInputEvent>();
        _inputLoopCts = new CancellationTokenSource();
        _inputLoopTask = Task.Run(() => ReadInputLoopAsync(_inputLoopCts.Token));
    }

    public ChannelReader<CustardInputEvent> InputEvents => _inputChannel.Reader;

    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;

    public void Write(string text) => Console.Write(text);

    public void Clear()
    {
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
    }

    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);

    public void EnterAlternateScreen()
    {
        Console.Write(EnterAlternateBuffer);
        Console.Write(HideCursor);
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
    }

    public void ExitAlternateScreen()
    {
        Console.Write(ShowCursor);
        Console.Write(ExitAlternateBuffer);
    }

    private async Task ReadInputLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Console.KeyAvailable is non-blocking check
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    var evt = new KeyInputEvent(
                        keyInfo.Key,
                        keyInfo.KeyChar,
                        (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                        (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                        (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
                    );
                    await _inputChannel.Writer.WriteAsync(evt, cancellationToken);
                }
                else
                {
                    // Small delay to avoid busy-waiting
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _inputChannel.Writer.Complete();
        }
    }

    public void Dispose()
    {
        _inputLoopCts.Cancel();
        _inputLoopCts.Dispose();
        // Don't await the task in Dispose, just let it complete
    }
}
