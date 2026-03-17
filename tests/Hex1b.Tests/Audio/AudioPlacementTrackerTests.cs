using Hex1b.Surfaces;
using Xunit;

namespace Hex1b.Tests.Audio;

public class AudioPlacementTrackerTests
{
    [Fact]
    public void GenerateCommands_EmptySurface_ReturnsEmpty()
    {
        var tracker = new AudioPlacementTracker();
        var surface = new Surface(10, 5);

        var commands = tracker.GenerateCommands(surface);
        Assert.Empty(commands);
    }

    [Fact]
    public void GenerateCommands_NewProducer_EmitsTransmitAndPlace()
    {
        var tracker = new AudioPlacementTracker();
        var surface = new Surface(10, 5);

        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var audioData = new AudioCellData(42, 80, true,
            transmitPayload: $"a=t,i=42,f=3,r=44100,q=2;{base64}");
        var tracked = new TrackedObject<AudioCellData>(audioData, _ => { });

        surface[3, 2] = new SurfaceCell("*", null, null, Audio: tracked);

        var commands = tracker.GenerateCommands(surface);

        // Should have at least transmit + place
        Assert.True(commands.Count >= 2);

        // First command should be transmit
        Assert.Contains("a=t", commands[0]);
        Assert.Contains("i=42", commands[0]);

        // Last command should be place
        var placeCmd = commands[^1];
        Assert.Contains("a=p", placeCmd);
        Assert.Contains("i=42", placeCmd);
        Assert.Contains("c=3", placeCmd);
        Assert.Contains("R=2", placeCmd);
        Assert.Contains("v=80", placeCmd);
    }

    [Fact]
    public void GenerateCommands_SameProducerNextFrame_NoCommands()
    {
        var tracker = new AudioPlacementTracker();
        var surface = new Surface(10, 5);

        var audioData = new AudioCellData(1, 100, false,
            transmitPayload: $"a=t,i=1,f=3,r=44100,q=2;{Convert.ToBase64String(new byte[] { 1 })}");
        var tracked = new TrackedObject<AudioCellData>(audioData, _ => { });

        surface[0, 0] = new SurfaceCell(".", null, null, Audio: tracked);

        // First frame
        tracker.GenerateCommands(surface);

        // Second frame with same producer at same position
        var commands = tracker.GenerateCommands(surface);
        Assert.Empty(commands);
    }

    [Fact]
    public void GenerateCommands_ProducerRemoved_EmitsDelete()
    {
        var tracker = new AudioPlacementTracker();

        // Frame 1: producer present
        var surface1 = new Surface(10, 5);
        var audioData = new AudioCellData(1, 100, false,
            transmitPayload: $"a=t,i=1,f=3,r=44100,q=2;{Convert.ToBase64String(new byte[] { 1 })}");
        var tracked = new TrackedObject<AudioCellData>(audioData, _ => { });
        surface1[0, 0] = new SurfaceCell(".", null, null, Audio: tracked);
        tracker.GenerateCommands(surface1);

        // Frame 2: producer removed
        var surface2 = new Surface(10, 5);
        var commands = tracker.GenerateCommands(surface2);

        Assert.Single(commands);
        Assert.Contains("a=d", commands[0]);
        Assert.Contains("i=1", commands[0]);
    }

    [Fact]
    public void GenerateCommands_ProducerMoved_EmitsNewPlacement()
    {
        var tracker = new AudioPlacementTracker();

        // Frame 1
        var surface1 = new Surface(10, 5);
        var audioData = new AudioCellData(1, 100, false,
            transmitPayload: $"a=t,i=1,f=3,r=44100,q=2;{Convert.ToBase64String(new byte[] { 1 })}");
        var tracked = new TrackedObject<AudioCellData>(audioData, _ => { });
        surface1[0, 0] = new SurfaceCell(".", null, null, Audio: tracked);
        tracker.GenerateCommands(surface1);

        // Frame 2: moved to different position
        var surface2 = new Surface(10, 5);
        surface2[5, 3] = new SurfaceCell(".", null, null, Audio: tracked);
        var commands = tracker.GenerateCommands(surface2);

        // Should emit a placement at new position (not transmit, since clip is cached)
        Assert.True(commands.Count >= 1);
        var placeCmd = commands.FirstOrDefault(c => c.Contains("a=p"));
        Assert.NotNull(placeCmd);
        Assert.Contains("c=5", placeCmd);
        Assert.Contains("R=3", placeCmd);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tracker = new AudioPlacementTracker();
        var surface = new Surface(10, 5);
        var audioData = new AudioCellData(1, 100, false,
            transmitPayload: $"a=t,i=1,f=3,r=44100,q=2;{Convert.ToBase64String(new byte[] { 1 })}");
        var tracked = new TrackedObject<AudioCellData>(audioData, _ => { });
        surface[0, 0] = new SurfaceCell(".", null, null, Audio: tracked);

        tracker.GenerateCommands(surface);
        tracker.Reset();

        // After reset, same surface should re-transmit
        var commands = tracker.GenerateCommands(surface);
        Assert.Contains(commands, c => c.Contains("a=t"));
    }
}
