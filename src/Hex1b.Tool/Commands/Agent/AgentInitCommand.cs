using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Agent;

/// <summary>
/// Drops a GitHub Copilot skill file into the repo so agents know how to use the Hex1b CLI.
/// </summary>
internal sealed class AgentInitCommand : BaseCommand
{
    private static readonly Option<string?> s_pathOption = new("--path") { Description = "Explicit repo root path (skips auto-detection)" };
    private static readonly Option<bool> s_stdoutOption = new("--stdout") { Description = "Write skill file to stdout instead of disk" };
    private static readonly Option<bool> s_forceOption = new("--force") { Description = "Overwrite existing skill file" };

    public AgentInitCommand(
        OutputFormatter formatter,
        ILogger<AgentInitCommand> logger)
        : base("init", "Initialize Hex1b agent skill file in a repository", formatter, logger)
    {
        Options.Add(s_pathOption);
        Options.Add(s_stdoutOption);
        Options.Add(s_forceOption);
    }

    protected override Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var explicitPath = parseResult.GetValue(s_pathOption);
        var toStdout = parseResult.GetValue(s_stdoutOption);
        var force = parseResult.GetValue(s_forceOption);

        var skillContent = GetSkillContent();

        if (toStdout)
        {
            Console.Write(skillContent);
            return Task.FromResult(0);
        }

        // Resolve repo root
        string repoRoot;
        if (explicitPath != null)
        {
            repoRoot = Path.GetFullPath(explicitPath);
            if (!Directory.Exists(repoRoot))
            {
                Formatter.WriteError($"Path does not exist: {repoRoot}");
                return Task.FromResult(1);
            }
        }
        else
        {
            var detected = DetectRepoRoot(Directory.GetCurrentDirectory());
            if (detected == null)
            {
                Formatter.WriteError("Could not detect repository root (no .git found). Use --path or --stdout.");
                return Task.FromResult(1);
            }
            repoRoot = detected;
        }

        var skillDir = Path.Combine(repoRoot, ".github", "skills", "hex1b");
        var skillFile = Path.Combine(skillDir, "SKILL.md");

        if (File.Exists(skillFile) && !force)
        {
            Formatter.WriteError($"Skill file already exists: {skillFile}");
            Formatter.WriteError("Use --force to overwrite.");
            return Task.FromResult(1);
        }

        Directory.CreateDirectory(skillDir);
        File.WriteAllText(skillFile, skillContent);

