using Hex1b.Terminal;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

public class ConsolePresentationAdapterTests
{
    [Fact]
    public async Task Constructor_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        // This test verifies the factory pattern works correctly
        // On unsupported platforms (Windows for now), should throw
        // On Linux/macOS, should succeed
        
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Throws<PlatformNotSupportedException>(() => 
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
                Assert.NotNull(adapter);
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
    
    [Fact]
    public void UnixConsoleDriver_OnlyAvailableOnUnixPlatforms()
    {
        if (OperatingSystem.IsLinux())
        {
            // Should be able to create the driver (but may fail without TTY)
            try
            {
                using var driver = new UnixConsoleDriver();
                Assert.NotNull(driver);
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
                Assert.NotNull(driver);
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
            Assert.Throws<PlatformNotSupportedException>(() => new UnixConsoleDriver());
#pragma warning restore CA1416
        }
    }
}

public class Hex1bTerminalTests_Workload
{
    [Fact]
    public void Constructor_HeadlessMode_CreatesWorkloadAdapter()
    {
        // Headless terminal (no presentation adapter)
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // WorkloadAdapter should implement the interface
        Assert.IsAssignableFrom<IHex1bAppTerminalWorkloadAdapter>(workload);
    }
    
    [Fact]
    public void GetSize_ReturnsConfiguredSize()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 100, 50);
        
        Assert.Equal(100, terminal.Width);
        Assert.Equal(50, terminal.Height);
    }
    
    [Fact]
    public void Write_CapturesOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        workload.Write("Hello");
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Hello"));
    }
    
    [Fact]
    public void InputEvents_ReturnsChannelReader()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        var reader = workload.InputEvents;
        Assert.NotNull(reader);
    }
    
    [Fact]
    public void Capabilities_ReturnsDefaults()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        var caps = workload.Capabilities;
        Assert.NotNull(caps);
        Assert.True(caps.SupportsMouse);
        Assert.True(caps.SupportsTrueColor);
    }
}
