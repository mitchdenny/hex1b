using Hex1b;

namespace MuxerDemo;

/// <summary>
/// Runs a headless PTY process and serves it over HMP v1 on a Unix domain socket.
/// </summary>
internal static class MuxerServer
{
    public static async Task RunAsync(string socketPath)
    {
        await using var terminal = Hex1b.Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = GetShell();
                if (OperatingSystem.IsWindows())
                    options.WindowsPtyMode = Hex1b.WindowsPtyMode.RequireProxy;
            })
            .WithHmp1UdsServer(socketPath)
            .Build();

        await terminal.RunAsync();

        // Clean up socket file on exit so clients don't see stale sessions
        try { File.Delete(socketPath); } catch { }
    }

    private static string GetShell()
    {
        if (OperatingSystem.IsWindows())
            return "pwsh.exe";

        var shell = Environment.GetEnvironmentVariable("SHELL");
        return !string.IsNullOrEmpty(shell) ? shell : "bash";
    }
}
