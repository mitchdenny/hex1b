using System.Runtime.InteropServices;
using Hex1b;

if (!OperatingSystem.IsWindows())
{
    await Console.Error.WriteLineAsync("WindowsPtyDemo requires Windows.");
    return 1;
}

if (args.Length == 1 &&
    (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
{
    await Console.Out.WriteLineAsync("Usage: dotnet run -- [proxy|direct]");
    await Console.Out.WriteLineAsync("  proxy  Run through hex1bpty.exe and fail if the proxy host cannot be used.");
    await Console.Out.WriteLineAsync("  direct Run through the in-process WindowsPtyHandle path.");
    return 0;
}

if (!TryParseMode(args, out var mode))
{
    await Console.Error.WriteLineAsync("Usage: dotnet run -- [proxy|direct]");
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;
var shimPath = TryFindShimPath();
var pwshPath = TryFindPwshPath();

if (pwshPath is null)
{
    await Console.Error.WriteLineAsync(
        "WindowsPtyDemo requires PowerShell 7 (`pwsh.exe`) to be installed and available on PATH.");
    return 1;
}

var windowsPtyMode = mode switch
{
    DemoMode.Proxy => WindowsPtyMode.RequireProxy,
    DemoMode.Direct => WindowsPtyMode.Direct
};

switch (mode)
{
    case DemoMode.Proxy:
        if (shimPath is null)
        {
            await Console.Error.WriteLineAsync(
                "Proxy mode requires hex1bpty.exe, but no shim binary was found in the sample output.");
            return 1;
        }

        await Console.Out.WriteLineAsync(
            $"Launching pwsh.exe through Hex1b PTY in PROXY mode using {Path.GetFileName(shimPath)}. Type 'exit' to quit.");
        break;

    case DemoMode.Direct:
        await Console.Out.WriteLineAsync(
            "Launching pwsh.exe through Hex1b PTY in DIRECT mode using the in-process WindowsPtyHandle. Type 'exit' to quit.");
        break;
}

try
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPtyProcess(options =>
        {
            options.FileName = pwshPath;
            options.Arguments = ["-NoLogo", "-NoProfile"];
            options.WindowsPtyMode = windowsPtyMode;
            if (mode == DemoMode.Proxy)
            {
                options.WindowsPtyHostPath = shimPath;
            }
        })
        .WithDimensions(width, height)
        .Build();

    await terminal.RunAsync();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync("Error running WindowsPtyDemo:");
    await Console.Error.WriteLineAsync(ex.ToString());
    return 1;
}

return 0;

static bool TryParseMode(string[] args, out DemoMode mode)
{
    if (args.Length == 1)
    {
        if (string.Equals(args[0], "proxy", StringComparison.OrdinalIgnoreCase))
        {
            mode = DemoMode.Proxy;
            return true;
        }

        if (string.Equals(args[0], "direct", StringComparison.OrdinalIgnoreCase))
        {
            mode = DemoMode.Direct;
            return true;
        }
    }

    mode = default;
    return false;
}

static string? TryFindShimPath()
{
    var rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
    string[] candidates =
    [
        Path.Combine(AppContext.BaseDirectory, "hex1bpty.exe"),
        Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", "hex1bpty.exe")
    ];

    return candidates.FirstOrDefault(File.Exists);
}

static string? TryFindPwshPath()
{
    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        try
        {
            var candidate = Path.Combine(entry, "pwsh.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        catch (ArgumentException)
        {
        }
    }

    return null;
}

enum DemoMode
{
    Proxy,
    Direct
}
