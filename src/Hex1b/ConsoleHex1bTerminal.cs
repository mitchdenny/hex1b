using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Hex1b.Input;

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

    private readonly Channel<Hex1bEvent> _inputChannel;
    private readonly CancellationTokenSource _inputLoopCts;
    private readonly Task _inputLoopTask;
    private PosixSignalRegistration? _sigwinchRegistration;
    private int _lastWidth;
    private int _lastHeight;
    private readonly bool _enableMouse;

    public ConsoleHex1bTerminal(bool enableMouse = false)
    {
        _enableMouse = enableMouse;
        
        // Disable Ctrl+C handling at Console level so we get the key event
        Console.TreatControlCAsInput = true;
        
        _inputChannel = Channel.CreateUnbounded<Hex1bEvent>();
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
            _inputChannel.Writer.TryWrite(new Hex1bResizeEvent(newWidth, newHeight));
        }
    }

    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;

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
        if (_enableMouse)
        {
            Console.Write(MouseParser.EnableMouseTracking);
        }
        Console.Write(ClearScreen);
        Console.Write(MoveCursorHome);
        Console.Out.Flush();
    }

    public void ExitAlternateScreen()
    {
        if (_enableMouse)
        {
            Console.Write(MouseParser.DisableMouseTracking);
        }
        Console.Write(ShowCursor);
        Console.Write(ExitAlternateBuffer);
        Console.Out.Flush();
    }

    private async Task ReadInputLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Use a StringBuilder to accumulate escape sequences
            var escapeBuffer = new StringBuilder();
            var inEscapeSequence = false;
            var inMouseSequence = false;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_enableMouse)
                {
                    // For mouse support, we need to read raw bytes to capture escape sequences
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(intercept: true);
                        
                        // Check for escape character
                        if (keyInfo.Key == ConsoleKey.Escape || keyInfo.KeyChar == '\x1b')
                        {
                            // Start of escape sequence
                            inEscapeSequence = true;
                            escapeBuffer.Clear();
                            escapeBuffer.Append('\x1b');
                            
                            // Read the rest of the escape sequence with a small timeout
                            var sequenceStart = DateTime.UtcNow;
                            while ((DateTime.UtcNow - sequenceStart).TotalMilliseconds < 50)
                            {
                                if (Console.KeyAvailable)
                                {
                                    var nextKey = Console.ReadKey(intercept: true);
                                    escapeBuffer.Append(nextKey.KeyChar);
                                    
                                    // Check if this is a mouse sequence start
                                    if (escapeBuffer.Length == 2 && nextKey.KeyChar == '[')
                                    {
                                        // Could be CSI sequence
                                    }
                                    else if (escapeBuffer.Length == 3 && escapeBuffer[2] == '<')
                                    {
                                        // SGR mouse sequence
                                        inMouseSequence = true;
                                    }
                                    
                                    // Check if sequence is complete
                                    if (inMouseSequence)
                                    {
                                        if (nextKey.KeyChar == 'M' || nextKey.KeyChar == 'm')
                                        {
                                            // Mouse sequence complete
                                            var sequence = escapeBuffer.ToString();
                                            if (sequence.StartsWith("\x1b[<"))
                                            {
                                                var sgrPart = sequence[3..]; // Remove \e[<
                                                if (MouseParser.TryParseSgr(sgrPart, out var mouseEvent) && mouseEvent != null)
                                                {
                                                    await _inputChannel.Writer.WriteAsync(mouseEvent, cancellationToken);
                                                }
                                            }
                                            inEscapeSequence = false;
                                            inMouseSequence = false;
                                            escapeBuffer.Clear();
                                            break;
                                        }
                                    }
                                    else if (!char.IsDigit(nextKey.KeyChar) && nextKey.KeyChar != ';' && nextKey.KeyChar != '<' && nextKey.KeyChar != '[')
                                    {
                                        // Some other CSI sequence (arrow keys, etc.) - pass as key event
                                        var evt = new Hex1bKeyEvent(
                                            KeyMapper.ToHex1bKey(keyInfo.Key),
                                            keyInfo.KeyChar,
                                            KeyMapper.ToHex1bModifiers(
                                                (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                                                (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                                                (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
                                            )
                                        );
                                        await _inputChannel.Writer.WriteAsync(evt, cancellationToken);
                                        inEscapeSequence = false;
                                        escapeBuffer.Clear();
                                        break;
                                    }
                                }
                                else
                                {
                                    await Task.Delay(1, cancellationToken);
                                }
                            }
                            
                            // If we timed out without completing, send as plain Escape
                            if (inEscapeSequence && !inMouseSequence)
                            {
                                var evt = new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None);
                                await _inputChannel.Writer.WriteAsync(evt, cancellationToken);
                                inEscapeSequence = false;
                                escapeBuffer.Clear();
                            }
                        }
                        else
                        {
                            // Regular key
                            var evt = new Hex1bKeyEvent(
                                KeyMapper.ToHex1bKey(keyInfo.Key),
                                keyInfo.KeyChar,
                                KeyMapper.ToHex1bModifiers(
                                    (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                                    (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                                    (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
                                )
                            );
                            await _inputChannel.Writer.WriteAsync(evt, cancellationToken);
                        }
                    }
                    else
                    {
                        // Small delay to avoid busy-waiting
                        await Task.Delay(10, cancellationToken);
                    }
                }
                else
                {
                    // Original simple key reading (no mouse support)
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(intercept: true);
                        var evt = new Hex1bKeyEvent(
                            KeyMapper.ToHex1bKey(keyInfo.Key),
                            keyInfo.KeyChar,
                            KeyMapper.ToHex1bModifiers(
                                (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                                (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                                (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
                            )
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
