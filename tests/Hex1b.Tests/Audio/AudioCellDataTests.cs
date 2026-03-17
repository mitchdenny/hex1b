using Xunit;

namespace Hex1b.Tests.Audio;

public class AudioCellDataTests
{
    [Fact]
    public void Constructor_ClampsVolume()
    {
        var data = new AudioCellData(1, 150, false);
        Assert.Equal(100, data.Volume);

        var data2 = new AudioCellData(1, -10, false);
        Assert.Equal(0, data2.Volume);
    }

    [Fact]
    public void BuildPlacementPayload_IncludesAllFields()
    {
        var data = new AudioCellData(42, 80, true);
        var payload = data.BuildPlacementPayload(5, 10, 3);

        Assert.Contains("a=p", payload);
        Assert.Contains("i=42", payload);
        Assert.Contains("c=5", payload);
        Assert.Contains("R=10", payload);
        Assert.Contains("v=80", payload);
        Assert.Contains("l=1", payload);
        Assert.Contains("p=3", payload);
        Assert.Contains("q=2", payload);
        Assert.StartsWith("\x1b_A", payload);
        Assert.EndsWith("\x1b\\", payload);
    }

    [Fact]
    public void BuildPlacementPayload_OmitsLoopWhenFalse()
    {
        var data = new AudioCellData(1, 100, false);
        var payload = data.BuildPlacementPayload(0, 0);

        Assert.DoesNotContain("l=1", payload);
    }

    [Fact]
    public void BuildPlacementPayload_OmitsPlacementIdWhenZero()
    {
        var data = new AudioCellData(1, 100, false);
        var payload = data.BuildPlacementPayload(0, 0, 0);

        Assert.DoesNotContain("p=", payload);
    }

    [Fact]
    public void BuildTransmitChunks_NullPayload_ReturnsEmpty()
    {
        var data = new AudioCellData(1, 100, false, transmitPayload: null);
        var chunks = data.BuildTransmitChunks();
        Assert.Empty(chunks);
    }

    [Fact]
    public void BuildTransmitChunks_SmallPayload_SingleChunk()
    {
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var data = new AudioCellData(42, 100, false,
            transmitPayload: $"a=t,i=42,f=3,r=44100,q=2;{base64}");

        var chunks = data.BuildTransmitChunks();
        Assert.Single(chunks);
        Assert.StartsWith("\x1b_A", chunks[0]);
        Assert.Contains(base64, chunks[0]);
        Assert.EndsWith("\x1b\\", chunks[0]);
    }

    [Fact]
    public void BuildTransmitChunks_LargePayload_MultiplChunks()
    {
        // Create a payload larger than 4096 bytes
        var largeData = new byte[4000];
        Array.Fill(largeData, (byte)42);
        var base64 = Convert.ToBase64String(largeData);
        Assert.True(base64.Length > 4096);

        var data = new AudioCellData(1, 100, false,
            transmitPayload: $"a=t,i=1,f=3,r=44100,q=2;{base64}");

        var chunks = data.BuildTransmitChunks();
        Assert.True(chunks.Count > 1);

        // First chunk should have m=1 and full control data
        Assert.Contains("m=1", chunks[0]);
        Assert.Contains("a=t", chunks[0]);

        // Last chunk should have m=0
        Assert.Contains("m=0", chunks[^1]);
    }
}
