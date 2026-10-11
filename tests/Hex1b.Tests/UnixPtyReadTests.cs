using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Hex1b.Tests;

[TestClass]
[TestCategory("Unix")]
[DoNotParallelize]
public class UnixPtyReadTests
{
    private const int DummyDescriptorCount = 1100;
    private readonly List<SafeFileHandle> _dummyDescriptors = [];

    [TestCleanup]
    public void ReleaseDummyDescriptors()
    {
        foreach (var descriptor in _dummyDescriptors)
            descriptor.Dispose();
        _dummyDescriptors.Clear();
    }

    [TestMethod]
    public async Task ReadOutputAsync_MasterDescriptorAboveSelectLimit_ReturnsChildOutput()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");

        // Occupy the low descriptor numbers so the PTY master is numbered 1024 or above.
        for (var i = 0; i < DummyDescriptorCount; i++)
            _dummyDescriptors.Add(File.OpenHandle("/dev/null"));

        await using var process = new Hex1bTerminalChildProcess(
            "/bin/sh", ["-c", "printf ready; read line"]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.StartAsync(cts.Token);
            var bytes = await process.ReadOutputAsync(cts.Token);

            Assert.IsFalse(process.HasExited, "The child blocks on input and must still be running.");
            Assert.IsGreaterThan(0, bytes.Length, "The first read must not report end of output while the child is alive.");
            Assert.Contains("ready", Encoding.UTF8.GetString(bytes.Span));
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(9);
        }
    }
}
