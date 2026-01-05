using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace Hex1b.Terminal;

/// <summary>
/// Windows PTY implementation using ConPTY APIs.
/// </summary>
/// <remarks>
/// Requires Windows 10 version 1809 (build 17763) or later.
/// Uses the Windows Pseudo Console (ConPTY) API to create a pseudo-terminal
/// that can host console applications like pwsh.exe, cmd.exe, etc.
/// 
/// I/O is handled via background threads that perform blocking reads/writes
/// on the synchronous pipe handles, bridged to async consumers via Channels.
/// </remarks>
internal sealed class WindowsPtyHandle : IPtyHandle
{
    // === P/Invoke Constants ===
    
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    private const uint INFINITE = 0xFFFFFFFF;
    private const uint WAIT_OBJECT_0 = 0;
    private const uint STILL_ACTIVE = 259;
    
    // === P/Invoke Structures ===
    
    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEXW
    {
        public STARTUPINFOW StartupInfo;
        public IntPtr lpAttributeList;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOW
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
    
    // === P/Invoke Functions ===
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(
        COORD size,
        SafeFileHandle hInput,
        SafeFileHandle hOutput,
        uint dwFlags,
        out IntPtr phPC);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr hPC);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(
        out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe,
        ref SECURITY_ATTRIBUTES lpPipeAttributes,
        int nSize);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(
        SafeFileHandle hObject,
        int dwMask,
        int dwFlags);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr Attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEXW lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
    
    // === Instance Fields ===
    
    private IntPtr _hPC = IntPtr.Zero;              // Pseudo console handle
    private IntPtr _hProcess = IntPtr.Zero;         // Child process handle
    private IntPtr _hThread = IntPtr.Zero;          // Child main thread handle
    private int _processId;
    
    private SafeFileHandle? _pipeOurInputWrite;     // We write to child's stdin
    private SafeFileHandle? _pipePtyOutputRead;     // We read from child's stdout
    
    private FileStream? _writeStream;               // Sync stream to write input to child
    private FileStream? _readStream;                // Sync stream to read output from child
    
    // Channel-based async I/O bridge
    private Channel<byte[]>? _outputChannel;        // PTY output -> async consumer
    private Channel<byte[]>? _inputChannel;         // Async producer -> PTY input
    private Thread? _readThread;                    // Background thread for blocking reads
    private Thread? _writeThread;                   // Background thread for blocking writes
    private CancellationTokenSource? _cts;          // For signaling shutdown
    
    private bool _disposed;
    
    // === IPtyHandle Implementation ===
    
    public int ProcessId => _processId;
    
