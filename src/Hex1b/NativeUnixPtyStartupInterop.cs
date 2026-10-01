using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Hex1b;

internal sealed partial class NativeUnixPtyStartupInterop : IUnixPtyStartupInterop
{
    public void ValidateLibrary()
    {
        if (!NativeLibrary.TryLoad("hex1binterop", typeof(UnixPtyHandle).Assembly, null, out var library))
        {
            throw new InvalidOperationException(
                "Native hex1binterop library not found. Unix PTY operation requires the matching " +
                "libhex1binterop.so (Linux) or libhex1binterop.dylib (macOS) from the Hex1b package.");
        }

        try
        {
            foreach (var export in new[]
            {
                "hex1b_forkpty_shell_env_start", "hex1b_forkpty_exec_env_start",
                "hex1b_poll_startup", "hex1b_abort_startup"
            })
            {
                if (!NativeLibrary.TryGetExport(library, export, out _))
                {
                    throw new InvalidOperationException(
                        $"The native hex1binterop library does not support the required startup handshake " +
                        $"(missing '{export}'). Deploy the native library from the same Hex1b package as the managed assembly.");
                }
            }
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    public UnixPtyStartupHandles Begin(string executable, string[] arguments, string workingDirectory,
        string[] environment, int width, int height)
    {
        int result, master, pid, startup, stage;
        if (arguments.Length == 0)
        {
            result = StartShell(executable, workingDirectory, environment, width, height,
                out master, out pid, out startup, out stage);
        }
        else
        {
            var argv = new string[arguments.Length + 2];
            argv[0] = executable;
            arguments.CopyTo(argv, 1);
            result = StartExec(executable, argv, arguments.Length + 1, workingDirectory, environment,
                width, height, out master, out pid, out startup, out stage);
        }

        var error = Marshal.GetLastPInvokeError();
        if (result < 0)
            throw CreateStartupException(executable, workingDirectory, stage, error);

        return new(master, pid, startup);
    }

    public int Poll(int startupFd, int timeoutMilliseconds, ref UnixPtyStartupState state, out int stage, out int error)
    {
        var result = PollStartup(startupFd, timeoutMilliseconds, ref state, out stage);
        error = Marshal.GetLastPInvokeError();
        return result;
    }

    public void CloseStartup(int startupFd)
    {
        if (Close(startupFd) < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new Win32Exception(error, $"Failed to close the Unix PTY startup channel (errno {error}).");
        }
    }

    public void Abort(UnixPtyStartupHandles handles)
    {
        if (AbortStartup(handles.MasterFd, handles.ChildPid, handles.StartupFd) < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new Win32Exception(error, $"Failed to clean up the Unix PTY child (errno {error}).");
        }
    }

    internal static InvalidOperationException CreateStartupException(string executable, string workingDirectory,
        int stage, int error)
    {
        var cause = new Win32Exception(error);
        var operation = stage switch
        {
            2 => "could not enter the working directory",
            3 => "could not execute the target",
            4 => "the startup handshake failed or the child exited before confirming startup",
            _ => "could not initialize the PTY"
        };
        var execution = stage is 2 or 3 ? " The target was not executed." : "";
        return new InvalidOperationException(
            $"Cannot start '{executable}' in working directory '{workingDirectory}': {operation} " +
            $"(errno {error}: {cause.Message}).{execution}", cause);
    }

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_forkpty_shell_env_start", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int StartShell(string executable, string workingDirectory,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] environment,
        int width, int height, out int masterFd, out int childPid, out int startupFd, out int stage);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_forkpty_exec_env_start", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int StartExec(string executable,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] argv, int argc,
        string workingDirectory,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] environment,
        int width, int height, out int masterFd, out int childPid, out int startupFd, out int stage);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_poll_startup", SetLastError = true)]
    private static partial int PollStartup(int startupFd, int timeoutMilliseconds, ref UnixPtyStartupState state, out int stage);

    [LibraryImport("hex1binterop", EntryPoint = "hex1b_abort_startup", SetLastError = true)]
    private static partial int AbortStartup(int masterFd, int childPid, int startupFd);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fd);
}
