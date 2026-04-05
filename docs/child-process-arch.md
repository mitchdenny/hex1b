# Child Terminal Process Architecture

This document describes the architecture for running child processes with PTY (pseudo-terminal) support in Hex1b.

## Overview

The child process system allows Hex1b to spawn and manage interactive terminal applications (shells, tmux, vim, etc.) with full PTY support. This enables programs that require a controlling terminal to function correctly.

## Key Components

### 1. Native Interop Library (`src/Hex1b/Terminal/native/hex1binterop.c`)

A native C library that handles low-level PTY operations. Required because .NET doesn't expose the necessary PTY APIs.

**Key Functions:**
- `hex1b_forkpty_shell()` - Simple shell spawning using `forkpty()`
- `hex1b_resize()` - Resize the PTY window
- `hex1b_wait()` - Wait for child with timeout

**Important Design Decision:**
The `forkpty()` calls pass `NULL` for the termios parameter instead of copying the parent's termios. This ensures the PTY gets default "cooked mode" settings with echo enabled, even if the parent process has already entered raw mode.

```c
// Pass NULL for termios - let the PTY get default cooked mode settings
pid_t pid = forkpty(&master, NULL, NULL, &ws);
```

**Build Commands:**
```bash
# Build for current platform using Makefile
cd src/Hex1b/Terminal/native && make
```

### 2. UnixPtyHandle (`src/Hex1b/Terminal/UnixPtyHandle.cs`)

C# wrapper around the native library. Provides async read/write operations for the PTY master file descriptor.

**Key Methods:**
- `SpawnAsync()` - Spawns a child process with PTY
- `ReadAsync()` - Non-blocking read using `select()` + `read()`
- `WriteAsync()` - Writes input to the PTY
- `ResizeAsync()` - Sends SIGWINCH to child

**I/O Pattern:**
```
┌─────────────────────────────────────────────────────────────┐
│  UnixPtyHandle                                              │
│  ┌─────────────┐              ┌─────────────────────────┐   │
│  │ ReadAsync() │──select()───>│ PTY Master FD           │   │
│  │             │<──read()─────│ (bidirectional pipe to  │   │
│  └─────────────┘              │  child's stdin/stdout)  │   │
│  ┌─────────────┐              │                         │   │
│  │ WriteAsync()│───write()───>│                         │   │
│  └─────────────┘              └─────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### 3. Windows PTY shim (`src/Hex1b.PtyHost/`)

On Windows, Hex1b uses an out-of-process managed helper (`hex1bpty.exe`) for
`WithPtyProcess(...)`. The helper talks directly to the Win32 ConPTY APIs and
exposes the session over a local Unix-domain socket using the framed shim
protocol:

- launch request (file name, args, cwd, env, cols, rows)
- raw output frames back to Hex1b
- raw input frames from Hex1b
- resize / kill / shutdown control messages
- exit notification with the child exit code

This keeps the public `WithPtyProcess(...)` API unchanged while isolating the
Windows PTY host in a dedicated helper process. For normal repo builds on
Windows, MSBuild publishes a debuggable non-AOT version of `hex1bpty.exe`. For
package production, the same build wiring can switch to a Native AOT publish so
the packaged runtime asset remains self-contained. If the helper is not
available, Hex1b falls back to the existing in-process `WindowsPtyHandle`
implementation so local development and existing tests continue to work.

### 4. Hex1bTerminalChildProcess (`src/Hex1b/Terminal/Hex1bTerminalChildProcess.cs`)

High-level wrapper that implements `IHex1bTerminalWorkloadAdapter`. This is the public API for spawning child processes.

**Usage:**
```csharp
await using var process = new Hex1bTerminalChildProcess(
    "/bin/bash",
    [],
    workingDirectory: Environment.CurrentDirectory,
    inheritEnvironment: true,
    initialWidth: 80,
    initialHeight: 24,
    environment: new Dictionary<string, string> { ["PS1"] = "$ " }
);
```

### 5. Hex1bTerminal (`src/Hex1b/Terminal/Hex1bTerminal.cs`)

The terminal emulator that bridges presentation (user's console) and workload (child process).

**I/O Pump Architecture:**
```
┌──────────────────────────────────────────────────────────────────┐
│  Hex1bTerminal                                                   │
│                                                                  │
│  ┌─────────────────────┐         ┌─────────────────────────┐    │
│  │ Presentation        │         │ Workload                │    │
│  │ (Console)           │         │ (Child Process)         │    │
│  └─────────────────────┘         └─────────────────────────┘    │
│           │                                 │                    │
│           │ ReadInputAsync()                │ ReadOutputAsync()  │
│           ▼                                 ▼                    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              PumpPresentationInputAsync                  │    │
│  │  User keystrokes ──────────────────────> Child stdin     │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              PumpWorkloadOutputAsync                     │    │
│  │  Child stdout ─────> Tokenize ─────> Screen Buffer       │    │
│  │                          │                               │    │
│  │                          └─────> Serialize ─────> Console│    │
│  └─────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

