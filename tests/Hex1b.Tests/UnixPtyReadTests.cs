using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Hex1b.Tests;

[TestClass]
[TestCategory("Unix")]
[DoNotParallelize]
public class UnixPtyReadTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IdlePeriod = TimeSpan.FromMilliseconds(250);
    private const string InteractiveScript =
        "stty -echo; printf ready; while IFS= read -r line; do " +
        "[ \"$line\" = quit ] && exit 0; printf '<%s>' \"$line\"; done";

    [TestInitialize]
    public void RequireUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Assert.Inconclusive("Requires a Unix PTY.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadOutputAsync_LiveChildAtOrdinaryOrHighDescriptor_ReturnsExactMarker(bool highDescriptor)
    {
        var descriptors = new List<SafeFileHandle>();
        var handle = new UnixPtyHandle();
        var process = new Hex1bTerminalChildProcess("/bin/sh", ["-c", InteractiveScript],
            workingDirectory: null, environment: null, inheritEnvironment: true,
            initialWidth: 80, initialHeight: 24, ptyHandleFactory: _ => handle);
        using var cancellation = new CancellationTokenSource(Deadline);
        try
        {
            if (highDescriptor)
            {
                // Stop at a measured descriptor, not an assumed number of open handles.
                do
                {
                    try
                    {
                        descriptors.Add(File.OpenHandle("/dev/null"));
                    }
                    catch (IOException error) when ((error.HResult & 0xffff) == 24)
                    {
                        Assert.Inconclusive("The process descriptor limit prevents exercising a PTY above 1023.");
                    }
                } while (descriptors[^1].DangerousGetHandle().ToInt64() < 1100);
            }

            await process.StartAsync(cancellation.Token).WaitAsync(Deadline);
            Assert.IsGreaterThanOrEqualTo(0, handle.MasterFileDescriptor);
            if (highDescriptor)
                Assert.IsGreaterThanOrEqualTo(1024, handle.MasterFileDescriptor,
                    "The actual PTY master must exercise poll beyond select's descriptor limit.");
            await ReadExactAsync(process, "ready", cancellation.Token);
            var pending = process.ReadOutputAsync(cancellation.Token).AsTask();
            await AssertIdleAsync(pending);
            Assert.IsFalse(process.HasExited);
            await WriteAsync(process, "probe\n", cancellation.Token);
            await ReadExactAsync(process, "<probe>", cancellation.Token, pending);
            await WriteAsync(process, "quit\n", cancellation.Token);
            Assert.IsTrue((await process.ReadOutputAsync(cancellation.Token).AsTask().WaitAsync(Deadline)).IsEmpty);
            Assert.AreEqual(0, await Task.Run(() => process.WaitForExitAsync(cancellation.Token)).WaitAsync(Deadline));
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await DisposeAsync(process);
            }
            finally
            {
                foreach (var descriptor in descriptors)
                    descriptor.Dispose();
            }
        }
    }

    [TestMethod]
    public async Task ReadOutputAsync_RepeatedIdleInputHandshakes_PreservesExactOutput()
    {
        var process = new Hex1bTerminalChildProcess("/bin/sh", ["-c", InteractiveScript]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.StartAsync(cancellation.Token).WaitAsync(Deadline);
            var actual = new StringBuilder(await ReadExactAsync(process, "ready", cancellation.Token));
            var expected = new StringBuilder("ready");
            for (var i = 0; i < 12; i++)
            {
                var pending = process.ReadOutputAsync(cancellation.Token).AsTask();
                await AssertIdleAsync(pending);
                var marker = $"marker-{i:D2}";
                await WriteAsync(process, marker + "\n", cancellation.Token);
                actual.Append(await ReadExactAsync(process, $"<{marker}>", cancellation.Token, pending));
                expected.Append($"<{marker}>");
            }
            await WriteAsync(process, "quit\n", cancellation.Token);
            Assert.IsTrue((await process.ReadOutputAsync(cancellation.Token).AsTask().WaitAsync(Deadline)).IsEmpty,
                "There must be no trailing echo or duplicated marker.");
            Assert.AreEqual(expected.ToString(), actual.ToString());
            Assert.AreEqual(0, await Task.Run(() => process.WaitForExitAsync(cancellation.Token)).WaitAsync(Deadline));
        }
        finally
        {
            cancellation.Cancel();
            await DisposeAsync(process);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadOutputAsync_LargePayloadAcrossChildExit_DrainsEveryByteAndReturnsEof(bool reapConcurrently)
    {
        var block = new string(Enumerable.Range(0, 256).Select(i => (char)('A' + i % 26)).ToArray());
        var expected = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(block, 256)));
        Assert.AreEqual(65536, expected.Length);
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var process = new Hex1bTerminalChildProcess("/bin/sh",
                ["-c", "stty -echo; printf ready; read line; " +
                    "i=0; while [ \"$i\" -lt 256 ]; do printf '%s' \"$1\"; i=$((i+1)); done",
                    "fixture", block]);
            using var cancellation = new CancellationTokenSource(Deadline);
            try
            {
                await process.StartAsync(cancellation.Token).WaitAsync(Deadline);
                await ReadExactAsync(process, "ready", cancellation.Token);
                var drain = DrainAsync(process, cancellation.Token);
                var exit = reapConcurrently
                    ? Task.Run(() => process.WaitForExitAsync(cancellation.Token))
                    : null;
                await WriteAsync(process, "go\n", cancellation.Token);
                var output = await drain.WaitAsync(Deadline);
                TestSeq.AreEqual(expected, output);
                Assert.AreEqual(0, await (exit ?? Task.Run(() => process.WaitForExitAsync(cancellation.Token))).WaitAsync(Deadline));
                for (var read = 0; read < 3; read++)
                    Assert.IsTrue((await process.ReadOutputAsync(cancellation.Token).AsTask().WaitAsync(Deadline)).IsEmpty);
            }
            finally
            {
                cancellation.Cancel();
                await DisposeAsync(process);
            }
        }
    }

    [TestMethod]
    public async Task ReadOutputAsync_EmptyChildExit_RepeatedReadsReturnPromptEof()
    {
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var process = new Hex1bTerminalChildProcess("/bin/sh", ["-c", "exit 0"]);
            using var cancellation = new CancellationTokenSource(Deadline);
            try
            {
                await process.StartAsync(cancellation.Token).WaitAsync(Deadline);
                for (var read = 0; read < 4; read++)
                    Assert.IsTrue((await process.ReadOutputAsync(cancellation.Token).AsTask().WaitAsync(Deadline)).IsEmpty);
                Assert.AreEqual(0, await Task.Run(() => process.WaitForExitAsync(cancellation.Token)).WaitAsync(Deadline));
            }
            finally
            {
                cancellation.Cancel();
                await DisposeAsync(process);
            }
        }
    }

    [TestMethod]
    public async Task ReadOutputAsync_CanceledIdleRead_SubsequentReadStillReceivesOutput()
    {
        var process = new Hex1bTerminalChildProcess("/bin/sh", ["-c", InteractiveScript]);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.StartAsync(lifetime.Token).WaitAsync(Deadline);
            await ReadExactAsync(process, "ready", lifetime.Token);
            for (var iteration = 0; iteration < 10; iteration++)
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                var pending = process.ReadOutputAsync(cancellation.Token).AsTask();
                await AssertIdleAsync(pending);
                cancellation.Cancel();
                Assert.IsTrue((await pending.WaitAsync(Deadline)).IsEmpty,
                    "The existing workload cancellation contract returns empty output.");
                var next = process.ReadOutputAsync(lifetime.Token).AsTask();
                await WriteAsync(process, $"after-{iteration}\n", lifetime.Token);
                await ReadExactAsync(process, $"<after-{iteration}>", lifetime.Token, next);
            }
            await WriteAsync(process, "quit\n", lifetime.Token);
            Assert.IsTrue((await process.ReadOutputAsync(lifetime.Token).AsTask().WaitAsync(Deadline)).IsEmpty);
            Assert.AreEqual(0, await Task.Run(() => process.WaitForExitAsync(lifetime.Token)).WaitAsync(Deadline));
        }
        finally
        {
            lifetime.Cancel();
            await DisposeAsync(process);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisposeAsync_PendingIdleRead_CompletesReadAndDisposal(bool waitThroughPollTimeouts)
    {
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var process = new Hex1bTerminalChildProcess("/bin/sh", ["-c", InteractiveScript]);
            using var startup = new CancellationTokenSource(Deadline);
            try
            {
                await process.StartAsync(startup.Token).WaitAsync(Deadline);
                await ReadExactAsync(process, "ready", startup.Token);
                // No read cancellation: disposal itself must unblock the native wait.
                var pending = process.ReadOutputAsync().AsTask();
                if (waitThroughPollTimeouts)
                    await AssertIdleAsync(pending);
                await DisposeAsync(process);
                Assert.IsTrue((await pending.WaitAsync(Deadline)).IsEmpty);
                Assert.IsTrue((await process.ReadOutputAsync().AsTask().WaitAsync(Deadline)).IsEmpty);
            }
            finally
            {
                await DisposeAsync(process);
            }
        }
    }

    private static async Task AssertIdleAsync(Task<ReadOnlyMemory<byte>> read)
    {
        // This is an intentional idle interval across multiple 100 ms poll timeouts,
        // not a sleep used to guess whether the child's marker has been written.
        await Assert.ThrowsExactlyAsync<TimeoutException>(() => read.WaitAsync(IdlePeriod));
        Assert.IsFalse(read.IsCompleted, "An idle live PTY must not report EOF.");
    }

    private static async Task<string> ReadExactAsync(Hex1bTerminalChildProcess process, string expected,
        CancellationToken ct, Task<ReadOnlyMemory<byte>>? firstRead = null)
    {
        var output = new StringBuilder();
        while (output.Length < expected.Length)
        {
            var bytes = await (firstRead ?? process.ReadOutputAsync(ct).AsTask()).WaitAsync(Deadline);
            firstRead = null;
            Assert.IsFalse(bytes.IsEmpty, $"Unexpected EOF after '{output}', expecting '{expected}'.");
            output.Append(Encoding.ASCII.GetString(bytes.Span));
        }
        Assert.AreEqual(expected, output.ToString());
        return output.ToString();
    }

    private static async Task<byte[]> DrainAsync(Hex1bTerminalChildProcess process, CancellationToken ct)
    {
        using var output = new MemoryStream();
        while (true)
        {
            var bytes = await process.ReadOutputAsync(ct).AsTask().WaitAsync(Deadline);
            if (bytes.IsEmpty)
                return output.ToArray();
            Assert.IsLessThanOrEqualTo(65536L, output.Length + bytes.Length, "Unexpected surplus output.");
            output.Write(bytes.Span);
        }
    }

    // Put potentially synchronous native operations on a worker so the watchdog
    // can fail independently even if cancellation cannot interrupt the syscall.
    private static Task WriteAsync(Hex1bTerminalChildProcess process, string text, CancellationToken ct)
        => Task.Run(async () => await process.WriteInputAsync(Encoding.ASCII.GetBytes(text), ct)).WaitAsync(Deadline);

    private static Task DisposeAsync(Hex1bTerminalChildProcess process)
        => Task.Run(async () => await process.DisposeAsync()).WaitAsync(Deadline);
}
