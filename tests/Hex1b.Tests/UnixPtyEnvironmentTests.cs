using System.Text;

namespace Hex1b.Tests;

[TestClass]
[TestCategory("Unix")]
public class UnixPtyEnvironmentTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StartAsync_Exec_ReceivesOnlyConfiguredEnvironment(bool overrideTerm)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");

        var environment = new Dictionary<string, string> { ["HEX1B_MARKER"] = "space = \u03bb" };
        if (overrideTerm)
            environment["TERM"] = "vt100";
        await using var process = new Hex1bTerminalChildProcess(
            "/usr/bin/env", ["HEX1B_ARG=argument"],
            environment: environment, inheritEnvironment: false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.StartAsync(cts.Token);
        var output = await ReadOutputAsync(process, cts.Token);
        Assert.AreEqual(0, await process.WaitForExitAsync(cts.Token));

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(4, lines.Length, "The child must not inherit the host's environment.");
        var variables = lines.Select(line => line.Split('=', 2)).ToDictionary(parts => parts[0], parts => parts[1]);
        Assert.AreEqual(overrideTerm ? "vt100" : "xterm-256color", variables["TERM"]);
        Assert.AreEqual("space = \u03bb", variables["HEX1B_MARKER"]);
        Assert.AreEqual("argument", variables["HEX1B_ARG"]);
        Assert.AreEqual("1", variables["HEX1B_NESTING_LEVEL"]);
    }

    [TestMethod]
    public async Task StartAsync_LoginShell_PreservesEnvironmentAndLoginArgv()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");

        await using var process = new Hex1bTerminalChildProcess(
            "/bin/sh", [], environment: new()
            {
                ["TERM"] = "xterm-256color",
                ["HEX1B_MARKER"] = "login-value",
                ["HEX1B_NESTING_LEVEL"] = "7"
            }, inheritEnvironment: false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.StartAsync(cts.Token);
        await process.WriteInputAsync(Encoding.UTF8.GetBytes(
            "printf '\\nRESULT:%s|%s|%s|%s\\n' \"$HEX1B_MARKER\" \"$TERM\" \"$HEX1B_NESTING_LEVEL\" \"$0\"; exit\n"),
            cts.Token);
        var output = await ReadOutputAsync(process, cts.Token);
        Assert.AreEqual(0, await process.WaitForExitAsync(cts.Token));
        Assert.Contains("RESULT:login-value|xterm-256color|7|-sh", output);
    }

    [TestMethod]
    public async Task StartAsync_ConcurrentChildren_KeepEnvironmentsIsolated()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var originalNesting = Environment.GetEnvironmentVariable("HEX1B_NESTING_LEVEL");
        var results = await Task.WhenAll(Enumerable.Range(1, 4).Select(async index =>
        {
            await using var process = new Hex1bTerminalChildProcess(
                "/bin/sh", ["-c", "printf '%s' \"$HEX1B_MARKER:$HEX1B_NESTING_LEVEL\""],
                environment: new()
                {
                    ["HEX1B_MARKER"] = $"child-{index}",
                    ["HEX1B_NESTING_LEVEL"] = index.ToString()
                }, inheritEnvironment: false);
            await process.StartAsync(cts.Token);
            var output = await ReadOutputAsync(process, cts.Token);
            Assert.AreEqual(0, await process.WaitForExitAsync(cts.Token));
            return output;
        }));

        TestSeq.AreEqual(new[] { "child-1:1", "child-2:2", "child-3:3", "child-4:4" }, results);
        Assert.AreEqual(originalNesting, Environment.GetEnvironmentVariable("HEX1B_NESTING_LEVEL"));
    }

    private static async Task<string> ReadOutputAsync(Hex1bTerminalChildProcess process, CancellationToken ct)
    {
        using var output = new MemoryStream();
        while (true)
        {
            var bytes = await process.ReadOutputAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (bytes.IsEmpty)
                return Encoding.UTF8.GetString(output.ToArray());
            output.Write(bytes.Span);
        }
    }
}
