
namespace Hex1b.Tests;

public class KgpCommandParserTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsDefaults()
    {
        var cmd = KgpCommand.Parse("");

        Assert.Equal(KgpAction.Transmit, cmd.Action);
        Assert.Equal(KgpFormat.Rgba32, cmd.Format);
        Assert.Equal(KgpTransmissionMedium.Direct, cmd.Medium);
        Assert.Equal(0u, cmd.Width);
        Assert.Equal(0u, cmd.Height);
        Assert.Equal(0u, cmd.ImageId);
        Assert.Equal(0u, cmd.ImageNumber);
        Assert.Equal(0u, cmd.PlacementId);
        Assert.Equal(0, cmd.Quiet);
        Assert.Equal(0, cmd.MoreData);
        Assert.Null(cmd.Compression);
        Assert.Equal(0, cmd.ZIndex);
        Assert.Equal(KgpDeleteTarget.All, cmd.DeleteTarget);
    }

    [Fact]
    public void Parse_NullString_ReturnsDefaults()
    {
        var cmd = KgpCommand.Parse(null!);

        Assert.Equal(KgpAction.Transmit, cmd.Action);
    }

    // --- Action parsing ---

    [Theory]
    [InlineData("a=t", KgpAction.Transmit)]
    [InlineData("a=T", KgpAction.TransmitAndDisplay)]
    [InlineData("a=q", KgpAction.Query)]
    [InlineData("a=p", KgpAction.Put)]
    [InlineData("a=d", KgpAction.Delete)]
    [InlineData("a=f", KgpAction.AnimationFrame)]
    [InlineData("a=a", KgpAction.AnimationControl)]
    [InlineData("a=c", KgpAction.Compose)]
    public void Parse_ActionKey_CorrectAction(string controlData, KgpAction expected)
    {
        var cmd = KgpCommand.Parse(controlData);
        Assert.Equal(expected, cmd.Action);
    }

    [Fact]
    public void Parse_InvalidAction_DefaultsToTransmit()
    {
        var cmd = KgpCommand.Parse("a=Z");
        Assert.Equal(KgpAction.Transmit, cmd.Action);
    }

    // --- Format parsing ---

    [Theory]
    [InlineData("f=24", KgpFormat.Rgb24)]
    [InlineData("f=32", KgpFormat.Rgba32)]
    [InlineData("f=100", KgpFormat.Png)]
    public void Parse_FormatKey_CorrectFormat(string controlData, KgpFormat expected)
    {
        var cmd = KgpCommand.Parse(controlData);
        Assert.Equal(expected, cmd.Format);
    }

    [Fact]
    public void Parse_InvalidFormat_DefaultsToRgba32()
    {
        var cmd = KgpCommand.Parse("f=99");
        Assert.Equal(KgpFormat.Rgba32, cmd.Format);
    }

    // --- Transmission medium ---

    [Theory]
    [InlineData("t=d", KgpTransmissionMedium.Direct)]
    [InlineData("t=f", KgpTransmissionMedium.File)]
    [InlineData("t=t", KgpTransmissionMedium.TempFile)]
    [InlineData("t=s", KgpTransmissionMedium.SharedMemory)]
    public void Parse_MediumKey_CorrectMedium(string controlData, KgpTransmissionMedium expected)
    {
        var cmd = KgpCommand.Parse(controlData);
        Assert.Equal(expected, cmd.Medium);
    }

    // --- Integer keys ---

    [Fact]
    public void Parse_DimensionKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("s=100,v=200");

        Assert.Equal(100u, cmd.Width);
        Assert.Equal(200u, cmd.Height);
    }

    [Fact]
    public void Parse_ImageIdAndNumber_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("i=42,I=7");

        Assert.Equal(42u, cmd.ImageId);
        Assert.Equal(7u, cmd.ImageNumber);
    }

    [Fact]
    public void Parse_PlacementId_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("p=17");
        Assert.Equal(17u, cmd.PlacementId);
    }

    [Fact]
    public void Parse_MoreDataKey_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("m=1");
        Assert.Equal(1, cmd.MoreData);
    }

    [Fact]
    public void Parse_QuietKey_ParsedCorrectly()
    {
        var cmd0 = KgpCommand.Parse("q=0");
        Assert.Equal(0, cmd0.Quiet);

        var cmd1 = KgpCommand.Parse("q=1");
        Assert.Equal(1, cmd1.Quiet);

        var cmd2 = KgpCommand.Parse("q=2");
        Assert.Equal(2, cmd2.Quiet);
    }

    [Fact]
    public void Parse_CompressionKey_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("o=z");
        Assert.Equal('z', cmd.Compression);
    }

    [Fact]
    public void Parse_NoCompression_IsNull()
    {
        var cmd = KgpCommand.Parse("f=24");
        Assert.Null(cmd.Compression);
    }

    [Fact]
    public void Parse_FileSizeAndOffset_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("S=1024,O=512");

        Assert.Equal(1024u, cmd.FileSize);
        Assert.Equal(512u, cmd.FileOffset);
    }

    // --- Display keys ---

    [Fact]
    public void Parse_SourceRectKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("x=10,y=20,w=100,h=200");

        Assert.Equal(10u, cmd.SourceX);
        Assert.Equal(20u, cmd.SourceY);
        Assert.Equal(100u, cmd.SourceWidth);
        Assert.Equal(200u, cmd.SourceHeight);
    }

    [Fact]
    public void Parse_CellOffsetKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("X=3,Y=5");

        Assert.Equal(3u, cmd.CellOffsetX);
        Assert.Equal(5u, cmd.CellOffsetY);
    }

    [Fact]
    public void Parse_DisplaySizeKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("c=10,r=5");

        Assert.Equal(10u, cmd.DisplayColumns);
        Assert.Equal(5u, cmd.DisplayRows);
    }

    [Fact]
    public void Parse_CursorMovementPolicy_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("C=1");
        Assert.Equal(1, cmd.CursorMovement);
    }

    [Fact]
    public void Parse_UnicodePlaceholder_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("U=1");
        Assert.Equal(1, cmd.UnicodePlaceholder);
    }

    [Fact]
    public void Parse_ZIndex_ParsedCorrectly()
    {
        var cmdPos = KgpCommand.Parse("z=5");
        Assert.Equal(5, cmdPos.ZIndex);

        var cmdNeg = KgpCommand.Parse("z=-1");
        Assert.Equal(-1, cmdNeg.ZIndex);
    }

    // --- Relative placement keys ---

    [Fact]
    public void Parse_RelativePlacementKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("P=10,Q=3,H=2,V=-1");

        Assert.Equal(10u, cmd.ParentImageId);
        Assert.Equal(3u, cmd.ParentPlacementId);
        Assert.Equal(2, cmd.ParentOffsetH);
        Assert.Equal(-1, cmd.ParentOffsetV);
    }

    // --- Delete target ---

    [Theory]
    [InlineData("d=a", KgpDeleteTarget.All)]
    [InlineData("d=A", KgpDeleteTarget.AllFreeData)]
    [InlineData("d=i", KgpDeleteTarget.ById)]
    [InlineData("d=I", KgpDeleteTarget.ByIdFreeData)]
    [InlineData("d=n", KgpDeleteTarget.ByNumber)]
    [InlineData("d=N", KgpDeleteTarget.ByNumberFreeData)]
    [InlineData("d=c", KgpDeleteTarget.AtCursor)]
    [InlineData("d=C", KgpDeleteTarget.AtCursorFreeData)]
    [InlineData("d=p", KgpDeleteTarget.AtCell)]
    [InlineData("d=P", KgpDeleteTarget.AtCellFreeData)]
    [InlineData("d=q", KgpDeleteTarget.AtCellWithZIndex)]
    [InlineData("d=Q", KgpDeleteTarget.AtCellWithZIndexFreeData)]
    [InlineData("d=x", KgpDeleteTarget.ByColumn)]
    [InlineData("d=X", KgpDeleteTarget.ByColumnFreeData)]
    [InlineData("d=y", KgpDeleteTarget.ByRow)]
    [InlineData("d=Y", KgpDeleteTarget.ByRowFreeData)]
    [InlineData("d=z", KgpDeleteTarget.ByZIndex)]
    [InlineData("d=Z", KgpDeleteTarget.ByZIndexFreeData)]
    [InlineData("d=r", KgpDeleteTarget.ByRange)]
    [InlineData("d=R", KgpDeleteTarget.ByRangeFreeData)]
    [InlineData("d=f", KgpDeleteTarget.AnimationFrames)]
    [InlineData("d=F", KgpDeleteTarget.AnimationFramesFreeData)]
    public void Parse_DeleteTargetKey_CorrectTarget(string controlData, KgpDeleteTarget expected)
    {
        var cmd = KgpCommand.Parse($"a=d,{controlData}");
        Assert.Equal(expected, cmd.DeleteTarget);
    }

    // --- Complex multi-key parsing ---

    [Fact]
    public void Parse_FullTransmitAndDisplay_AllKeysParsed()
    {
        var cmd = KgpCommand.Parse("a=T,f=24,s=100,v=200,i=42,p=7,m=0,q=1,o=z,c=10,r=5,z=-1");

        Assert.Equal(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.Equal(KgpFormat.Rgb24, cmd.Format);
        Assert.Equal(100u, cmd.Width);
        Assert.Equal(200u, cmd.Height);
        Assert.Equal(42u, cmd.ImageId);
        Assert.Equal(7u, cmd.PlacementId);
        Assert.Equal(0, cmd.MoreData);
        Assert.Equal(1, cmd.Quiet);
        Assert.Equal('z', cmd.Compression);
        Assert.Equal(10u, cmd.DisplayColumns);
        Assert.Equal(5u, cmd.DisplayRows);
        Assert.Equal(-1, cmd.ZIndex);
    }

    [Fact]
    public void Parse_FullQueryCommand_AllKeysParsed()
    {
        var cmd = KgpCommand.Parse("i=31,s=1,v=1,a=q,t=d,f=24");

        Assert.Equal(KgpAction.Query, cmd.Action);
        Assert.Equal(31u, cmd.ImageId);
        Assert.Equal(1u, cmd.Width);
        Assert.Equal(1u, cmd.Height);
        Assert.Equal(KgpTransmissionMedium.Direct, cmd.Medium);
        Assert.Equal(KgpFormat.Rgb24, cmd.Format);
    }

    [Fact]
    public void Parse_DeleteWithPosition_AllKeysParsed()
    {
        var cmd = KgpCommand.Parse("a=d,d=p,x=3,y=4");

        Assert.Equal(KgpAction.Delete, cmd.Action);
        Assert.Equal(KgpDeleteTarget.AtCell, cmd.DeleteTarget);
        Assert.Equal(3u, cmd.SourceX);
        Assert.Equal(4u, cmd.SourceY);
    }

    // --- Edge cases ---

    [Fact]
    public void Parse_InvalidKeyValuePair_SkipsGracefully()
    {
        // Pairs without = or empty value should be skipped
        var cmd = KgpCommand.Parse("a=T,invalid,=bad,f=24");

        Assert.Equal(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.Equal(KgpFormat.Rgb24, cmd.Format);
    }

    [Fact]
    public void Parse_NonNumericValue_DefaultsToZero()
    {
        var cmd = KgpCommand.Parse("s=abc,v=xyz");

        Assert.Equal(0u, cmd.Width);
        Assert.Equal(0u, cmd.Height);
    }

    [Fact]
    public void Parse_MaxUInt32_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("i=4294967295");
        Assert.Equal(4294967295u, cmd.ImageId);
    }

    [Fact]
    public void Parse_LargeNegativeZIndex_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("z=-1073741824");
        Assert.Equal(-1073741824, cmd.ZIndex);
    }

    [Fact]
    public void Parse_DuplicateKeys_LastWins()
    {
        var cmd = KgpCommand.Parse("i=1,i=2,i=3");
        Assert.Equal(3u, cmd.ImageId);
    }

    [Fact]
    public void Parse_UnknownKey_Ignored()
    {
        var cmd = KgpCommand.Parse("a=T,unknown=99,i=1");

        Assert.Equal(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.Equal(1u, cmd.ImageId);
    }

    [Fact]
    public void Parse_KeyWithNoValue_Skipped()
    {
        var cmd = KgpCommand.Parse("a=T,i=");
        Assert.Equal(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.Equal(0u, cmd.ImageId);
    }

    [Fact]
    public void Parse_ChunkedContinuationMinimalKeys_ParsedCorrectly()
    {
        // Continuation chunks only need m and optionally q
        var cmd = KgpCommand.Parse("m=1");
        Assert.Equal(1, cmd.MoreData);
        Assert.Equal(KgpAction.Transmit, cmd.Action);
    }

    [Fact]
    public void Parse_AnimationControlKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("a=a,i=3,s=3,v=5");
        Assert.Equal(KgpAction.AnimationControl, cmd.Action);
        Assert.Equal(3u, cmd.ImageId);
        // Note: 's' maps to AnimationState for animation control, but in our parser
        // it maps to Width. This is acceptable since the same key has different
        // semantics based on action type — the caller interprets based on Action.
    }
}
