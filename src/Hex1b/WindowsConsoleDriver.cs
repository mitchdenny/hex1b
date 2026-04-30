using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Hex1b;

/// <summary>
/// Windows console driver using ReadConsoleInput for all input handling.
/// </summary>
/// <remarks>
/// This driver uses ReadConsoleInput to read INPUT_RECORD structures directly,
/// which allows us to properly handle WINDOW_BUFFER_SIZE_EVENT for resize detection
/// while also processing keyboard and mouse input.
/// 
/// Key events are converted to UTF-8 bytes (with VT sequences for special keys).
/// Mouse events are converted to SGR mouse encoding sequences.
/// Resize events fire the Resized event immediately.
/// 
/// Requirements: Windows 10 1809+ or Windows 11.
/// </remarks>
internal sealed class WindowsConsoleDriver : IConsoleDriver
{
    // Standard handles
    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    private static readonly nint InvalidHandleValue = new(-1);
    
    // Console mode flags - Input
    private const uint ENABLE_WINDOW_INPUT = 0x0008;           // Window buffer size changes reported
    private const uint ENABLE_MOUSE_INPUT = 0x0010;            // Mouse events reported
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;         // Required for disabling quick edit
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200; // Forward VT input sequences under ConPTY
    
    // INPUT_RECORD event types
    private const ushort KEY_EVENT = 0x0001;
    private const ushort MOUSE_EVENT = 0x0002;
    private const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
    
    // Virtual key codes
    private const ushort VK_BACK = 0x08;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_ESCAPE = 0x1B;
    private const ushort VK_PRIOR = 0x21;  // Page Up
    private const ushort VK_NEXT = 0x22;   // Page Down
    private const ushort VK_END = 0x23;
    private const ushort VK_HOME = 0x24;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_UP = 0x26;
    private const ushort VK_RIGHT = 0x27;
    private const ushort VK_DOWN = 0x28;
    private const ushort VK_INSERT = 0x2D;
    private const ushort VK_DELETE = 0x2E;
    private const ushort VK_F1 = 0x70;
    private const ushort VK_F2 = 0x71;
    private const ushort VK_F3 = 0x72;
    private const ushort VK_F4 = 0x73;
    private const ushort VK_F5 = 0x74;
    private const ushort VK_F6 = 0x75;
    private const ushort VK_F7 = 0x76;
    private const ushort VK_F8 = 0x77;
    private const ushort VK_F9 = 0x78;
    private const ushort VK_F10 = 0x79;
    private const ushort VK_F11 = 0x7A;
    private const ushort VK_F12 = 0x7B;
    // Printable OEM punctuation keys used when some Windows console hosts report
    // KEY_EVENT_RECORD.UnicodeChar == '\0' for ordinary text input. This covers
    // the standard US-layout punctuation set so commands, paths, and quotes still
    // reach the child shell. A future layout-aware ToUnicodeEx-based path would
    // be more robust for non-US keyboard mappings.
    private const ushort VK_OEM_1 = 0xBA;       // ;:
    private const ushort VK_OEM_PLUS = 0xBB;    // =+
    private const ushort VK_OEM_COMMA = 0xBC;   // ,<
    private const ushort VK_OEM_MINUS = 0xBD;   // -_
    private const ushort VK_OEM_PERIOD = 0xBE;  // .>
    private const ushort VK_OEM_2 = 0xBF;       // /?
    private const ushort VK_OEM_3 = 0xC0;       // `~
    private const ushort VK_OEM_4 = 0xDB;       // [{
    private const ushort VK_OEM_5 = 0xDC;       // \|
    private const ushort VK_OEM_6 = 0xDD;       // ]}
    private const ushort VK_OEM_7 = 0xDE;       // '"
    
    // Control key state flags
    private const uint RIGHT_ALT_PRESSED = 0x0001;
    private const uint LEFT_ALT_PRESSED = 0x0002;
    private const uint RIGHT_CTRL_PRESSED = 0x0004;
    private const uint LEFT_CTRL_PRESSED = 0x0008;
    private const uint SHIFT_PRESSED = 0x0010;
    
