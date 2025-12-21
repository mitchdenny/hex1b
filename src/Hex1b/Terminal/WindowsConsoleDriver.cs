using System.Runtime.InteropServices;
using System.Text;

namespace Hex1b.Terminal;

/// <summary>
/// Windows console driver targeting ConPTY/Windows Terminal with VT sequence support.
/// </summary>
/// <remarks>
/// This driver targets modern Windows terminals (Windows Terminal, VS Code terminal, etc.)
/// that support ConPTY and native VT sequence processing. It enables VT input mode so
/// the terminal sends VT escape sequences directly, similar to Unix terminals.
/// 
/// Requirements: Windows 10 1809+ or Windows 11 with a VT-capable terminal.
/// </remarks>
internal sealed class WindowsConsoleDriver : IConsoleDriver
{
    // Standard handles
    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    
    // Console mode flags - Input
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;        // Ctrl+C processed by system
    private const uint ENABLE_LINE_INPUT = 0x0002;             // ReadFile waits for Enter
    private const uint ENABLE_ECHO_INPUT = 0x0004;             // Characters echoed
    private const uint ENABLE_WINDOW_INPUT = 0x0008;           // Window buffer size changes reported
    private const uint ENABLE_MOUSE_INPUT = 0x0010;            // Mouse events reported (legacy)
    private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;        // Quick edit mode (mouse for selection)
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;         // Required for ENABLE_QUICK_EDIT_MODE
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200; // VT input sequences (ConPTY)
    
    // Console mode flags - Output
    private const uint ENABLE_PROCESSED_OUTPUT = 0x0001;              // Process special chars
    private const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;            // Wrap at line end
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;   // VT100 sequences (ConPTY)
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;          // No auto CR for LF
    
    // Wait constants
    private const uint WAIT_OBJECT_0 = 0;
    private const uint WAIT_TIMEOUT = 0x00000102;
    
    private nint _inputHandle;
    private nint _outputHandle;
    private uint _originalInputMode;
    private uint _originalOutputMode;
    private bool _inRawMode;
    private bool _disposed;
    private int _lastWidth;
    private int _lastHeight;
    
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
        
        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;
    }
    
    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;
    
    public event Action<int, int>? Resized;
    
    public void EnterRawMode()
    {
        if (_inRawMode) return;
        
        // Save original modes
        if (!GetConsoleMode(_inputHandle, out _originalInputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for input: {Marshal.GetLastWin32Error()}");
        }
        
        if (!GetConsoleMode(_outputHandle, out _originalOutputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for output: {Marshal.GetLastWin32Error()}");
        }
        
        // Set up VT input mode for ConPTY:
        // - Disable line input (no waiting for Enter)
        // - Disable echo
        // - Disable Ctrl+C processing (we handle it via VT sequences)
        // - Disable quick edit mode (interferes with mouse)
        // - Enable VT input (terminal sends VT sequences directly)
        var newInputMode = ENABLE_VIRTUAL_TERMINAL_INPUT | ENABLE_EXTENDED_FLAGS;
        
        if (!SetConsoleMode(_inputHandle, newInputMode))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SetConsoleMode failed for input (error {error}). " +
                "This driver requires Windows 10 1809+ with a VT-capable terminal (Windows Terminal, VS Code, etc.).");
        }
        
        // Set up VT output mode for ConPTY
        var newOutputMode = ENABLE_PROCESSED_OUTPUT | 
                           ENABLE_WRAP_AT_EOL_OUTPUT | 
                           ENABLE_VIRTUAL_TERMINAL_PROCESSING |
                           DISABLE_NEWLINE_AUTO_RETURN;
        
        if (!SetConsoleMode(_outputHandle, newOutputMode))
        {
            // Restore input mode and fail
            SetConsoleMode(_inputHandle, _originalInputMode);
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SetConsoleMode failed for output (error {error}). " +
                "This driver requires Windows 10 1809+ with VT sequence support.");
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
            
            // Check if console has pending input
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
                // Wait for input with timeout to allow cancellation checks
                var waitResult = WaitForSingleObject(_inputHandle, 100); // 100ms timeout
                
                if (waitResult == WAIT_TIMEOUT)
                {
                    // Check for resize while waiting
                    CheckResize();
                    continue;
                }
                
                if (waitResult != WAIT_OBJECT_0)
                {
                    throw new InvalidOperationException($"WaitForSingleObject failed: {Marshal.GetLastWin32Error()}");
                }
                
                // With VT input mode, we can read raw bytes directly
                // The terminal sends VT sequences for special keys and mouse
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
                            // ERROR_IO_PENDING (997) is expected for async, but we're sync here
                            if (error != 0)
                            {
                                throw new InvalidOperationException($"ReadFile failed: {error}");
                            }
                        }
                    }
                }
            }
            
            return 0; // Cancelled
        }, ct);
    }
    
    private void CheckResize()
    {
        var newWidth = Console.WindowWidth;
        var newHeight = Console.WindowHeight;
        
        if (newWidth != _lastWidth || newHeight != _lastHeight)
        {
            _lastWidth = newWidth;
            _lastHeight = newHeight;
            Resized?.Invoke(newWidth, newHeight);
        }
    }
    
    public void Write(ReadOnlySpan<byte> data)
    {
        // With VT processing enabled, we can write raw bytes containing VT sequences
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
        // Console output is typically unbuffered, but flush just in case
        FlushFileBuffers(_outputHandle);
    }
    
    public void DrainInput()
    {
        if (!_inRawMode) return;
        
        // Flush the console input buffer
        FlushConsoleInputBuffer(_inputHandle);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ExitRawMode();
        // Handles are pseudo-handles from GetStdHandle, no need to close
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
}
