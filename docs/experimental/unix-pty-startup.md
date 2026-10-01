# Experimental Unix PTY startup timeout

`Hex1bTerminalProcessOptions.UnixPtyStartupTimeout` and the init-only
`Hex1bTerminalChildProcess.UnixPtyStartupTimeout` configure the Unix PTY startup
handshake. Both properties carry the experimental diagnostic
`HEX1B_UNIX_PTY_STARTUP`; their name, scope, and policy may change.

The default is **10 seconds**. Any positive `TimeSpan` is accepted, including
sub-millisecond durations. `Timeout.InfiniteTimeSpan` disables automatic
expiration. Zero and all other negative durations throw
`ArgumentOutOfRangeException` when assigned.

The setting affects **Unix PTYs only**. Windows PTY backends retain their existing
startup behavior and ignore it, although values are validated consistently on
all platforms. Redirected `WithProcess` workloads are unaffected.

## Configuration

This complete Unix example gives a PTY process a longer handshake deadline:

```csharp
using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess(options =>
    {
        options.FileName = "/bin/sh";
        options.Arguments = ["-c", "pwd"];
#pragma warning disable HEX1B_UNIX_PTY_STARTUP
        options.UnixPtyStartupTimeout = TimeSpan.FromSeconds(30);
#pragma warning restore HEX1B_UNIX_PTY_STARTUP
    })
    .WithHeadless()
    .Build();

var exitCode = await terminal.RunAsync();
Console.WriteLine($"Exit code: {exitCode}");
```

The builder snapshots the timeout when its configuration callback completes.
Changing a retained options object afterward does not change that timeout.

Direct callers can disable automatic expiration while retaining cancellation:

```csharp
using Hex1b;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

#pragma warning disable HEX1B_UNIX_PTY_STARTUP
await using var process = new Hex1bTerminalChildProcess("/bin/pwd", ["-P"])
{
    UnixPtyStartupTimeout = Timeout.InfiniteTimeSpan
};
#pragma warning restore HEX1B_UNIX_PTY_STARTUP

await process.StartAsync(cancellation.Token);
Console.WriteLine($"Exit code: {await process.WaitForExitAsync(cancellation.Token)}");
```

## What startup confirms

On Unix, success requires the private startup channel to report the pre-exec
marker and then close without a startup error as the close-on-exec descriptor is
closed at exec. EOF without the marker is not success. This is an **exec-boundary
handshake**, not a guarantee that the application remains alive, has displayed a
shell prompt, or is ready to accept requests. A successfully launched target can
still exit immediately with any ordinary exit status.

Changing to the requested working directory happens in the child. A reported
`chdir` failure is fatal: it includes the actual operating-system cause, and the
target is not executed in a fallback directory. Null, empty, and whitespace-only
working directories use the current directory. An embedded NUL is rejected on
Unix rather than silently truncating the path.

Expiration throws `TimeoutException`; caller cancellation throws
`OperationCanceledException`. Waiting reads and writes also observe startup
failure rather than returning a success-shaped result. Failed startup leaves
`HasStarted` and `HasExited` false and `ProcessId` equal to -1.
`WaitForExitAsync` rejects a process that never started. An attempted start is
single-use, including failure: create a new process instance to retry.

Timeout diagnostics identify the handshake, configured and elapsed durations
with units, executable, effective working directory, last observable checkpoint
(no pre-exec marker, or marker received but exec unconfirmed), cleanup outcome,
and the setting to adjust. They do not include the full arguments or environment.
A timeout does **not** establish that `chdir` hung or failed, and does not establish
that the target never ran. Cancellation or timeout racing with exec may terminate
a target that briefly ran. A decoded native error retains precedence; otherwise
caller cancellation takes precedence when cancellation and expiration are
observed together.

## Cancellation and cleanup limits

Infinite waiting still uses the handshake, treats startup errors as fatal, and
honors cancellation and disposal. Disposal before startup releases waiting I/O
with an object-disposed error. Disposal during startup prevents a late successful
publication and waits for startup cleanup.

Failed startup terminates/reaps owned partial children and releases descriptors
before returning where the operating system permits. Cleanup failures are
retained alongside the primary startup error, rather than replacing it.

The timeout is not a hard wall-clock limit on all kernel operations or cleanup:
an uninterruptible kernel operation or delayed termination/reaping can exceed
the configured duration. Diagnostics must describe the actual cleanup outcome,
not claim termination or reaping completed when it did not.
