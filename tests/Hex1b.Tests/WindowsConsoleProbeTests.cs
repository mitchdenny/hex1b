using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class WindowsConsoleProbeTests
{
    private const string ChildEnvironmentVariable = "HEX1B_WINDOWS_CONSOLE_PROBE_TEST";
    private const string ProbeStarted = "PROBE_STARTED";
    internal const string PtyIoChildEnvironmentVariable = "HEX1B_WINDOWS_PTY_IO_TEST";

    [TestMethod]
    public async Task PtyIoChild()
    {
        if (Environment.GetEnvironmentVariable(PtyIoChildEnvironmentVariable) != "1")
            return;

        using var driver = new WindowsConsoleDriver();
        driver.EnterRawMode();
        driver.Resized += (width, height) =>
            driver.Write(Encoding.UTF8.GetBytes($"PTY_RESIZED:{width}x{height};\r\n"));
        driver.Write(Encoding.UTF8.GetBytes($"PTY_READY:{driver.Width}x{driver.Height};\r\n"));

        var buffer = new byte[32];
        while (true)
        {
            var count = await driver.ReadAsync(buffer, TestContext.Current.CancellationToken);
            Assert.IsTrue(count > 0, "PTY probe input ended before the quit command.");
            for (var i = 0; i < count; i++)
            {
                var input = (char)buffer[i];
                if (input == 'q')
                    return;

                Assert.IsTrue(input is 'a' or 'b' or 'c', $"Unexpected PTY probe input: {(int)input}");
                driver.Write(Encoding.UTF8.GetBytes($"PTY_INPUT:{input}:{driver.Width}x{driver.Height};\r\n"));
            }
        }
    }

    [TestMethod]
    [TestCategory("Windows")]
    [DataRow("OK", true)]
    [DataRow("ENOTSUP", false)]
    [DataRow(null, false)]
    public async Task EnterRawModeAsync_ThroughConPty_DiscoversKgpAndPreservesInput(string? reply, bool supported)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        await using var pty = new WindowsProxyPtyHandle();
        await pty.StartAsync(
            Path.Combine(AppContext.BaseDirectory, "Hex1b.Tests.exe"),
            ["--filter", "FullyQualifiedName=Hex1b.Tests.WindowsConsoleProbeTests.ProbeChild", "--no-progress", "--no-ansi"],
            AppContext.BaseDirectory,
            new Dictionary<string, string> { [ChildEnvironmentVariable] = "1" },
            80, 24, ct);

        var output = new StringBuilder();
        var replied = false;
        var expected = $"PROBE_RESULT:{supported}:abc";
        while (!output.ToString().Contains(expected, StringComparison.Ordinal))
        {
            var bytes = await pty.ReadAsync(ct);
            Assert.IsFalse(bytes.IsEmpty, $"Child exited before reporting capabilities: {output}");
            output.Append(Encoding.UTF8.GetString(bytes.Span));
            if (!replied && output.ToString().Contains(ProbeStarted, StringComparison.Ordinal))
            {
                replied = true;
                // Exercise replies larger than a ReadConsoleInput batch, mixed with keyboard input.
                if (reply is not null)
                {
                    foreach (var value in Encoding.UTF8.GetBytes($"\x1b_Gi=2147483647;{reply}\x1b\\"))
                        await pty.WriteAsync(new byte[] { value }, ct);
                }
                await pty.WriteAsync("abc"u8.ToArray(), ct);
            }
        }

        Assert.IsTrue(replied, $"Child did not start probing: {output}");
        await pty.WriteAsync("z\r"u8.ToArray(), ct);
        Assert.AreEqual(0, await pty.WaitForExitAsync(ct), output.ToString());
    }

    [TestMethod]
    public async Task ProbeChild()
    {
        if (Environment.GetEnvironmentVariable(ChildEnvironmentVariable) != "1")
            return;

        using var driver = new WindowsConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(
            driver, kgpProbeTimeout: TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var probe = adapter.EnterRawModeAsync(cancellation.Token);
        // Some ConPTY hosts consume APC queries instead of forwarding them.
        // Signal after the probe starts so input coverage does not require graphics passthrough.
        driver.Write(Encoding.UTF8.GetBytes(ProbeStarted + "\r\n"));
        await probe;

        var input = new StringBuilder();
        while (input.Length < 3)
        {
            var bytes = await adapter.ReadInputAsync(cancellation.Token);
            Assert.IsFalse(bytes.IsEmpty);
            input.Append(Encoding.UTF8.GetString(bytes.Span));
        }

        driver.Write(Encoding.UTF8.GetBytes($"PROBE_RESULT:{adapter.Capabilities.SupportsKgp}:{input}\r\n"));

        input.Clear();
        while (input.Length < 2)
        {
            var bytes = await adapter.ReadInputAsync(cancellation.Token);
            Assert.IsFalse(bytes.IsEmpty);
            input.Append(Encoding.UTF8.GetString(bytes.Span));
        }
        Assert.AreEqual("z\r", input.ToString());
    }
}
