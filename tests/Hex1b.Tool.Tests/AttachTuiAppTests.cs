using Hex1b.Tool.Commands.Terminal;

namespace Hex1b.Tool.Tests;

[TestClass]
public class AttachTuiAppTests
{
    [TestMethod]
    [DataRow(true, true, true, true)]
    [DataRow(true, false, true, false)]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, true, false)]
    public void ShouldSendResizeForDisplayChange_RespectsLeaderAndInitialResize(
        bool isLeader,
        bool initialResizeRequested,
        bool isInitialSessionStart,
        bool expected)
    {
        var shouldSend = AttachTuiApp.ShouldSendResizeForDisplayChange(
            isLeader,
            initialResizeRequested,
            isInitialSessionStart);

        Assert.AreEqual(expected, shouldSend);
    }

    [TestMethod]
    public void CalculateResizeTarget_SubtractsAttachChromeFromDisplay()
    {
        var target = AttachTuiApp.CalculateResizeTarget(
            displayWidth: 80,
            displayHeight: 24,
            remoteWidth: 120,
            remoteHeight: 30);

        Assert.AreEqual((Width: 78, Height: 20), target);
    }

    [TestMethod]
    public void CalculateResizeTarget_UnchangedOrTooSmall_ReturnsNull()
    {
        Assert.IsNull(AttachTuiApp.CalculateResizeTarget(
            displayWidth: 82,
            displayHeight: 24,
            remoteWidth: 80,
            remoteHeight: 20));

        Assert.IsNull(AttachTuiApp.CalculateResizeTarget(
            displayWidth: 2,
            displayHeight: 4,
            remoteWidth: 80,
            remoteHeight: 20));
    }
}