### 6. Console Adapters

**ConsolePresentationAdapter** - Handles raw mode, input reading, and output writing for the user's console.

**UnixConsoleDriver** - Platform-specific raw mode using `cfmakeraw()`.

## Data Flow

### Input Path (User → Child)
1. User types in console
2. `ConsolePresentationAdapter.ReadInputAsync()` reads raw bytes
3. `PumpPresentationInputAsync` forwards to workload
4. `Hex1bTerminalChildProcess.WriteInputAsync()` writes to PTY
5. Child process receives input

### Output Path (Child → User)
1. Child process writes to stdout
2. `UnixPtyHandle.ReadAsync()` reads from PTY master
3. `PumpWorkloadOutputAsync` receives data
4. `AnsiTokenizer.Tokenize()` parses ANSI sequences
5. Tokens applied to internal screen buffer
6. Tokens serialized back to ANSI and sent to console

## Raw Mode Timing

**Critical:** The Hex1bTerminal constructor calls `Start()` which enters raw mode on the presentation adapter. This happens BEFORE the child process is spawned.

```
1. new Hex1bTerminal(options)
   └── Start()
       └── _presentation.EnterRawModeAsync()  ← Console now in raw mode (no echo)
   
2. process.StartAsync()
   └── hex1b_forkpty_shell()  ← PTY spawned with default termios (HAS echo)
```

This is why the native library must NOT copy the parent's termios - the parent is already in raw mode with echo disabled.

## Known Issues / Future Work

Tmux now works correctly, including vertical splits. The fix was to remove `Console.TreatControlCAsInput = true` from the console driver, as this was corrupting terminal state.

### Windows packaging

The Windows PTY helper ships as a packaged runtime asset alongside Hex1b under
`runtimes/win-*/native/hex1bpty.exe`. The repo's MSBuild flow defaults to a
non-AOT helper for local debugging, but package-oriented builds can switch to a
Native AOT publish so the bundled Windows helper behaves like the other native
runtime assets.

For Windows contributors working in the repo:

```powershell
# Default inner-loop/debuggable helper build
dotnet build src/Hex1b/Hex1b.csproj

# Explicit Native AOT helper build
dotnet build src/Hex1b/Hex1b.csproj -p:Hex1bPtyHostPublishMode=Aot
```

`dotnet pack` on Windows automatically prefers the `Aot` mode unless it is
explicitly overridden.

### Mouse Support
Currently keyboard-only. Mouse events would need to be:
1. Captured in presentation adapter
2. Encoded as ANSI mouse sequences
3. Forwarded to workload

## Testing

The `TmuxDemo` sample provides a minimal test harness:

```bash
dotnet run --project samples/TmuxDemo
```

This spawns a bash shell with a custom prompt. Type `tmux` to test tmux functionality.

On Windows, `WindowsPtyDemo` provides the equivalent pass-through harness for the
packaged PTY shim:

```powershell
dotnet run --project samples/WindowsPtyDemo
```

It launches `powershell.exe` via `WithPtyProcess(...)` and will use
`hex1bpty.exe` automatically when that helper is present in the sample output.

## Files Reference

| File | Purpose |
|------|---------|
| `src/Hex1b/Terminal/native/hex1binterop.c` | Native interop library |
| `src/Hex1b/Terminal/UnixPtyHandle.cs` | C# PTY wrapper |
| `src/Hex1b/Terminal/Hex1bTerminalChildProcess.cs` | High-level child process API |
| `src/Hex1b/Terminal/Hex1bTerminal.cs` | Terminal emulator with I/O pumps |
| `src/Hex1b/Terminal/ConsolePresentationAdapter.cs` | Console I/O adapter |
| `src/Hex1b/Terminal/UnixConsoleDriver.cs` | Unix raw mode driver |
| `src/Hex1b/Terminal/AnsiTokenizer.cs` | ANSI sequence parser |
| `samples/TmuxDemo/Program.cs` | Test harness |
| `samples/WindowsPtyDemo/Program.cs` | Windows PTY shim harness |