    public Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsPtyHandle));
        
        // Create pipes for ConPTY
        // Pipe 1: PTY reads from this (child's stdin) - we write to pipeOurInputWrite
        // Pipe 2: PTY writes to this (child's stdout) - we read from pipePtyOutputRead
        
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1,
            lpSecurityDescriptor = IntPtr.Zero
        };
        
        // Create pipe for PTY input (child stdin)
        if (!CreatePipe(out var pipePtyInputRead, out _pipeOurInputWrite, ref sa, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create input pipe");
        
        // Create pipe for PTY output (child stdout)
        if (!CreatePipe(out _pipePtyOutputRead, out var pipePtyOutputWrite, ref sa, 0))
        {
            pipePtyInputRead.Dispose();
            _pipeOurInputWrite.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create output pipe");
        }
        
        try
        {
            // Create the pseudo console
            var size = new COORD { X = (short)width, Y = (short)height };
            int hr = CreatePseudoConsole(size, pipePtyInputRead, pipePtyOutputWrite, 0, out _hPC);
            if (hr != 0)
                throw new Win32Exception(hr, "Failed to create pseudo console");
            
            // Close the pipe ends that ConPTY now owns
            pipePtyInputRead.Dispose();
            pipePtyOutputWrite.Dispose();
            
            // Initialize the process thread attribute list
            var attrListSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
            
            var attrList = Marshal.AllocHGlobal(attrListSize);
            try
            {
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to initialize attribute list");
                
                // Add the pseudo console to the attribute list
                if (!UpdateProcThreadAttribute(
                    attrList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    _hPC,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to update attribute list");
                }
                
                // Build the command line
                var cmdLine = new StringBuilder();
                cmdLine.Append('"').Append(fileName).Append('"');
                foreach (var arg in arguments)
                {
                    cmdLine.Append(' ');
                    if (arg.Contains(' ') || arg.Contains('"'))
                    {
                        cmdLine.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
                    }
                    else
                    {
                        cmdLine.Append(arg);
                    }
                }
                
                // Build environment block
                var envBlock = BuildEnvironmentBlock(environment);
                var envPtr = IntPtr.Zero;
                if (envBlock != null)
                {
                    envPtr = Marshal.StringToHGlobalUni(envBlock);
                }
                
                try
                {
                    // Setup startup info
                    var startupInfo = new STARTUPINFOEXW
                    {
                        StartupInfo = new STARTUPINFOW
                        {
                            cb = Marshal.SizeOf<STARTUPINFOEXW>()
                        },
                        lpAttributeList = attrList
                    };
                    
                    // Create the process
                    if (!CreateProcessW(
                        null,
                        cmdLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                        envPtr,
                        workingDirectory,
                        ref startupInfo,
                        out var processInfo))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create process");
                    }
                    
                    _hProcess = processInfo.hProcess;
                    _hThread = processInfo.hThread;
                    _processId = processInfo.dwProcessId;
                }
                finally
                {
                    if (envPtr != IntPtr.Zero)
                        Marshal.FreeHGlobal(envPtr);
                }
            }
            finally
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }
            
            // Create synchronous streams for blocking I/O
            _writeStream = new FileStream(_pipeOurInputWrite, FileAccess.Write, bufferSize: 4096, isAsync: false);
            _readStream = new FileStream(_pipePtyOutputRead, FileAccess.Read, bufferSize: 4096, isAsync: false);
            
            // Create channels for async bridge
            _outputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(16)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            
            _inputChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(16)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            
            _cts = new CancellationTokenSource();
            
            // Start background read thread (blocking read -> channel)
            _readThread = new Thread(ReadThreadProc)
            {
                IsBackground = true,
                Name = "ConPTY Read Thread"
            };
            _readThread.Start();
            
            // Start background write thread (channel -> blocking write)
            _writeThread = new Thread(WriteThreadProc)
            {
                IsBackground = true,
                Name = "ConPTY Write Thread"
            };
            _writeThread.Start();
            
            return Task.CompletedTask;
        }
        catch
        {
            // Cleanup on failure
            _pipeOurInputWrite?.Dispose();
            _pipePtyOutputRead?.Dispose();
            if (_hPC != IntPtr.Zero)
            {
                ClosePseudoConsole(_hPC);
                _hPC = IntPtr.Zero;
            }
            throw;
        }
    }
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
    {
        if (_disposed || _outputChannel == null)
            return ReadOnlyMemory<byte>.Empty;
        
        try
        {
            // Read from the channel (populated by background read thread)
            if (await _outputChannel.Reader.WaitToReadAsync(ct))
            {
                if (_outputChannel.Reader.TryRead(out var data))
                    return data;
            }
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (ChannelClosedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }
    
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_disposed || _inputChannel == null)
            return;
        
        try
        {
            // Write to the channel (consumed by background write thread)
            await _inputChannel.Writer.WriteAsync(data.ToArray(), ct);
        }
        catch (ChannelClosedException)
        {
            // Channel closed, ignore
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }
    
    public void Resize(int width, int height)
    {
        if (_disposed || _hPC == IntPtr.Zero)
            return;
        
        var size = new COORD { X = (short)width, Y = (short)height };
        ResizePseudoConsole(_hPC, size);
    }
    
    public void Kill(int signal)
    {
        if (_disposed || _hProcess == IntPtr.Zero)
            return;
        
        // On Windows, we just terminate the process (signal is ignored)
        TerminateProcess(_hProcess, 1);
    }
    
    public Task<int> WaitForExitAsync(CancellationToken ct)
    {
        if (_disposed || _hProcess == IntPtr.Zero)
            return Task.FromResult(-1);
        
        return Task.Run(() =>
        {
            // Poll for process exit with cancellation support
            while (!ct.IsCancellationRequested)
            {
                uint waitResult = WaitForSingleObject(_hProcess, 100);
                if (waitResult == WAIT_OBJECT_0)
                {
                    if (GetExitCodeProcess(_hProcess, out uint exitCode))
                        return (int)exitCode;
                    return -1;
                }
            }
            
            ct.ThrowIfCancellationRequested();
            return -1;
        }, ct);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        // Signal shutdown to background threads
        _cts?.Cancel();
        
        // Close channels to unblock threads waiting on channel operations
        _outputChannel?.Writer.TryComplete();
        _inputChannel?.Writer.TryComplete();
        
        // Close streams FIRST to unblock blocking Read()/Write() calls in threads
        // This causes the blocking I/O to throw, allowing threads to exit
        if (_writeStream != null)
        {
            try { _writeStream.Close(); } catch { }
            _writeStream = null;
        }
        
        if (_readStream != null)
        {
            try { _readStream.Close(); } catch { }
            _readStream = null;
        }
        
        // Close pipe handles to ensure threads are unblocked
        _pipeOurInputWrite?.Dispose();
        _pipeOurInputWrite = null;
        _pipePtyOutputRead?.Dispose();
        _pipePtyOutputRead = null;
        
        // Now wait for threads to exit (they should exit quickly now)
        _readThread?.Join(TimeSpan.FromSeconds(2));
        _writeThread?.Join(TimeSpan.FromSeconds(2));
        
        // Dispose CTS
        _cts?.Dispose();
        
        // Close pseudo console
        if (_hPC != IntPtr.Zero)
        {
            ClosePseudoConsole(_hPC);
            _hPC = IntPtr.Zero;
        }
        
        // Close process handles
        if (_hThread != IntPtr.Zero)
        {
            CloseHandle(_hThread);
            _hThread = IntPtr.Zero;
        }
        
        if (_hProcess != IntPtr.Zero)
        {
            CloseHandle(_hProcess);
            _hProcess = IntPtr.Zero;
        }
    }
    
    // === Background Thread Methods ===
    
    private void ReadThreadProc()
    {
        var buffer = new byte[4096];
        try
        {
            while (!_cts!.Token.IsCancellationRequested && _readStream != null)
            {
                int bytesRead;
                try
                {
                    bytesRead = _readStream.Read(buffer, 0, buffer.Length);
                }
                catch (IOException)
                {
                    break; // Pipe closed
                }
                catch (ObjectDisposedException)
                {
                    break; // Stream disposed
                }
                
                if (bytesRead == 0)
                    break; // EOF
                
                // Copy data and write to channel
                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);
                
                // Block until channel accepts the data (provides backpressure)
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (_outputChannel!.Writer.TryWrite(data))
                        break;
                    
                    // Wait a bit and retry
                    Thread.Sleep(1);
                }
            }
        }
        finally
        {
            _outputChannel?.Writer.TryComplete();
        }
    }
    
    private void WriteThreadProc()
    {
        try
        {
            while (!_cts!.Token.IsCancellationRequested && _writeStream != null)
            {
                // Blocking wait for data from channel
                byte[]? data = null;
                try
                {
                    // Use synchronous blocking read from channel
                    var reader = _inputChannel!.Reader;
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        if (reader.TryRead(out data))
                            break;
                        
                        // Wait a bit and retry (simple polling approach)
                        Thread.Sleep(1);
                        
                        // Check if channel is completed
                        if (reader.Completion.IsCompleted)
                            return;
                    }
                }
                catch (ChannelClosedException)
                {
                    break;
                }
                
                if (data == null || _cts.Token.IsCancellationRequested)
                    break;
                
                try
                {
                    _writeStream.Write(data, 0, data.Length);
                    _writeStream.Flush();
                }
                catch (IOException)
                {
                    break; // Pipe closed
                }
                catch (ObjectDisposedException)
                {
                    break; // Stream disposed
                }
            }
        }
        catch
        {
            // Swallow exceptions on shutdown
        }
    }
    
    // === Helper Methods ===
    
    private static string? BuildEnvironmentBlock(Dictionary<string, string> environment)
    {
        if (environment == null || environment.Count == 0)
            return null;
        
        // Environment block is a null-separated list of KEY=VALUE pairs, terminated by double null
        var sb = new StringBuilder();
        foreach (var kvp in environment)
        {
            sb.Append(kvp.Key).Append('=').Append(kvp.Value).Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }
}
