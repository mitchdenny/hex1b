using System.Runtime.InteropServices;
using Hex1b;

if (!OperatingSystem.IsWindows())
{
    await Console.Error.WriteLineAsync("WindowsPtyDemo requires Windows.");
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;
var shimPath = TryFindShimPath();

if (shimPath is not null)
{
    await Console.Out.WriteLineAsync(
        $"Launching Windows PowerShell through Hex1b PTY using {Path.GetFileName(shimPath)}. Type 'exit' to quit.");
}
else
{
    await Console.Out.WriteLineAsync(
        "Launching Windows PowerShell through Hex1b PTY. hex1bpty.exe was not found in the sample output, so Hex1b will fall back to its in-process Windows PTY host.");
}

try
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPtyProcess("powershell.exe", "-NoLogo", "-NoProfile")
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
