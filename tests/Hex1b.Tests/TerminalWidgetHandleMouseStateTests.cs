using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Regression tests for terminal-state flags on <see cref="TerminalWidgetHandle"/>
/// that gate input forwarding to the embedded workload (mouse tracking, alt-screen).
/// Focus: RIS (ESC c) must clear those flags, so a workload swap that prepends
/// RIS leaves the handle in a clean state for the new active child.
/// </summary>
[TestClass]
public class TerminalWidgetHandleMouseStateTests
{
    private static AppliedToken Applied(AnsiToken token)
        => AppliedToken.WithNoCellImpacts(token, 0, 0, 0, 0);

    [TestMethod]
    public async Task PrivateMode1003Enable_FlipsMouseTrackingEnabled()
    {
        var handle = new TerminalWidgetHandle(80, 24);

        Assert.IsFalse(handle.MouseTrackingEnabled);

        await handle.WriteOutputWithImpactsAsync([Applied(new PrivateModeToken(1003, true))]);

        Assert.IsTrue(handle.MouseTrackingEnabled);
    }

    [TestMethod]
    public async Task RisToken_ResetsMouseTrackingEnabled()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        await handle.WriteOutputWithImpactsAsync([Applied(new PrivateModeToken(1003, true))]);
        Assert.IsTrue(handle.MouseTrackingEnabled, "Precondition: workload enabled mouse tracking.");

        await handle.WriteOutputWithImpactsAsync([Applied(RisToken.Instance)]);

        Assert.IsFalse(handle.MouseTrackingEnabled,
            "RIS must reset mouse tracking — otherwise a placeholder that enabled mouse will leak forwarding to the next workload after a swap.");
    }

    [TestMethod]
    public async Task RisToken_ResetsAlternateScreen()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        await handle.WriteOutputWithImpactsAsync([Applied(new PrivateModeToken(1049, true))]);
        Assert.IsTrue(handle.InAlternateScreen);

        await handle.WriteOutputWithImpactsAsync([Applied(RisToken.Instance)]);

        Assert.IsFalse(handle.InAlternateScreen);
    }
}
