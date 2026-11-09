using System.Diagnostics;
using System.Text;

namespace Hex1b.Tests.NativeAot;

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
    public static async Task<ProcessOutcome> RunPtyAsync(string executable, string workingDirectory,
        string logPath, IReadOnlyList<string> stopPrompts, CancellationToken cancellationToken)
    {
        IPtyHandle pty = OperatingSystem.IsWindows() ? new WindowsProxyPtyHandle() : new UnixPtyHandle();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(30));
        var output = new StringBuilder();
        int? exitCode = null;
        var status = "running in PTY";
        var prompt = 0;
        Exception? failure = null;
        try
        {
            await pty.StartAsync(executable, [], workingDirectory, [], 100, 24, lifetime.Token);
            var decoder = Encoding.UTF8.GetDecoder();
            var chars = new char[4096];
            while (true)
            {
                var bytes = await pty.ReadAsync(lifetime.Token);
                if (bytes.IsEmpty)
                    break;
                var remaining = bytes;
                while (!remaining.IsEmpty)
                {
                    decoder.Convert(remaining.Span, chars, false, out var used, out var count, out _);
                    output.Append(chars, 0, count);
                    remaining = remaining[used..];
                }
                if (prompt < stopPrompts.Count && output.ToString().Contains(stopPrompts[prompt], StringComparison.Ordinal))
                {
                    await pty.WriteAsync(new byte[] { 3 }, lifetime.Token);
                    prompt++;
                }
            }
            exitCode = await pty.WaitForExitAsync(lifetime.Token);
            status = "exited";
            if (exitCode != 0 || prompt != stopPrompts.Count)
                throw new InvalidOperationException($"Native console control exited with code {exitCode}, rendered {prompt}/{stopPrompts.Count} prompts.");
        }
        catch (Exception ex)
        {
            failure = ex;
            status = "PTY execution failed";
        }
        finally
        {
            try
            {
                await Task.Run(async () => await pty.DisposeAsync()).WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                failure = failure is null ? ex : new AggregateException(failure, ex);
                status += "; PTY cleanup failed";
            }
            await File.WriteAllTextAsync(logPath,
                $"Command: {executable}\nWorking directory: {workingDirectory}\nStatus: {status}\nExit code: {exitCode}\n" +
                $"Rendered prompts: {prompt}/{stopPrompts.Count}\nFailure: {failure}\n--- PTY output ---\n{output}",
                CancellationToken.None);
        }
        var outcome = new ProcessOutcome(executable, exitCode, output.ToString(), "", status, logPath);
        if (failure is not null)
            throw new TestProcessException(outcome, failure);
        return outcome;
    }

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