    // Mouse button state flags
    private const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;
    private const uint RIGHTMOST_BUTTON_PRESSED = 0x0002;
    private const uint FROM_LEFT_2ND_BUTTON_PRESSED = 0x0004;
    
    // Mouse event flags
    private const uint MOUSE_MOVED = 0x0001;
    private const uint MOUSE_WHEELED = 0x0004;
    
    // Console mode flags - Output
    private const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
    private const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
    
    // Wait constants
    private const uint WAIT_OBJECT_0 = 0;
    private const uint WAIT_TIMEOUT = 0x00000102;
    
    private readonly nint _inputHandle;
    private readonly nint _outputHandle;
    private uint _originalInputMode;
    private uint _originalOutputMode;
    private uint _originalOutputCodePage;
    private bool _inRawMode;
    private bool _disposed;
    private int _lastWidth;
    private int _lastHeight;
    
    // Buffer for pending bytes from key events
    private readonly Queue<byte> _pendingBytes = new();
    private readonly StringBuilder _pendingVtInput = new();
    
    // Track previous mouse state for generating proper events
    private uint _lastMouseButtonState;
    
    public WindowsConsoleDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsConsoleDriver only works on Windows");
        }
        
        _inputHandle = GetStdHandle(STD_INPUT_HANDLE);
        _outputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        
        if (_inputHandle == nint.Zero || _outputHandle == nint.Zero ||
            _inputHandle == InvalidHandleValue || _outputHandle == InvalidHandleValue)
        {
            throw CreateConsoleUnavailableException();
        }
        
        var (w, h) = GetWindowSize();
        _lastWidth = w;
        _lastHeight = h;
    }
    
    private (int width, int height) GetWindowSize()
    {
        if (GetConsoleScreenBufferInfo(_outputHandle, out var info))
        {
            var width = info.srWindow.Right - info.srWindow.Left + 1;
            var height = info.srWindow.Bottom - info.srWindow.Top + 1;
            return (width, height);
        }

        try
        {
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;
            if (width > 0 && height > 0)
            {
                return (width, height);
            }
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        throw CreateConsoleUnavailableException();
    }
    
    public int Width => GetWindowSize().width;
    public int Height => GetWindowSize().height;
    
    public event Action<int, int>? Resized;
    
    public void EnterRawMode(bool preserveOPost = false)
    {
        // Note: preserveOPost is Unix-specific (for termios OPOST flag).
        // On Windows, output processing is controlled differently and doesn't need this.
        if (_inRawMode) return;
        
        if (!GetConsoleMode(_inputHandle, out _originalInputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for input: {Marshal.GetLastWin32Error()}");
        }
        
        if (!GetConsoleMode(_outputHandle, out _originalOutputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for output: {Marshal.GetLastWin32Error()}");
        }
        
        // Use console input mode with VT input enabled when available:
        // - Enable window input for resize events
        // - Enable mouse input for classic console mouse events
        // - Enable VT input so nested Hex1b apps running under ConPTY can receive
        //   forwarded CSI/SGR mouse sequences from an outer terminal widget.
        // - Disable quick edit (via ENABLE_EXTENDED_FLAGS with no ENABLE_QUICK_EDIT_MODE)
        var newInputMode = ENABLE_WINDOW_INPUT |
                           ENABLE_MOUSE_INPUT |
                           ENABLE_EXTENDED_FLAGS |
                           ENABLE_VIRTUAL_TERMINAL_INPUT;

        if (!SetConsoleMode(_inputHandle, newInputMode))
        {
            var error = Marshal.GetLastWin32Error();
            var fallbackInputMode = ENABLE_WINDOW_INPUT | ENABLE_MOUSE_INPUT | ENABLE_EXTENDED_FLAGS;
            if (!SetConsoleMode(_inputHandle, fallbackInputMode))
            {
                throw new InvalidOperationException($"SetConsoleMode failed for input (error {error}).");
            }

            TraceInput($"vt-input-unavailable error={error}");
        }
        
        // VT output for escape sequences
        var newOutputMode = ENABLE_PROCESSED_OUTPUT | 
                           ENABLE_WRAP_AT_EOL_OUTPUT | 
                           ENABLE_VIRTUAL_TERMINAL_PROCESSING |
                           DISABLE_NEWLINE_AUTO_RETURN;
        
        if (!SetConsoleMode(_outputHandle, newOutputMode))
        {
            SetConsoleMode(_inputHandle, _originalInputMode);
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SetConsoleMode failed for output (error {error}).");
        }
        
        // Ensure UTF-8 output so WriteFile sends correct multi-byte sequences.
        // Without this, Native AOT binaries use the system default code page
        // (e.g. 437) and Unicode characters are garbled.
        _originalOutputCodePage = GetConsoleOutputCP();
        if (_originalOutputCodePage != 65001)
        {
            if (!SetConsoleOutputCP(65001))
            {
                SetConsoleMode(_inputHandle, _originalInputMode);
                SetConsoleMode(_outputHandle, _originalOutputMode);
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"SetConsoleOutputCP failed (error {error}).");
            }
        }
        
        _inRawMode = true;
        Console.TreatControlCAsInput = true;
    }
    
    public void ExitRawMode()
    {
        if (!_inRawMode) return;
        
        SetConsoleMode(_inputHandle, _originalInputMode);
        SetConsoleMode(_outputHandle, _originalOutputMode);
        
        if (_originalOutputCodePage != 0 && _originalOutputCodePage != 65001)
        {
            SetConsoleOutputCP(_originalOutputCodePage);
        }
        
        _inRawMode = false;
        Console.TreatControlCAsInput = false;
    }
    
    public bool DataAvailable
    {
        get
        {
            if (!_inRawMode) return false;
            if (_pendingBytes.Count > 0) return true;
            
            if (GetNumberOfConsoleInputEvents(_inputHandle, out var count))
            {
                return count > 0;
            }
            return false;
        }
    }
    
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!_inRawMode)
        {
            throw new InvalidOperationException("Must enter raw mode before reading");
        }
        
        return await Task.Run(() =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // First, drain any pending bytes from previous processing
                    if (_pendingBytes.Count > 0)
                    {
                        return DrainPendingBytes(buffer.Span);
                    }
                    
                    // Check for resize events via PeekConsoleInput before blocking on ReadFile
                    DrainResizeEvents();
                    
                    // Wait for input with timeout so we can check cancellation periodically
                    var waitResult = WaitForSingleObject(_inputHandle, 100);
                    
                    if (waitResult == WAIT_TIMEOUT)
                    {
                        continue;
                    }
                    
                    if (waitResult != WAIT_OBJECT_0)
                    {
                        throw new InvalidOperationException($"WaitForSingleObject failed: {Marshal.GetLastWin32Error()}");
                    }
                    
                    // Drain resize events that may have arrived
                    DrainResizeEvents();
                    
                    // After draining resize events, check if there's still data to read.
                    // If DrainResizeEvents consumed the event that woke us, ReadFile would
                    // block. Go back to WaitForSingleObject instead.
                    if (!GetNumberOfConsoleInputEvents(_inputHandle, out var remaining) || remaining == 0)
                    {
                        continue;
                    }
                    
                    // Read raw bytes via ReadFile. With ENABLE_VIRTUAL_TERMINAL_INPUT,
                    // keyboard input arrives as VT sequences and APC responses (like KGP
                    // protocol replies) come through as raw bytes — unlike ReadConsoleInput
                    // which only returns structured INPUT_RECORD and drops APC.
                    unsafe
                    {
                        fixed (byte* ptr = buffer.Span)
                        {
                            if (ReadFile(_inputHandle, ptr, (uint)buffer.Length, out var bytesRead, nint.Zero))
                            {
                                if (bytesRead > 0)
                                {
                                    return (int)bytesRead;
                                }
                            }
                            else
                            {
                                var error = Marshal.GetLastWin32Error();
                                // ERROR_IO_PENDING or similar — try again
                                if (error != 0)
                                {
                                    TraceInput($"ReadFile failed error={error}");
                                }
                            }
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceInput($"read-error {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }, ct);
    }
    
    /// <summary>
    /// Peeks at the console input buffer for WINDOW_BUFFER_SIZE_EVENT records
    /// and fires resize events. Reads events one at a time to avoid consuming
    /// key/mouse events that ReadFile needs.
    /// </summary>
    private void DrainResizeEvents()
    {
        if (!GetNumberOfConsoleInputEvents(_inputHandle, out var eventCount) || eventCount == 0)
            return;
        
        var peekRecords = new INPUT_RECORD[eventCount];
        if (!PeekConsoleInput(_inputHandle, peekRecords, (uint)peekRecords.Length, out var numPeeked) || numPeeked == 0)
            return;
        
        // Check if any resize events are present
        bool hasResize = false;
        for (int i = 0; i < numPeeked; i++)
        {
            if (peekRecords[i].EventType == WINDOW_BUFFER_SIZE_EVENT)
            {
                hasResize = true;
                break;
            }
        }
        
        if (!hasResize)
            return;
        
        // Read events one at a time. Consume resize events and any non-key/mouse
        // events (like focus events). Leave key/mouse events for ReadFile by
        // not calling ReadConsoleInput when a key/mouse record is at the head.
        var singleRecord = new INPUT_RECORD[1];
        while (GetNumberOfConsoleInputEvents(_inputHandle, out var remaining) && remaining > 0)
        {
            // Peek at the head
            if (!PeekConsoleInput(_inputHandle, singleRecord, 1, out var peeked) || peeked == 0)
                break;
            
            if (singleRecord[0].EventType == WINDOW_BUFFER_SIZE_EVENT)
            {
                // Consume and process resize
                ReadConsoleInput(_inputHandle, singleRecord, 1, out _);
                ProcessResizeEvent(ref singleRecord[0].WindowBufferSizeEvent);
            }
            else if (singleRecord[0].EventType is KEY_EVENT or MOUSE_EVENT)
            {
                // Stop — don't consume key/mouse events, ReadFile needs them
                break;
            }
            else
            {
                // Consume and discard other event types (focus, menu, etc.)
                ReadConsoleInput(_inputHandle, singleRecord, 1, out _);
            }
        }
    }
    
    private int DrainPendingBytes(Span<byte> buffer)
    {
        var count = Math.Min(_pendingBytes.Count, buffer.Length);
        for (int i = 0; i < count; i++)
        {
            buffer[i] = _pendingBytes.Dequeue();
        }
        return count;
    }
    
    private void ProcessInputRecord(ref INPUT_RECORD record)
    {
        switch (record.EventType)
        {
            case KEY_EVENT:
                ProcessKeyEvent(ref record.KeyEvent);
                break;
                
            case MOUSE_EVENT:
                ProcessMouseEvent(ref record.MouseEvent);
                break;
                
            case WINDOW_BUFFER_SIZE_EVENT:
                ProcessResizeEvent(ref record.WindowBufferSizeEvent);
                break;
        }
    }
    
    private void ProcessKeyEvent(ref KEY_EVENT_RECORD key)
    {
        // Only process key down events
        if (key.bKeyDown == 0) return;
        
        var vk = key.wVirtualKeyCode;
        var ch = key.UnicodeChar;
        var ctrl = key.dwControlKeyState;
        
        bool hasCtrl = (ctrl & (LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED)) != 0;
        bool hasAlt = (ctrl & (LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED)) != 0;
        bool hasShift = (ctrl & SHIFT_PRESSED) != 0;
        int repeatCount = Math.Max(1, (int)key.wRepeatCount);

        TraceInput(
            $"key vk=0x{vk:X2} scan=0x{key.wVirtualScanCode:X2} char=0x{(int)ch:X4} ctrl={hasCtrl} alt={hasAlt} shift={hasShift} repeat={repeatCount}");

        if (vk == 0 && key.wVirtualScanCode == 0 && ch != 0)
        {
            ProcessVirtualTerminalInputChar(ch);
            return;
        }

        FlushPendingVirtualTerminalInput();
        
        // Check for special keys that generate VT sequences
        var vtSequence = GetVtSequence(vk, hasCtrl, hasAlt, hasShift);
        if (vtSequence != null)
        {
            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                foreach (var b in vtSequence)
                {
                    _pendingBytes.Enqueue(b);
                }
            }
            return;
        }
        
        // Handle Ctrl+letter combinations (Ctrl+A = 1, Ctrl+B = 2, etc.)
        if (hasCtrl && !hasAlt && ch >= 'A' - 64 && ch <= 'Z' - 64)
        {
            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                _pendingBytes.Enqueue((byte)ch);
            }
            return;
        }
        
        // Handle Alt+key combinations - emit ESC followed by the character
        // This matches the VT sequence that Linux terminals generate for Alt+key
        if (hasAlt && !hasCtrl && ch != 0)
        {
            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                _pendingBytes.Enqueue(0x1B); // ESC
                EnqueueUtf8Char(ch);
            }
            return;
        }

        var text = GetPrintableText(vk, ch, hasCtrl, hasAlt, hasShift);
        if (!string.IsNullOrEmpty(text))
        {
            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                EnqueueUtf8(text);
            }
        }
    }

    private void ProcessVirtualTerminalInputChar(char ch)
    {
        _pendingVtInput.Append(ch);

        while (_pendingVtInput.Length > 0)
        {
            if (_pendingVtInput[0] != '\x1b')
            {
                EnqueueUtf8Char(_pendingVtInput[0]);
                _pendingVtInput.Remove(0, 1);
                continue;
            }

            if (!TryReadCompleteEscapeSequence(_pendingVtInput, out var sequence))
            {
                return;
            }

            _pendingVtInput.Remove(0, sequence.Length);

            if (TryTranslateWin32InputSequence(sequence, out var translated))
            {
                foreach (var b in translated)
                {
                    _pendingBytes.Enqueue(b);
                }

                TraceInput($"vt-decoded sequence={EscapeForTrace(sequence)} bytes={BitConverter.ToString(translated)}");
                continue;
            }

            EnqueueUtf8(sequence);
        }
    }

    private void FlushPendingVirtualTerminalInput()
    {
        if (_pendingVtInput.Length == 0)
        {
            return;
        }

        EnqueueUtf8(_pendingVtInput.ToString());
        _pendingVtInput.Clear();
    }

    private void EnqueueUtf8Char(char value)
    {
        Span<byte> utf8 = stackalloc byte[4];
        var charSpan = new ReadOnlySpan<char>(in value);
        var len = Encoding.UTF8.GetBytes(charSpan, utf8);
        for (int i = 0; i < len; i++)
        {
            _pendingBytes.Enqueue(utf8[i]);
        }
    }

    private void EnqueueUtf8(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var b in bytes)
        {
            _pendingBytes.Enqueue(b);
        }
    }

    internal static string? GetPrintableText(ushort vk, char ch, bool hasCtrl, bool hasAlt, bool hasShift)
    {
        if (ch != 0)
        {
            return ch.ToString();
        }

        // Some Windows terminal hosts deliver KEY_EVENT_RECORDs with a zero
        // UnicodeChar for ordinary printable keys. Fall back to a virtual-key
        // mapping so interactive shells still receive characters like "exit".
        if (hasCtrl || hasAlt)
        {
            return null;
        }

        if (vk is >= 0x41 and <= 0x5A)
        {
            var letter = (char)vk;
            return hasShift ? letter.ToString() : char.ToLowerInvariant(letter).ToString();
        }

        if (vk is >= 0x30 and <= 0x39)
        {
            return hasShift
                ? ")!@#$%^&*("[vk - 0x30].ToString()
                : ((char)vk).ToString();
        }

        return vk switch
        {
            VK_SPACE => " ",
            VK_OEM_1 => hasShift ? ":" : ";",
            VK_OEM_PLUS => hasShift ? "+" : "=",
            VK_OEM_COMMA => hasShift ? "<" : ",",
            VK_OEM_MINUS => hasShift ? "_" : "-",
            VK_OEM_PERIOD => hasShift ? ">" : ".",
            VK_OEM_2 => hasShift ? "?" : "/",
            VK_OEM_3 => hasShift ? "~" : "`",
            VK_OEM_4 => hasShift ? "{" : "[",
            VK_OEM_5 => hasShift ? "|" : "\\",
            VK_OEM_6 => hasShift ? "}" : "]",
            VK_OEM_7 => hasShift ? "\"" : "'",
            _ => null
        };
    }

    internal static bool TryTranslateWin32InputSequence(string sequence, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        if (string.IsNullOrEmpty(sequence) ||
            !sequence.StartsWith("\x1b[", StringComparison.Ordinal) ||
            !sequence.EndsWith('_'))
        {
            return false;
        }

        var payload = sequence[2..^1];
        var parts = payload.Split(';');
        if (parts.Length != 6)
        {
            return false;
        }

        Span<int> values = stackalloc int[6];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        var vk = unchecked((ushort)values[0]);
        var unicodeChar = values[2] is > 0 and <= char.MaxValue ? (char)values[2] : '\0';
        var keyDown = values[3] != 0;
        var controlState = unchecked((uint)values[4]);
        var repeatCount = Math.Max(1, values[5]);

        if (!keyDown)
        {
            return true;
        }

        var hasCtrl = (controlState & (LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED)) != 0;
        var hasAlt = (controlState & (LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED)) != 0;
        var hasShift = (controlState & SHIFT_PRESSED) != 0;

        var output = new List<byte>(Math.Max(4, repeatCount));

        var vtSequence = GetVtSequence(vk, hasCtrl, hasAlt, hasShift);
        if (vtSequence != null)
        {
            for (var repeat = 0; repeat < repeatCount; repeat++)
            {
                output.AddRange(vtSequence);
            }

            bytes = output.ToArray();
            return true;
        }

        if (hasCtrl && !hasAlt && unicodeChar >= 'A' - 64 && unicodeChar <= 'Z' - 64)
        {
            for (var repeat = 0; repeat < repeatCount; repeat++)
            {
                output.Add((byte)unicodeChar);
            }

            bytes = output.ToArray();
            return true;
        }

        if (hasAlt && !hasCtrl && unicodeChar != 0)
        {
            for (var repeat = 0; repeat < repeatCount; repeat++)
            {
                output.Add(0x1B);
                output.AddRange(Encoding.UTF8.GetBytes(unicodeChar.ToString()));
            }

            bytes = output.ToArray();
            return true;
        }

        var text = GetPrintableText(vk, unicodeChar, hasCtrl, hasAlt, hasShift);
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        for (var repeat = 0; repeat < repeatCount; repeat++)
        {
            output.AddRange(Encoding.UTF8.GetBytes(text));
        }

        bytes = output.ToArray();
        return true;
    }

    private static void TraceInput(string message)
    {
        var tracePath = Environment.GetEnvironmentVariable("HEX1B_CONSOLE_INPUT_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            File.AppendAllText(tracePath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static PlatformNotSupportedException CreateConsoleUnavailableException()
    {
        return new PlatformNotSupportedException(
            "WindowsConsoleDriver requires an attached interactive Windows console.");
    }

    private static byte[]? GetVtSequence(ushort vk, bool ctrl, bool alt, bool shift)
    {
        // Calculate modifier parameter for CSI sequences
        int mod = 1;
        if (shift) mod += 1;
        if (alt) mod += 2;
        if (ctrl) mod += 4;
        
        return vk switch
        {
            VK_UP => mod == 1 ? "\x1b[A"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}A"),
            VK_DOWN => mod == 1 ? "\x1b[B"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}B"),
            VK_RIGHT => mod == 1 ? "\x1b[C"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}C"),
            VK_LEFT => mod == 1 ? "\x1b[D"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}D"),
            VK_HOME => mod == 1 ? "\x1b[H"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}H"),
            VK_END => mod == 1 ? "\x1b[F"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}F"),
            VK_INSERT => mod == 1 ? "\x1b[2~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[2;{mod}~"),
            VK_DELETE => mod == 1 ? "\x1b[3~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[3;{mod}~"),
            VK_PRIOR => mod == 1 ? "\x1b[5~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[5;{mod}~"),
            VK_NEXT => mod == 1 ? "\x1b[6~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[6;{mod}~"),
            VK_F1 => mod == 1 ? "\x1bOP"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}P"),
            VK_F2 => mod == 1 ? "\x1bOQ"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}Q"),
            VK_F3 => mod == 1 ? "\x1bOR"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}R"),
            VK_F4 => mod == 1 ? "\x1bOS"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[1;{mod}S"),
            VK_F5 => mod == 1 ? "\x1b[15~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[15;{mod}~"),
            VK_F6 => mod == 1 ? "\x1b[17~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[17;{mod}~"),
            VK_F7 => mod == 1 ? "\x1b[18~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[18;{mod}~"),
            VK_F8 => mod == 1 ? "\x1b[19~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[19;{mod}~"),
            VK_F9 => mod == 1 ? "\x1b[20~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[20;{mod}~"),
            VK_F10 => mod == 1 ? "\x1b[21~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[21;{mod}~"),
            VK_F11 => mod == 1 ? "\x1b[23~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[23;{mod}~"),
            VK_F12 => mod == 1 ? "\x1b[24~"u8.ToArray() : Encoding.ASCII.GetBytes($"\x1b[24;{mod}~"),
            // PowerShell on Windows expects 0x7F (DEL) for normal backspace, 0x08 (BS) for Ctrl+Backspace
            VK_BACK => [(byte)(ctrl ? 0x08 : 0x7F)],
            VK_TAB => shift ? "\x1b[Z"u8.ToArray() : [(byte)0x09],
            VK_RETURN => [(byte)0x0D],
            VK_ESCAPE => [(byte)0x1B],
            _ => null
        };
    }
    
    private void ProcessMouseEvent(ref MOUSE_EVENT_RECORD mouse)
    {
        var x = mouse.dwMousePosition.X + 1;  // 1-based
        var y = mouse.dwMousePosition.Y + 1;  // 1-based
        var buttons = mouse.dwButtonState;
        var flags = mouse.dwEventFlags;
        var ctrl = mouse.dwControlKeyState;
        
        bool hasCtrl = (ctrl & (LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED)) != 0;
        bool hasAlt = (ctrl & (LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED)) != 0;
        bool hasShift = (ctrl & SHIFT_PRESSED) != 0;
        
        int modifiers = 0;
        if (hasShift) modifiers |= 4;
        if (hasAlt) modifiers |= 8;
        if (hasCtrl) modifiers |= 16;
        
        if ((flags & MOUSE_WHEELED) != 0)
        {
            int delta = (short)(buttons >> 16);
            int button = delta > 0 ? 64 : 65;
            button |= modifiers;
            var seq = $"\x1b[<{button};{x};{y}M";
            foreach (var b in Encoding.ASCII.GetBytes(seq))
            {
                _pendingBytes.Enqueue(b);
            }
            return;
        }
        
        uint buttonMask = FROM_LEFT_1ST_BUTTON_PRESSED | RIGHTMOST_BUTTON_PRESSED | FROM_LEFT_2ND_BUTTON_PRESSED;
        uint currentButtons = buttons & buttonMask;
        uint previousButtons = _lastMouseButtonState & buttonMask;
        
        bool isMove = (flags & MOUSE_MOVED) != 0;
        bool buttonsChanged = currentButtons != previousButtons;
        
        if (buttonsChanged)
        {
            uint pressed = currentButtons & ~previousButtons;
            uint released = previousButtons & ~currentButtons;
            
            if (pressed != 0)
            {
                int button = GetButtonNumber(pressed) | modifiers;
                var seq = $"\x1b[<{button};{x};{y}M";
                foreach (var b in Encoding.ASCII.GetBytes(seq))
                {
                    _pendingBytes.Enqueue(b);
                }
            }
            
            if (released != 0)
            {
                int button = GetButtonNumber(released) | modifiers;
                var seq = $"\x1b[<{button};{x};{y}m";
                foreach (var b in Encoding.ASCII.GetBytes(seq))
                {
                    _pendingBytes.Enqueue(b);
                }
            }
        }
        else if (isMove)
        {
            // Motion event: button code 32 = motion flag, 3 = no button (so 35 for motion with no button)
            // If button is held, use that button number + 32
            int button = currentButtons != 0 
                ? GetButtonNumber(currentButtons) | 32 | modifiers
                : 35 | modifiers;  // 35 = 32 (motion) + 3 (no button)
            var seq = $"\x1b[<{button};{x};{y}M";
            foreach (var b in Encoding.ASCII.GetBytes(seq))
            {
                _pendingBytes.Enqueue(b);
            }
        }
        
        _lastMouseButtonState = buttons;
    }
    
    private static int GetButtonNumber(uint buttonState)
    {
        if ((buttonState & FROM_LEFT_1ST_BUTTON_PRESSED) != 0) return 0;
        if ((buttonState & FROM_LEFT_2ND_BUTTON_PRESSED) != 0) return 1;
        if ((buttonState & RIGHTMOST_BUTTON_PRESSED) != 0) return 2;
        return 0;
    }
    
    private void ProcessResizeEvent(ref WINDOW_BUFFER_SIZE_RECORD resize)
    {
        var (newWidth, newHeight) = GetWindowSize();
        
        if (newWidth != _lastWidth || newHeight != _lastHeight)
        {
            _lastWidth = newWidth;
            _lastHeight = newHeight;
            Resized?.Invoke(newWidth, newHeight);
        }
    }
    
    public void Write(ReadOnlySpan<byte> data)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var remaining = (uint)data.Length;
                var offset = 0;
                
                while (remaining > 0)
                {
                    if (!WriteFile(_outputHandle, ptr + offset, remaining, out var bytesWritten, nint.Zero))
                    {
                        var error = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException($"WriteFile failed: {error}");
                    }
                    
                    offset += (int)bytesWritten;
                    remaining -= bytesWritten;
                }
            }
        }
    }
    
    public void Flush()
    {
        FlushFileBuffers(_outputHandle);
    }
    
    public void DrainInput()
    {
        if (!_inRawMode) return;
        
        _pendingBytes.Clear();
        _pendingVtInput.Clear();
        FlushConsoleInputBuffer(_inputHandle);
    }

    private static bool TryReadCompleteEscapeSequence(StringBuilder buffer, out string sequence)
    {
        sequence = string.Empty;
        if (buffer.Length == 0 || buffer[0] != '\x1b')
        {
            return false;
        }

        if (buffer.Length == 1)
        {
            return false;
        }

        var second = buffer[1];
        switch (second)
        {
            case '[':
                for (var i = 2; i < buffer.Length; i++)
                {
                    var c = buffer[i];
                    if (c >= '@' && c <= '~')
                    {
                        sequence = buffer.ToString(0, i + 1);
                        return true;
                    }
                }

                return false;

            case 'O':
            case 'N':
            case '(':
            case ')':
            case '*':
            case '+':
            case '#':
                if (buffer.Length < 3)
                {
                    return false;
                }

                sequence = buffer.ToString(0, 3);
                return true;

            default:
                sequence = buffer.ToString(0, 2);
                return true;
        }
    }

    private static string EscapeForTrace(string value)
        => value
            .Replace("\x1b", "\\x1b", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ExitRawMode();
    }
    
    // P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumberOfConsoleInputEvents(nint hConsoleInput, out uint lpcNumberOfEvents);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadConsoleInput(
        nint hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekConsoleInput(
        nint hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool ReadFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        nint lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool WriteFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        nint lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushConsoleInputBuffer(nint hConsoleInput);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(nint hFile);
    
    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleOutputCP();
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(nint hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }
    
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_RECORD
    {
        [FieldOffset(0)]
        public ushort EventType;
        
        [FieldOffset(4)]
        public KEY_EVENT_RECORD KeyEvent;
        
        [FieldOffset(4)]
        public MOUSE_EVENT_RECORD MouseEvent;
        
        [FieldOffset(4)]
        public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public int bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSE_EVENT_RECORD
    {
        public COORD dwMousePosition;
        public uint dwButtonState;
        public uint dwControlKeyState;
        public uint dwEventFlags;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOW_BUFFER_SIZE_RECORD
    {
        public COORD dwSize;
    }
}
