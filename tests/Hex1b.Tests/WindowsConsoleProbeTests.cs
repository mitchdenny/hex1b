using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class WindowsConsoleProbeTests
{
    private const string ChildEnvironmentVariable = "HEX1B_WINDOWS_CONSOLE_PROBE_TEST";
    private const string Query = "\x1b_Gi=2147483647,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\";

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
            if (!replied && output.ToString().Contains(Query, StringComparison.Ordinal))
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

        Assert.IsTrue(replied, $"KGP query did not reach the outer terminal: {output}");
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
        await adapter.EnterRawModeAsync(cancellation.Token);

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
