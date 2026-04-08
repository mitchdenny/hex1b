using Hex1b.Tool.Commands.Terminal;

namespace Hex1b.Tool.Tests;

public class AttachTuiAppTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, true, false)]
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

        Assert.Equal(expected, shouldSend);
    }

    [Fact]
    public void CalculateResizeTarget_SubtractsAttachChromeFromDisplay()
    {
        var target = AttachTuiApp.CalculateResizeTarget(
            displayWidth: 80,
            displayHeight: 24,
            remoteWidth: 120,
            remoteHeight: 30);

        Assert.Equal((Width: 78, Height: 20), target);
    }

    [Fact]
    public void CalculateResizeTarget_UnchangedOrTooSmall_ReturnsNull()
    {
        Assert.Null(AttachTuiApp.CalculateResizeTarget(
            displayWidth: 82,
            displayHeight: 24,
            remoteWidth: 80,
            remoteHeight: 20));

        Assert.Null(AttachTuiApp.CalculateResizeTarget(
            displayWidth: 2,
            displayHeight: 4,
            remoteWidth: 80,
            remoteHeight: 20));
    }
}
