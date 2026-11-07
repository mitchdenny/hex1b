using System.Diagnostics;
using System.Text;

namespace Hex1b.Aot.Tests;

internal sealed record ProcessOutcome(string Command, int? ExitCode, string StandardOutput,
    string StandardError, string Status, string LogPath);

internal sealed class TestProcessException(ProcessOutcome outcome, Exception? innerException)
    : Exception($"Native AOT command failed. {outcome.Command}\nStatus: {outcome.Status}; " +
        $"exit code: {outcome.ExitCode?.ToString() ?? "unavailable"}.\nEvidence: {outcome.LogPath}\n" +
        $"{outcome.StandardOutput}\n{outcome.StandardError}", innerException)
{
    public ProcessOutcome Outcome { get; } = outcome;
}

internal static class TestProcess
{
    public static async Task<ProcessOutcome> RunAsync(string executable, IReadOnlyList<string> arguments,
        string workingDirectory, string logPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        var command = string.Join(" ", new[] { executable }.Concat(arguments).Select(argument => $"\"{argument}\""));
        using var process = new Process { StartInfo = start };
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(timeout);
        Task? stdout = null;
        Task? stderr = null;
        var stdoutText = new CapturedOutput();
        var stderrText = new CapturedOutput();
        var status = "not started";
        Exception? failure = null;
        int? exitCode = null;
        try
        {
            lifetime.Token.ThrowIfCancellationRequested();
            if (!process.Start())
                throw new InvalidOperationException("Process.Start returned false.");
            status = "running";
            stdout = stdoutText.ReadAsync(process.StandardOutput);
            stderr = stderrText.ReadAsync(process.StandardError);
            await process.WaitForExitAsync(lifetime.Token);
            exitCode = process.ExitCode;
            status = "exited";
        }
        catch (OperationCanceledException ex)
        {
            status = cancellationToken.IsCancellationRequested ? "cancelled" : $"timed out after {timeout}";
            failure = ex;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            status = "launch/wait failed";
            failure = ex;
        }
        finally
        {
            if (stdout is not null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    exitCode = process.ExitCode;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException)
                {
                    failure = failure is null ? ex : new AggregateException(failure, ex);
                    status += "; process cleanup failed";
                }
            }
        }
        try
        {
            if (stdout is not null && stderr is not null)
            {
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            failure = failure is null ? ex : new AggregateException(failure, ex);
            status += "; output drain failed/incomplete";
        }
        var output = stdoutText.Text;
        var error = stderrText.Text;
        var outcome = new ProcessOutcome(command, exitCode, output, error, status, logPath);
        await File.WriteAllTextAsync(logPath,
            $"Command: {command}\nWorking directory: {workingDirectory}\nStatus: {status}\nExit code: {exitCode?.ToString() ?? "unavailable"}\n" +
            $"Failure: {failure}\n--- stdout ---\n{output}\n--- stderr ---\n{error}\n", CancellationToken.None);
        if (failure is not null || exitCode != 0)
            throw new TestProcessException(outcome, failure);
        return outcome;
    }

    private sealed class CapturedOutput
    {
        private readonly StringBuilder _text = new();

        public string Text
        {
            get
            {
                lock (_text)
                    return _text.ToString();
            }
        }

        public async Task ReadAsync(StreamReader reader)
        {
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer)) != 0)
            {
                lock (_text)
                    _text.Append(buffer, 0, count);
            }
        }
    }
}
