using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal static class PagesProcess
{
    internal static async Task RunAsync(
        string executable, string[] arguments, string workingDirectory,
        ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        logger.LogInformation("Running {Executable} in {Directory}.", executable, workingDirectory);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {executable}.");
        var output = PumpAsync(process.StandardOutput, logger);
        var error = PumpAsync(process.StandardError, logger);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(output, error);
            throw;
        }
        await Task.WhenAll(output, error);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} failed with exit code {process.ExitCode}.");
        }
    }

    private static async Task PumpAsync(StreamReader reader, ILogger logger)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            logger.LogInformation("{Output}", line);
        }
    }
}
