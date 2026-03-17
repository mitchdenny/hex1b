using Xunit;

namespace Hex1b.Tests.Audio;

public class AudioCommandTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsDefaults()
    {
        var cmd = AudioCommand.Parse("");
        Assert.Equal(AudioAction.Transmit, cmd.Action);
        Assert.Equal(AudioFormat.Wav, cmd.Format);
        Assert.Equal(44100u, cmd.SampleRate);
        Assert.Equal(100, cmd.Volume);
        Assert.Equal(0, cmd.MoreData);
    }

    [Fact]
    public void Parse_TransmitCommand_ParsesAllFields()
    {
        var cmd = AudioCommand.Parse("a=t,i=42,f=3,r=22050,q=2");
        Assert.Equal(AudioAction.Transmit, cmd.Action);
        Assert.Equal(42u, cmd.ClipId);
        Assert.Equal(AudioFormat.Wav, cmd.Format);
        Assert.Equal(22050u, cmd.SampleRate);
        Assert.Equal(2, cmd.Quiet);
    }

    [Fact]
    public void Parse_PlaceCommand_ParsesPositionAndVolume()
    {
        var cmd = AudioCommand.Parse("a=p,i=1,c=5,R=10,v=80,l=1,p=3");
        Assert.Equal(AudioAction.Place, cmd.Action);
        Assert.Equal(1u, cmd.ClipId);
        Assert.Equal(5, cmd.Column);
        Assert.Equal(10, cmd.Row);
        Assert.Equal(80, cmd.Volume);
        Assert.Equal(1, cmd.Loop);
        Assert.Equal(3u, cmd.PlacementId);
    }

    [Fact]
    public void Parse_StopCommand()
    {
        var cmd = AudioCommand.Parse("a=s,i=42,p=1");
        Assert.Equal(AudioAction.Stop, cmd.Action);
        Assert.Equal(42u, cmd.ClipId);
        Assert.Equal(1u, cmd.PlacementId);
    }

    [Fact]
    public void Parse_DeleteAllCommand()
    {
        var cmd = AudioCommand.Parse("a=d,d=a");
        Assert.Equal(AudioAction.Delete, cmd.Action);
        Assert.Equal(AudioDeleteTarget.All, cmd.DeleteTarget);
    }

    [Fact]
    public void Parse_DeleteByIdCommand()
    {
        var cmd = AudioCommand.Parse("a=d,d=i,i=42");
        Assert.Equal(AudioAction.Delete, cmd.Action);
        Assert.Equal(AudioDeleteTarget.ById, cmd.DeleteTarget);
        Assert.Equal(42u, cmd.ClipId);
    }

    [Fact]
    public void Parse_Pcm16MonoFormat()
    {
        var cmd = AudioCommand.Parse("a=t,f=1,r=8000");
        Assert.Equal(AudioFormat.Pcm16Mono, cmd.Format);
        Assert.Equal(8000u, cmd.SampleRate);
    }

    [Fact]
    public void Parse_Pcm16StereoFormat()
    {
        var cmd = AudioCommand.Parse("a=t,f=2");
        Assert.Equal(AudioFormat.Pcm16Stereo, cmd.Format);
    }

    [Fact]
    public void Parse_ChunkedTransmit()
    {
        var cmd = AudioCommand.Parse("a=t,i=1,f=3,m=1");
        Assert.Equal(1, cmd.MoreData);
    }

    [Fact]
    public void Parse_InvalidKey_IgnoredGracefully()
    {
        var cmd = AudioCommand.Parse("a=t,INVALID=xyz,i=5");
        Assert.Equal(AudioAction.Transmit, cmd.Action);
        Assert.Equal(5u, cmd.ClipId);
    }
}