        if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new { path = skillFile, repoRoot });
        }
        else
        {
            Formatter.WriteLine($"Created {Path.GetRelativePath(repoRoot, skillFile)}");
        }

        return Task.FromResult(0);
    }

    private static string? DetectRepoRoot(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string GetSkillContent() => """
        ---
        name: hex1b
        description: CLI tool for automating any terminal application — TUI apps, shells, CLI tools, REPLs, and more. Use when you need to launch a process in a virtual terminal, capture its screen output, inject keystrokes or mouse input, or assert on what's visible.
        ---

        # Hex1b CLI Skill

        The `dotnet hex1b` CLI tool lets you automate **any terminal application** — TUI apps,
        interactive CLIs, shells, REPLs, curses programs, or anything else that runs in a terminal.
        It wraps arbitrary processes in a headless virtual terminal, giving you programmatic control
        over screen capture, input injection, and content assertions.

        Use it to:
        - Launch any command in a virtual terminal and interact with it programmatically
        - Capture what's on screen as text, ANSI, or SVG at any point
        - Send keystrokes, text, and mouse events
        - Assert on visible content (useful for CI and scripted testing)
        - Inspect Hex1b TUI widget trees (Hex1b apps only)

        ## Installation

        The tool is distributed as a .NET local tool. Install it in the repo:

        ```bash
        dotnet tool install Hex1b.Tool
        ```

        Or restore if already in the manifest:

        ```bash
        dotnet tool restore
        ```

        ## Concepts

        A **terminal** is a headless virtual terminal managed by Hex1b. Any process that runs in a
        terminal emulator can be launched inside one — shells, TUI apps, CLI tools, REPLs, compilers,
        test runners, etc. Terminals are identified by a short numeric ID (the process ID).

        There are two kinds:

        - **Hosted terminals** — Any process spawned by the CLI inside a headless virtual terminal. This is the primary way to automate arbitrary programs.
        - **TUI apps** — Hex1b applications with diagnostics enabled (`WithDiagnostics()`). Discovered automatically; support additional features like widget tree inspection.

        All commands that target a terminal take an `<id>` argument. Use a prefix if unambiguous.

        ## Global Options

        - `--json` — Output results as JSON (available on all commands)
        - `--help` — Show help for any command

        ## Commands

        ### Terminal Lifecycle

        ```bash
        # List all known terminals (auto-cleans stale sockets)
        dotnet hex1b terminal list

        # Start a hosted terminal running a command
        dotnet hex1b terminal start [--width 120] [--height 30] [--cwd <path>] -- <command> [args...]

        # Stop a hosted terminal
        dotnet hex1b terminal stop <id>

        # Show terminal details (name, PID, dimensions, uptime)
        dotnet hex1b terminal info <id>

        # Resize a terminal
        dotnet hex1b terminal resize <id> --width <n> --height <n>

        # Remove stale socket files
        dotnet hex1b terminal clean

        # Attach to a terminal (interactive — streams output, forwards input, Ctrl+] to detach)
        dotnet hex1b terminal attach <id>
        ```

        ### Screen Capture

        ```bash
        # Capture terminal screen (formats: text, ansi, svg)
        dotnet hex1b capture <id> [--format text|ansi|svg] [--output <file>]

        # Wait for text to appear before capturing
        dotnet hex1b capture <id> --wait "Ready" --timeout 30
        ```

        ### Input Injection

        ```bash
        # Send a named key (Enter, Tab, Escape, F1, ArrowUp, etc.)
        dotnet hex1b keys <id> --key Enter

        # Send text as keystrokes
        dotnet hex1b keys <id> --text "hello world"

        # Send key with modifiers
        dotnet hex1b keys <id> --key C --ctrl

        # Click at coordinates (0-based column, row)
        dotnet hex1b mouse click <id> <x> <y> [--button left|right|middle]
        ```

        ### Assertions (for CI and scripting)

        ```bash
        # Assert text is visible on screen (waits up to --timeout seconds)
        dotnet hex1b assert <id> --text-present "Welcome"

        # Assert text is NOT visible
        dotnet hex1b assert <id> --text-absent "Error" --timeout 10
        ```

        ### Widget Tree Inspection (TUI apps only)

        ```bash
        # Inspect the widget/node tree
        dotnet hex1b app tree <id> [--focus] [--popups] [--depth <n>]
        ```

        ## Common Workflows

        ### Automate any CLI or TUI application

        ```bash
        # Launch htop in a virtual terminal
        dotnet hex1b terminal start -- htop

        # Launch a Python REPL
        dotnet hex1b terminal start -- python3

        # Launch vim editing a file
        dotnet hex1b terminal start --width 80 --height 24 -- vim myfile.txt

        # Get the terminal ID
        dotnet hex1b terminal list
        ```

        ### Interact with a running program

        ```bash
        # Type into a shell or REPL
        dotnet hex1b keys <id> --text "print('hello')"
        dotnet hex1b keys <id> --key Enter

        # Wait for output to appear, then capture it
        dotnet hex1b assert <id> --text-present "hello"
        dotnet hex1b capture <id> --format text
        ```

        ### Launch and test a Hex1b TUI app

        ```bash
        # Start the app in a hosted terminal
        dotnet hex1b terminal start -- dotnet run --project samples/MyApp

        # List to get the ID
        dotnet hex1b terminal list

        # Wait for it to be ready, then interact
        dotnet hex1b assert <id> --text-present "Ready"
        dotnet hex1b keys <id> --key Tab
        dotnet hex1b capture <id> --format text
        ```

        ### Scripted testing

        ```bash
        # Start, interact, assert, stop
        ID=$(dotnet hex1b terminal start --json -- dotnet run --project src/MyApp | jq -r .id)
        dotnet hex1b assert $ID --text-present "Main Menu" --timeout 15
        dotnet hex1b keys $ID --key Enter
        dotnet hex1b assert $ID --text-present "Settings"
        dotnet hex1b capture $ID --format svg --output screenshot.svg
        dotnet hex1b terminal stop $ID
        ```

        ## Tips

        - Use `--json` with `jq` for scriptable output (e.g., `dotnet hex1b terminal list --json | jq '.[] | .id'`)
        - Terminal IDs are PIDs — you can use a unique prefix instead of the full number
        - `terminal list` automatically cleans up stale sockets from exited processes
        - `capture --wait` is useful for waiting for async rendering before taking a screenshot
        - Key names match .NET's `ConsoleKey` enum: `Enter`, `Tab`, `Escape`, `Backspace`, `UpArrow`, `DownArrow`, `LeftArrow`, `RightArrow`, `F1`–`F12`, `Home`, `End`, `PageUp`, `PageDown`, `Delete`, `Insert`, `Spacebar`, and single letters `A`–`Z`
        """;
}
