using Hex1b;

namespace EncryptedMuxerDemo;

/// <summary>
/// Runs a headless PTY process and serves it over TLS-encrypted HMP v1 on a Unix domain socket.
/// </summary>
internal static class EncryptedMuxerServer
{
    public static async Task RunAsync(string socketPath)
    {
        // Pre-generate the certificate so it's ready before any client connects
        _ = DemoTls.ServerCertificate;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess(options =>
            {
                options.FileName = GetShell();
                if (OperatingSystem.IsWindows())
                    options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
            })
            .WithHmp1UdsServer(socketPath, DemoTls.AuthenticateAsServerAsync)
            .Build();

        await terminal.RunAsync();

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
