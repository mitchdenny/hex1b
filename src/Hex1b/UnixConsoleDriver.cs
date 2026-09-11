using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Hex1b;

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

    /// <inheritdoc />
    public bool TryGetWindowPixelSize(out int pixelWidth, out int pixelHeight)
    {
        if (UnixTerminalInterop.GetWindowPixelSize(STDOUT_FILENO, out pixelWidth, out pixelHeight) == 0 &&
            pixelWidth > 0 && pixelHeight > 0)
            return true;
        pixelWidth = pixelHeight = 0;
        return false;
    }
    public Encoding InputEncoding => Console.InputEncoding;
    
    public event Action<int, int>? Resized;
    
    public void EnterRawMode(bool preserveOPost = false)
    {
        if (_inRawMode) return;
        
        // Get current termios settings for stdin
        _originalTermios = new byte[UnixTerminalInterop.TermiosSize];
        var result = UnixTerminalInterop.GetTermios(STDIN_FILENO, _originalTermios);
        if (result != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"tcgetattr failed with errno {errno}");
        }
        
        // Copy and use cfmakeraw() directly - this is the canonical way and matches SimplePty
        // cfmakeraw() clears OPOST which disables output post-processing (no LF->CRLF conversion)
        var rawTermios = (byte[])_originalTermios.Clone();
        result = UnixTerminalInterop.MakeRaw(rawTermios, preserveOPost);
        if (result != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"cfmakeraw failed with errno {errno}");
        }
        
        // Apply to stdin (for input handling)
        result = UnixTerminalInterop.SetTermios(STDIN_FILENO, rawTermios);
        if (result != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"tcsetattr on STDIN failed with errno {errno}");
        }
        
        // Also apply to stdout to ensure OPOST is disabled for output
        // This ensures LF bytes pass through unchanged (no ONLCR conversion)
        result = UnixTerminalInterop.SetTermios(STDOUT_FILENO, rawTermios);
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
        UnixTerminalInterop.SetTermios(STDIN_FILENO, _originalTermios);
        UnixTerminalInterop.SetTermios(STDOUT_FILENO, _originalTermios);
        _inRawMode = false;
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
