using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Console-based terminal implementation.
/// </summary>
public sealed class ConsoleHex1bTerminal : IHex1bTerminal, IDisposable
{
    private const string EnterAlternateBuffer = "\x1b[?1049h";
    private const string ExitAlternateBuffer = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";
    private const string MoveCursorHome = "\x1b[H";
    private const string HideCursor = "\x1b[?25l";
    private const string ShowCursor = "\x1b[?25h";

    private readonly Channel<Hex1bInputEvent> _inputChannel;
    private readonly CancellationTokenSource _inputLoopCts;
    private readonly Task _inputLoopTask;
    private PosixSignalRegistration? _sigwinchRegistration;
    private int _lastWidth;
    private int _lastHeight;

    public ConsoleHex1bTerminal()
    {
        // Disable Ctrl+C handling at Console level so we get the key event
        Console.TreatControlCAsInput = true;
        
        _inputChannel = Channel.CreateUnbounded<Hex1bInputEvent>();
        _inputLoopCts = new CancellationTokenSource();
        
        // Track initial size for resize detection
        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;
        
        // Register for SIGWINCH on supported platforms (Linux, macOS)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _sigwinchRegistration = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, OnSigwinch);
        }
        // TODO: Windows support - could poll for size changes or use Console APIs
        
        _inputLoopTask = Task.Run(() => ReadInputLoopAsync(_inputLoopCts.Token));
    }
    
    private void OnSigwinch(PosixSignalContext context)
    {
        // Don't cancel the default behavior
        context.Cancel = false;
        
        var newWidth = Console.WindowWidth;
        var newHeight = Console.WindowHeight;
        
        // Only send event if size actually changed
        if (newWidth != _lastWidth || newHeight != _lastHeight)
        {
            _lastWidth = newWidth;
            _lastHeight = newHeight;
            
            // Write resize event to the channel (non-blocking)
            _inputChannel.Writer.TryWrite(new ResizeInputEvent(newWidth, newHeight));
        }
    }

    public ChannelReader<Hex1bInputEvent> InputEvents => _inputChannel.Reader;

    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;

    public void Write(string text)
    {
        Console.Write(text);
        Console.Out.Flush();
    }

    public void Clear()
    {
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
        Console.Out.Flush();
    }

    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);

    public void EnterAlternateScreen()
    {
        Console.Write(EnterAlternateBuffer);
        Console.Write(HideCursor);
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
        Console.Out.Flush();
    }

    public void ExitAlternateScreen()
    {
        Console.Write(ShowCursor);
        Console.Write(ExitAlternateBuffer);
        Console.Out.Flush();
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
        _sigwinchRegistration?.Dispose();
        _inputLoopCts.Cancel();
        _inputLoopCts.Dispose();
        // Don't await the task in Dispose, just let it complete
    }
}
