using System.Runtime.InteropServices;
using System.Text;

namespace Hex1b.Terminal;

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
    
    // Console mode flags - Input
    private const uint ENABLE_WINDOW_INPUT = 0x0008;           // Window buffer size changes reported
    private const uint ENABLE_MOUSE_INPUT = 0x0010;            // Mouse events reported
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;         // Required for disabling quick edit
    
    // INPUT_RECORD event types
    private const ushort KEY_EVENT = 0x0001;
    private const ushort MOUSE_EVENT = 0x0002;
    private const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
    
    // Virtual key codes
    private const ushort VK_BACK = 0x08;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_RETURN = 0x0D;
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
    private bool _inRawMode;
    private bool _disposed;
    private int _lastWidth;
    private int _lastHeight;
    
    // Buffer for pending bytes from key events
    private readonly Queue<byte> _pendingBytes = new();
    
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
        
        if (_inputHandle == nint.Zero || _outputHandle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to get console handles");
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
        return (Console.WindowWidth, Console.WindowHeight);
    }
    
    public int Width => GetWindowSize().width;
    public int Height => GetWindowSize().height;
    
    public event Action<int, int>? Resized;
    
    public void EnterRawMode()
    {
        if (_inRawMode) return;
        
        if (!GetConsoleMode(_inputHandle, out _originalInputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for input: {Marshal.GetLastWin32Error()}");
        }
        
        if (!GetConsoleMode(_outputHandle, out _originalOutputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for output: {Marshal.GetLastWin32Error()}");
        }
        
        // Use ReadConsoleInput mode:
        // - Enable window input for resize events
        // - Enable mouse input for mouse events
        // - Disable quick edit (via ENABLE_EXTENDED_FLAGS with no ENABLE_QUICK_EDIT_MODE)
        var newInputMode = ENABLE_WINDOW_INPUT | ENABLE_MOUSE_INPUT | ENABLE_EXTENDED_FLAGS;
        
        if (!SetConsoleMode(_inputHandle, newInputMode))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SetConsoleMode failed for input (error {error}).");
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
        
        _inRawMode = true;
        Console.TreatControlCAsInput = true;
    }
    
    public void ExitRawMode()
    {
        if (!_inRawMode) return;
        
        SetConsoleMode(_inputHandle, _originalInputMode);
        SetConsoleMode(_outputHandle, _originalOutputMode);
        
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
            while (!ct.IsCancellationRequested)
            {
                // First, drain any pending bytes from previous key events
                if (_pendingBytes.Count > 0)
                {
                    return DrainPendingBytes(buffer.Span);
                }
                
                // Wait for input with timeout
                var waitResult = WaitForSingleObject(_inputHandle, 100);
                
                if (waitResult == WAIT_TIMEOUT)
                {
                    continue;
                }
                
                if (waitResult != WAIT_OBJECT_0)
                {
                    throw new InvalidOperationException($"WaitForSingleObject failed: {Marshal.GetLastWin32Error()}");
                }
                
                // Read console input records
                var records = new INPUT_RECORD[16];
                if (!ReadConsoleInput(_inputHandle, records, (uint)records.Length, out var numRead))
                {
                    throw new InvalidOperationException($"ReadConsoleInput failed: {Marshal.GetLastWin32Error()}");
                }
                
                // Process each record
                for (int i = 0; i < numRead; i++)
                {
                    ProcessInputRecord(ref records[i]);
                }
                
                // Return any bytes that were generated
                if (_pendingBytes.Count > 0)
                {
                    return DrainPendingBytes(buffer.Span);
                }
            }
            
            return 0;
        }, ct);
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
        
        // Check for special keys that generate VT sequences
        var vtSequence = GetVtSequence(vk, hasCtrl, hasAlt, hasShift);
        if (vtSequence != null)
        {
            foreach (var b in vtSequence)
            {
                _pendingBytes.Enqueue(b);
            }
            return;
        }
        
        // Handle Ctrl+letter combinations (Ctrl+A = 1, Ctrl+B = 2, etc.)
        if (hasCtrl && !hasAlt && ch >= 'A' - 64 && ch <= 'Z' - 64)
        {
            _pendingBytes.Enqueue((byte)ch);
            return;
        }
        
        // Regular character - encode as UTF-8
        if (ch != 0)
        {
            Span<byte> utf8 = stackalloc byte[4];
            var charSpan = new ReadOnlySpan<char>(in ch);
            var len = Encoding.UTF8.GetBytes(charSpan, utf8);
            for (int i = 0; i < len; i++)
            {
                _pendingBytes.Enqueue(utf8[i]);
            }
        }
    }
    
    private byte[]? GetVtSequence(ushort vk, bool ctrl, bool alt, bool shift)
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
            VK_BACK => [(byte)(ctrl ? 0x7F : 0x08)],
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
        FlushConsoleInputBuffer(_inputHandle);
    }
    
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
