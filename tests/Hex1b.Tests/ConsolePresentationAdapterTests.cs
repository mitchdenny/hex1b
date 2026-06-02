using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class ConsolePresentationAdapterTests
{
    [TestMethod]
    [DataRow(0x45, '\0', false, false, false, "e")]
    [DataRow(0x45, '\0', false, false, true, "E")]
    [DataRow(0x31, '\0', false, false, false, "1")]
    [DataRow(0x31, '\0', false, false, true, "!")]
    [DataRow(0xBF, '\0', false, false, false, "/")]
    [DataRow(0xBF, '\0', false, false, true, "?")]
    [DataRow(0x20, '\0', false, false, false, " ")]
    public void WindowsConsoleDriver_GetPrintableText_FallsBackToVirtualKeyMapping(
        int virtualKey,
        char unicodeChar,
        bool hasCtrl,
        bool hasAlt,
        bool hasShift,
        string expected)
    {
        var text = WindowsConsoleDriver.GetPrintableText((ushort)virtualKey, unicodeChar, hasCtrl, hasAlt, hasShift);

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    [DataRow(0x45, true, false)]
    [DataRow(0x45, false, true)]
    public void WindowsConsoleDriver_GetPrintableText_DoesNotInventModifiedCharacters(
        int virtualKey,
        bool hasCtrl,
        bool hasAlt)
    {
        var text = WindowsConsoleDriver.GetPrintableText((ushort)virtualKey, '\0', hasCtrl, hasAlt, hasShift: false);

        Assert.IsNull(text);
    }

    [TestMethod]
    [DataRow("\x1b[65;30;97;1;0;1_", "a")]
    [DataRow("\x1b[13;28;13;1;0;1_", "\r")]
    [DataRow("\x1b[65;30;97;1;0;3_", "aaa")]
    public void WindowsConsoleDriver_TryTranslateWin32InputSequence_DecodesForwardedKeyboardInput(
        string sequence,
        string expected)
    {
        var handled = WindowsConsoleDriver.TryTranslateWin32InputSequence(sequence, out var bytes);

        Assert.IsTrue(handled);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public void WindowsConsoleDriver_TryTranslateWin32InputSequence_IgnoresKeyUpFrames()
    {
        var handled = WindowsConsoleDriver.TryTranslateWin32InputSequence("\x1b[65;30;97;0;0;1_", out var bytes);

        Assert.IsTrue(handled);
        Assert.IsEmpty(bytes);
    }

    [TestMethod]
    public async Task Constructor_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        // This test verifies the factory pattern works correctly
        // On unsupported platforms (Windows for now), should throw
        // On Linux/macOS, should succeed
        
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.ThrowsExactly<PlatformNotSupportedException>(() => 
                new ConsolePresentationAdapter(enableMouse: false));
        }
        else
        {
            // On Linux/macOS, constructor should work (but we can't fully test without a real terminal)
            // Just verify it doesn't throw during construction
            // Note: This may still fail in CI without a TTY, which is expected
            try
            {
                var adapter = new ConsolePresentationAdapter(enableMouse: false);
                Assert.IsNotNull(adapter);
                // Clean up - must use async dispose
                await adapter.DisposeAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("TTY") || ex.Message.Contains("terminal") || ex.Message.Contains("tcgetattr"))
            {
                // Expected in CI environments without a real TTY
                // Skip the test gracefully
            }
        }
    }
    
    [TestMethod]
    public void UnixConsoleDriver_OnlyAvailableOnUnixPlatforms()
    {
        if (OperatingSystem.IsLinux())
        {
            // Should be able to create the driver (but may fail without TTY)
            try
            {
                using var driver = new UnixConsoleDriver();
                Assert.IsNotNull(driver);
            }
            catch (Exception ex) when (ex.Message.Contains("TTY") || ex.Message.Contains("tcgetattr"))
            {
                // Expected in CI environments without a real TTY
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Should be able to create the driver (but may fail without TTY)
            try
            {
                using var driver = new UnixConsoleDriver();
                Assert.IsNotNull(driver);
            }
            catch (Exception ex) when (ex.Message.Contains("TTY") || ex.Message.Contains("tcgetattr"))
            {
                // Expected in CI environments without a real TTY
            }
        }
        else
        {
            // On Windows, the driver should throw or not be available
            // Suppress CA1416 - we're intentionally testing that this throws on unsupported platforms
#pragma warning disable CA1416
            Assert.ThrowsExactly<PlatformNotSupportedException>(() => new UnixConsoleDriver());
#pragma warning restore CA1416
        }
    }

    [TestMethod]
    public async Task EnterRawModeAsync_WhenKgpQueryResponds_EnablesKgpSupport()
    {
        using var driver = new FakeConsoleDriver($"\x1b_Gi=2147483647;OK\x1b\\");
        await using var adapter = new ConsolePresentationAdapter(
            driver,
            kgpProbeTimeout: TimeSpan.FromMilliseconds(25));

        Assert.IsFalse(adapter.Capabilities.SupportsKgp);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsTrue(adapter.Capabilities.SupportsKgp);
        Assert.Contains("\x1b_Gi=2147483647,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\", driver.WrittenText);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_WhenProbeTimesOut_LeavesKgpDisabled()
    {
        using var driver = new FakeConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(
            driver,
            kgpProbeTimeout: TimeSpan.FromMilliseconds(25));

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsFalse(adapter.Capabilities.SupportsKgp);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_WhenProbeReadsMixedInput_PreservesNonProbeBytes()
    {
        using var driver = new FakeConsoleDriver($"\x1b_Gi=2147483647;OK\x1b\\abc");
        await using var adapter = new ConsolePresentationAdapter(
            driver,
            kgpProbeTimeout: TimeSpan.FromMilliseconds(25));

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        var input = await adapter.ReadInputAsync(TestContext.Current.CancellationToken);

        Assert.AreEqual("abc", Encoding.ASCII.GetString(input.Span));
    }

    [TestMethod]
    public async Task EnterRawModeAsync_WhenProbeResponseIsSplitAcrossReads_EnablesKgpAndPreservesTrailingBytes()
    {
        using var driver = new FakeConsoleDriver("\x1b_Gi=2147483647", ";OK\x1b\\abc");
        await using var adapter = new ConsolePresentationAdapter(
            driver,
            kgpProbeTimeout: TimeSpan.FromMilliseconds(25));

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        var input = await adapter.ReadInputAsync(TestContext.Current.CancellationToken);

        Assert.IsTrue(adapter.Capabilities.SupportsKgp);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(input.Span));
    }
}

[TestClass]
public class Hex1bTerminalTests_Workload
{
    [TestMethod]
    public void Constructor_HeadlessMode_CreatesWorkloadAdapter()
    {
        // Headless terminal (no presentation adapter)
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // WorkloadAdapter should implement the interface
        TestSeq.IsType<IHex1bAppTerminalWorkloadAdapter>(workload);
    }
    
    [TestMethod]
    public void GetSize_ReturnsConfiguredSize()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(100, 50).Build();
        
        Assert.AreEqual(100, terminal.Width);
        Assert.AreEqual(50, terminal.Height);
    }
    
    [TestMethod]
    public async Task Write_CapturesOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        workload.Write("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.IsTrue(terminal.CreateSnapshot().ContainsText("Hello"));
    }
    
    [TestMethod]
    public void InputEvents_ReturnsChannelReader()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        var reader = workload.InputEvents;
        Assert.IsNotNull(reader);
    }
    
    [TestMethod]
    public void Capabilities_ReturnsDefaults()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        var caps = workload.Capabilities;
        Assert.IsNotNull(caps);
        Assert.IsTrue(caps.SupportsMouse);
        Assert.IsTrue(caps.SupportsTrueColor);
    }
}

internal sealed class FakeConsoleDriver : IConsoleDriver
{
    private readonly Queue<byte[]> _readChunks = new();
    private readonly List<byte> _written = new();

    public FakeConsoleDriver(params string[] readChunks)
    {
        foreach (var chunk in readChunks)
        {
            _readChunks.Enqueue(Encoding.ASCII.GetBytes(chunk));
        }
    }

    public bool DataAvailable => _readChunks.Count > 0;

    public int Width => 80;

    public int Height => 24;

    public string WrittenText => Encoding.ASCII.GetString(_written.ToArray());

    public event Action<int, int>? Resized
    {
        add { }
        remove { }
    }

    public void EnterRawMode(bool preserveOPost = false)
    {
    }

    public void ExitRawMode()
    {
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_readChunks.Count > 0)
        {
            var chunk = _readChunks.Dequeue();
            chunk.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(chunk.Length);
        }

        return WaitForCancellationAsync(ct);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        _written.AddRange(data.ToArray());
    }

    public void Flush()
    {
    }

    public void DrainInput()
    {
        _readChunks.Clear();
    }

    public void Dispose()
    {
    }

    private static async ValueTask<int> WaitForCancellationAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }
}
