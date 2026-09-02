using System.ComponentModel;
using System.Diagnostics;

namespace AzureSandboxDemo;

internal sealed class CommandRunner
{
    public async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool includeFailureOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["NO_COLOR"] = "1";
        startInfo.Environment["AZURE_CORE_NO_COLOR"] = "1";

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        cancellationToken.ThrowIfCancellationRequested();

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new AzureSandboxException($"Could not start '{fileName}'.");
        }
        catch (Win32Exception exception)
        {
            throw new AzureSandboxException(
                $"Could not start '{fileName}'. Ensure it is installed and available on PATH.",
                exception);
        }

        using (process)
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited between the state check and the kill request.
                    }
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                throw;
            }

            var output = await standardOutput;
            var error = await standardError;
            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                var detailMessage = includeFailureOutput && !string.IsNullOrWhiteSpace(detail)
                    ? $": {Truncate(detail.Trim())}"
                    : "";
                throw new AzureSandboxException(
                    $"'{fileName}' exited with code {process.ExitCode}{detailMessage}");
            }

            return new CommandResult(output.Trim(), error.Trim());
        }
    }

    private static string Truncate(string value)
        => value.Length <= 2000 ? value : $"{value[..2000]}...";
}
