using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hex1b.Terminal;

/// <summary>
/// Unix (Linux/macOS) console driver using termios for raw mode.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class UnixConsoleDriver : IConsoleDriver
{
    // File descriptors
    private const int STDIN_FILENO = 0;
    private const int STDOUT_FILENO = 1;
    
    // termios c_lflag bits (these are consistent across platforms)
    private const uint ICANON = 0x00000002;  // Canonical mode
    private const uint ECHO   = 0x00000008;  // Echo input
    private const uint ECHONL = 0x00000040;  // Echo NL even if ECHO is off
    private const uint ISIG   = 0x00000001;  // Enable signals (INTR, QUIT, SUSP)
    private const uint IEXTEN = 0x00008000;  // Extended input processing
    
    // termios c_iflag bits - these match cfmakeraw
    private const uint IGNBRK = 0x00000001;  // Ignore BREAK condition
    private const uint BRKINT = 0x00000002;  // Signal on break
    private const uint PARMRK = 0x00000008;  // Mark parity errors
    private const uint INPCK  = 0x00000010;  // Parity checking
    private const uint ISTRIP = 0x00000020;  // Strip 8th bit
    private const uint INLCR  = 0x00000040;  // Map NL to CR
    private const uint IGNCR  = 0x00000080;  // Ignore CR
    private const uint ICRNL  = 0x00000100;  // Map CR to NL
    private const uint IXON   = 0x00000400;  // Enable XON/XOFF flow control
    
    // termios c_oflag bits
    private const uint OPOST  = 0x00000001;  // Post-process output
    
    // termios c_cflag bits
    private const uint CSIZE  = 0x00000030;  // Character size mask
    private const uint PARENB = 0x00000100;  // Enable parity
    private const uint CS8    = 0x00000030;  // 8-bit chars
    
    // tcsetattr actions
    private const int TCSAFLUSH = 2;  // Flush and set
    
    // Offsets in termios struct - Linux x64
    private const int TERMIOS_SIZE = 60;
    private const int IFLAG_OFFSET = 0;
    private const int OFLAG_OFFSET = 4;
    private const int CFLAG_OFFSET = 8;
    private const int LFLAG_OFFSET = 12;
    
    // poll() constants
    private const short POLLIN = 0x0001;
    
    private byte[]? _originalTermios;
    private bool _inRawMode;
    private PosixSignalRegistration? _sigwinchRegistration;
    private int _lastWidth;
    private int _lastHeight;
    private bool _disposed;
    
    public UnixConsoleDriver()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("UnixConsoleDriver only works on Linux and macOS");
        }
        
        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;
        
        // Register for SIGWINCH
        _sigwinchRegistration = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, OnSigwinch);
    }
    
    private void OnSigwinch(PosixSignalContext context)
    {
        context.Cancel = false;
        
        var newWidth = Console.WindowWidth;
        var newHeight = Console.WindowHeight;
        
        if (newWidth != _lastWidth || newHeight != _lastHeight)
        {
            _lastWidth = newWidth;
            _lastHeight = newHeight;
            Resized?.Invoke(newWidth, newHeight);
        }
    }
    
    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;
    
    public event Action<int, int>? Resized;
    
    public void EnterRawMode(bool preserveOPost = false)
    {
        if (_inRawMode) return;
        
        // Get current termios settings for stdin
        _originalTermios = new byte[TERMIOS_SIZE];
        var result = tcgetattr(STDIN_FILENO, _originalTermios);
        if (result != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"tcgetattr failed with errno {errno}");
        }
        
        // Copy and use cfmakeraw() directly - this is the canonical way and matches SimplePty
        // cfmakeraw() clears OPOST which disables output post-processing (no LF->CRLF conversion)
        var rawTermios = (byte[])_originalTermios.Clone();
        cfmakeraw(rawTermios);
        
        // If preserveOPost is requested, re-enable OPOST for output post-processing (LF→CRLF)
        // This is useful for WithProcess scenarios where child programs expect normal output handling
        if (preserveOPost)
        {
            ModifyFlag(rawTermios, OFLAG_OFFSET, OPOST, clear: false);
        }
        cfmakeraw(rawTermios);
        
        // Apply to stdin (for input handling)
        result = tcsetattr(STDIN_FILENO, TCSAFLUSH, rawTermios);
        if (result != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"tcsetattr on STDIN failed with errno {errno}");
        }
        
        // Also apply to stdout to ensure OPOST is disabled for output
        // This ensures LF bytes pass through unchanged (no ONLCR conversion)
        result = tcsetattr(STDOUT_FILENO, TCSAFLUSH, rawTermios);
        if (result != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"tcsetattr on STDOUT failed with errno {errno}");
        }
        
        _inRawMode = true;
        
        // NOTE: Do NOT use Console.TreatControlCAsInput here!
        // Setting this property corrupts the terminal state and breaks programs like tmux.
        // cfmakeraw() already disables ISIG, so Ctrl+C comes through as raw 0x03 byte.
    }
    
    public void ExitRawMode()
    {
        if (!_inRawMode || _originalTermios == null) return;
        
        // Restore original termios settings for both stdin and stdout
        tcsetattr(STDIN_FILENO, TCSAFLUSH, _originalTermios);
        tcsetattr(STDOUT_FILENO, TCSAFLUSH, _originalTermios);
        _inRawMode = false;
    }
    
    private static void ModifyFlag(byte[] termios, int offset, uint flag, bool clear)
    {
        var current = BitConverter.ToUInt32(termios, offset);
        if (clear)
            current &= ~flag;
        else
            current |= flag;
        BitConverter.GetBytes(current).CopyTo(termios, offset);
    }
    
    public bool DataAvailable
    {
        get
        {
            if (!_inRawMode) return false;
            
            // Use poll() to check if stdin has data
            var pfd = new PollFd { fd = STDIN_FILENO, events = POLLIN, revents = 0 };
            var result = poll(ref pfd, 1, 0); // timeout=0 for non-blocking check
            return result > 0 && (pfd.revents & POLLIN) != 0;
        }
    }
    
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!_inRawMode)
        {
            throw new InvalidOperationException("Must enter raw mode before reading");
        }
        
        // Use Task.Run to offload the blocking read() call to a thread pool thread
        // This allows proper cancellation handling
        return await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                // Poll with a short timeout to allow cancellation checks
                var pfd = new PollFd { fd = STDIN_FILENO, events = POLLIN, revents = 0 };
                var pollResult = poll(ref pfd, 1, 100); // 100ms timeout
                
                if (pollResult < 0)
                {
                    var errno = Marshal.GetLastPInvokeError();
                    if (errno == 4) // EINTR - interrupted, retry
                        continue;
                    throw new InvalidOperationException($"poll() failed with errno {errno}");
                }
                
                if (pollResult == 0)
                {
                    // Timeout, check cancellation and retry
                    continue;
                }
                
                if ((pfd.revents & POLLIN) != 0)
                {
                    // Data available, read it
                    unsafe
                    {
                        fixed (byte* ptr = buffer.Span)
                        {
                            var bytesRead = read(STDIN_FILENO, ptr, (nuint)buffer.Length);
                            if (bytesRead < 0)
                            {
                                var errno = Marshal.GetLastPInvokeError();
                                if (errno == 4) // EINTR
                                    continue;
                                throw new InvalidOperationException($"read() failed with errno {errno}");
                            }
                            return (int)bytesRead;
                        }
                    }
                }
            }
            
            return 0; // Cancelled
        }, ct);
    }
    
    public void Write(ReadOnlySpan<byte> data)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var remaining = data.Length;
                var offset = 0;
                while (remaining > 0)
                {
                    var written = write(STDOUT_FILENO, ptr + offset, (nuint)remaining);
                    if (written < 0)
                    {
                        var errno = Marshal.GetLastPInvokeError();
                        if (errno == 4) // EINTR
                            continue;
                        throw new InvalidOperationException($"write() failed with errno {errno}");
                    }
                    offset += (int)written;
                    remaining -= (int)written;
                }
            }
        }
    }
    
    public void Flush()
    {
        // NOTE: Previously called tcdrain() here, but this blocks until all output is transmitted
        // which can cause timing issues with programs like tmux that expect immediate output.
        // The write() syscall already handles buffering at the kernel level.
        // tcdrain(STDOUT_FILENO);
    }
    
    public void DrainInput()
    {
        if (!_inRawMode) return;
        
        // Use poll() to check for and drain any pending input
        var buffer = new byte[256];
        var pfd = new PollFd { fd = STDIN_FILENO, events = POLLIN, revents = 0 };
        
        while (true)
        {
            // Check if data is available (non-blocking)
            var pollResult = poll(ref pfd, 1, 0);
            
            if (pollResult <= 0 || (pfd.revents & POLLIN) == 0)
            {
                // No more data available
                break;
            }
            
            // Read and discard the data
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    var bytesRead = read(STDIN_FILENO, ptr, (nuint)buffer.Length);
                    if (bytesRead <= 0)
                    {
                        break;
                    }
                }
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ExitRawMode();
        _sigwinchRegistration?.Dispose();
    }
    
    // P/Invoke declarations for termios
    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, byte[] termios);
    
    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int actions, byte[] termios);
    
    [DllImport("libc", SetLastError = true)]
    private static extern int tcdrain(int fd);
    
    [DllImport("libc", SetLastError = true)]
    private static extern void cfmakeraw(byte[] termios);
    
    // P/Invoke declarations for direct I/O
    [DllImport("libc", SetLastError = true)]
    private static extern unsafe nint read(int fd, byte* buf, nuint count);
    
    [DllImport("libc", SetLastError = true)]
    private static extern unsafe nint write(int fd, byte* buf, nuint count);
    
    // P/Invoke for poll()
    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }
    
    [DllImport("libc", SetLastError = true)]
    private static extern int poll(ref PollFd fds, nuint nfds, int timeout);
}
