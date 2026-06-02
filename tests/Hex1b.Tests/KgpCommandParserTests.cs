
namespace Hex1b.Tests;

[TestClass]
public class KgpCommandParserTests
{
    [TestMethod]
    public void Parse_EmptyString_ReturnsDefaults()
    {
        var cmd = KgpCommand.Parse("");

        Assert.AreEqual(KgpAction.Transmit, cmd.Action);
        Assert.AreEqual(KgpFormat.Rgba32, cmd.Format);
        Assert.AreEqual(KgpTransmissionMedium.Direct, cmd.Medium);
        Assert.AreEqual(0u, cmd.Width);
        Assert.AreEqual(0u, cmd.Height);
        Assert.AreEqual(0u, cmd.ImageId);
        Assert.AreEqual(0u, cmd.ImageNumber);
        Assert.AreEqual(0u, cmd.PlacementId);
        Assert.AreEqual(0, cmd.Quiet);
        Assert.AreEqual(0, cmd.MoreData);
        Assert.IsNull(cmd.Compression);
        Assert.AreEqual(0, cmd.ZIndex);
        Assert.AreEqual(KgpDeleteTarget.All, cmd.DeleteTarget);
    }

    [TestMethod]
    public void Parse_NullString_ReturnsDefaults()
    {
        var cmd = KgpCommand.Parse(null!);

        Assert.AreEqual(KgpAction.Transmit, cmd.Action);
    }

    // --- Action parsing ---

    [TestMethod]
    [DataRow("a=t", KgpAction.Transmit)]
    [DataRow("a=T", KgpAction.TransmitAndDisplay)]
    [DataRow("a=q", KgpAction.Query)]
    [DataRow("a=p", KgpAction.Put)]
    [DataRow("a=d", KgpAction.Delete)]
    [DataRow("a=f", KgpAction.AnimationFrame)]
    [DataRow("a=a", KgpAction.AnimationControl)]
    [DataRow("a=c", KgpAction.Compose)]
    public void Parse_ActionKey_CorrectAction(string controlData, KgpAction expected)
    {
        var cmd = KgpCommand.Parse(controlData);
        Assert.AreEqual(expected, cmd.Action);
    }

    [TestMethod]
    public void Parse_InvalidAction_DefaultsToTransmit()
    {
        var cmd = KgpCommand.Parse("a=Z");
        Assert.AreEqual(KgpAction.Transmit, cmd.Action);
    }

    // --- Format parsing ---

    [TestMethod]
    [DataRow("f=24", KgpFormat.Rgb24)]
    [DataRow("f=32", KgpFormat.Rgba32)]
    [DataRow("f=100", KgpFormat.Png)]
    public void Parse_FormatKey_CorrectFormat(string controlData, KgpFormat expected)
    {
        var cmd = KgpCommand.Parse(controlData);
        Assert.AreEqual(expected, cmd.Format);
    }

    [TestMethod]
    public void Parse_InvalidFormat_DefaultsToRgba32()
    {
        var cmd = KgpCommand.Parse("f=99");
        Assert.AreEqual(KgpFormat.Rgba32, cmd.Format);
    }

    // --- Transmission medium ---

    [TestMethod]
    [DataRow("t=d", KgpTransmissionMedium.Direct)]
    [DataRow("t=f", KgpTransmissionMedium.File)]
    [DataRow("t=t", KgpTransmissionMedium.TempFile)]
    [DataRow("t=s", KgpTransmissionMedium.SharedMemory)]
    public void Parse_MediumKey_CorrectMedium(string controlData, KgpTransmissionMedium expected)
    {
        var cmd = KgpCommand.Parse(controlData);
        Assert.AreEqual(expected, cmd.Medium);
    }

    // --- Integer keys ---

    [TestMethod]
    public void Parse_DimensionKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("s=100,v=200");

        Assert.AreEqual(100u, cmd.Width);
        Assert.AreEqual(200u, cmd.Height);
    }

    [TestMethod]
    public void Parse_ImageIdAndNumber_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("i=42,I=7");

        Assert.AreEqual(42u, cmd.ImageId);
        Assert.AreEqual(7u, cmd.ImageNumber);
    }

    [TestMethod]
    public void Parse_PlacementId_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("p=17");
        Assert.AreEqual(17u, cmd.PlacementId);
    }

    [TestMethod]
    public void Parse_MoreDataKey_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("m=1");
        Assert.AreEqual(1, cmd.MoreData);
    }

    [TestMethod]
    public void Parse_QuietKey_ParsedCorrectly()
    {
        var cmd0 = KgpCommand.Parse("q=0");
        Assert.AreEqual(0, cmd0.Quiet);

        var cmd1 = KgpCommand.Parse("q=1");
        Assert.AreEqual(1, cmd1.Quiet);

        var cmd2 = KgpCommand.Parse("q=2");
        Assert.AreEqual(2, cmd2.Quiet);
    }

    [TestMethod]
    public void Parse_CompressionKey_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("o=z");
        Assert.AreEqual('z', cmd.Compression);
    }

    [TestMethod]
    public void Parse_NoCompression_IsNull()
    {
        var cmd = KgpCommand.Parse("f=24");
        Assert.IsNull(cmd.Compression);
    }

    [TestMethod]
    public void Parse_FileSizeAndOffset_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("S=1024,O=512");

        Assert.AreEqual(1024u, cmd.FileSize);
        Assert.AreEqual(512u, cmd.FileOffset);
    }

    // --- Display keys ---

    [TestMethod]
    public void Parse_SourceRectKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("x=10,y=20,w=100,h=200");

        Assert.AreEqual(10u, cmd.SourceX);
        Assert.AreEqual(20u, cmd.SourceY);
        Assert.AreEqual(100u, cmd.SourceWidth);
        Assert.AreEqual(200u, cmd.SourceHeight);
    }

    [TestMethod]
    public void Parse_CellOffsetKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("X=3,Y=5");

        Assert.AreEqual(3u, cmd.CellOffsetX);
        Assert.AreEqual(5u, cmd.CellOffsetY);
    }

    [TestMethod]
    public void Parse_DisplaySizeKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("c=10,r=5");

        Assert.AreEqual(10u, cmd.DisplayColumns);
        Assert.AreEqual(5u, cmd.DisplayRows);
    }

    [TestMethod]
    public void Parse_CursorMovementPolicy_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("C=1");
        Assert.AreEqual(1, cmd.CursorMovement);
    }

    [TestMethod]
    public void Parse_UnicodePlaceholder_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("U=1");
        Assert.AreEqual(1, cmd.UnicodePlaceholder);
    }

    [TestMethod]
    public void Parse_ZIndex_ParsedCorrectly()
    {
        var cmdPos = KgpCommand.Parse("z=5");
        Assert.AreEqual(5, cmdPos.ZIndex);

        var cmdNeg = KgpCommand.Parse("z=-1");
        Assert.AreEqual(-1, cmdNeg.ZIndex);
    }

    // --- Relative placement keys ---

    [TestMethod]
    public void Parse_RelativePlacementKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("P=10,Q=3,H=2,V=-1");

        Assert.AreEqual(10u, cmd.ParentImageId);
        Assert.AreEqual(3u, cmd.ParentPlacementId);
        Assert.AreEqual(2, cmd.ParentOffsetH);
        Assert.AreEqual(-1, cmd.ParentOffsetV);
    }

    // --- Delete target ---

    [TestMethod]
    [DataRow("d=a", KgpDeleteTarget.All)]
    [DataRow("d=A", KgpDeleteTarget.AllFreeData)]
    [DataRow("d=i", KgpDeleteTarget.ById)]
    [DataRow("d=I", KgpDeleteTarget.ByIdFreeData)]
    [DataRow("d=n", KgpDeleteTarget.ByNumber)]
    [DataRow("d=N", KgpDeleteTarget.ByNumberFreeData)]
    [DataRow("d=c", KgpDeleteTarget.AtCursor)]
    [DataRow("d=C", KgpDeleteTarget.AtCursorFreeData)]
    [DataRow("d=p", KgpDeleteTarget.AtCell)]
    [DataRow("d=P", KgpDeleteTarget.AtCellFreeData)]
    [DataRow("d=q", KgpDeleteTarget.AtCellWithZIndex)]
    [DataRow("d=Q", KgpDeleteTarget.AtCellWithZIndexFreeData)]
    [DataRow("d=x", KgpDeleteTarget.ByColumn)]
    [DataRow("d=X", KgpDeleteTarget.ByColumnFreeData)]
    [DataRow("d=y", KgpDeleteTarget.ByRow)]
    [DataRow("d=Y", KgpDeleteTarget.ByRowFreeData)]
    [DataRow("d=z", KgpDeleteTarget.ByZIndex)]
    [DataRow("d=Z", KgpDeleteTarget.ByZIndexFreeData)]
    [DataRow("d=r", KgpDeleteTarget.ByRange)]
    [DataRow("d=R", KgpDeleteTarget.ByRangeFreeData)]
    [DataRow("d=f", KgpDeleteTarget.AnimationFrames)]
    [DataRow("d=F", KgpDeleteTarget.AnimationFramesFreeData)]
    public void Parse_DeleteTargetKey_CorrectTarget(string controlData, KgpDeleteTarget expected)
    {
        var cmd = KgpCommand.Parse($"a=d,{controlData}");
        Assert.AreEqual(expected, cmd.DeleteTarget);
    }

    // --- Complex multi-key parsing ---

    [TestMethod]
    public void Parse_FullTransmitAndDisplay_AllKeysParsed()
    {
        var cmd = KgpCommand.Parse("a=T,f=24,s=100,v=200,i=42,p=7,m=0,q=1,o=z,c=10,r=5,z=-1");

        Assert.AreEqual(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.AreEqual(KgpFormat.Rgb24, cmd.Format);
        Assert.AreEqual(100u, cmd.Width);
        Assert.AreEqual(200u, cmd.Height);
        Assert.AreEqual(42u, cmd.ImageId);
        Assert.AreEqual(7u, cmd.PlacementId);
        Assert.AreEqual(0, cmd.MoreData);
        Assert.AreEqual(1, cmd.Quiet);
        Assert.AreEqual('z', cmd.Compression);
        Assert.AreEqual(10u, cmd.DisplayColumns);
        Assert.AreEqual(5u, cmd.DisplayRows);
        Assert.AreEqual(-1, cmd.ZIndex);
    }

    [TestMethod]
    public void Parse_FullQueryCommand_AllKeysParsed()
    {
        var cmd = KgpCommand.Parse("i=31,s=1,v=1,a=q,t=d,f=24");

        Assert.AreEqual(KgpAction.Query, cmd.Action);
        Assert.AreEqual(31u, cmd.ImageId);
        Assert.AreEqual(1u, cmd.Width);
        Assert.AreEqual(1u, cmd.Height);
        Assert.AreEqual(KgpTransmissionMedium.Direct, cmd.Medium);
        Assert.AreEqual(KgpFormat.Rgb24, cmd.Format);
    }

    [TestMethod]
    public void Parse_DeleteWithPosition_AllKeysParsed()
    {
        var cmd = KgpCommand.Parse("a=d,d=p,x=3,y=4");

        Assert.AreEqual(KgpAction.Delete, cmd.Action);
        Assert.AreEqual(KgpDeleteTarget.AtCell, cmd.DeleteTarget);
        Assert.AreEqual(3u, cmd.SourceX);
        Assert.AreEqual(4u, cmd.SourceY);
    }

    // --- Edge cases ---

    [TestMethod]
    public void Parse_InvalidKeyValuePair_SkipsGracefully()
    {
        // Pairs without = or empty value should be skipped
        var cmd = KgpCommand.Parse("a=T,invalid,=bad,f=24");

        Assert.AreEqual(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.AreEqual(KgpFormat.Rgb24, cmd.Format);
    }

    [TestMethod]
    public void Parse_NonNumericValue_DefaultsToZero()
    {
        var cmd = KgpCommand.Parse("s=abc,v=xyz");

        Assert.AreEqual(0u, cmd.Width);
        Assert.AreEqual(0u, cmd.Height);
    }

    [TestMethod]
    public void Parse_MaxUInt32_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("i=4294967295");
        Assert.AreEqual(4294967295u, cmd.ImageId);
    }

    [TestMethod]
    public void Parse_LargeNegativeZIndex_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("z=-1073741824");
        Assert.AreEqual(-1073741824, cmd.ZIndex);
    }

    [TestMethod]
    public void Parse_DuplicateKeys_LastWins()
    {
        var cmd = KgpCommand.Parse("i=1,i=2,i=3");
        Assert.AreEqual(3u, cmd.ImageId);
    }

    [TestMethod]
    public void Parse_UnknownKey_Ignored()
    {
        var cmd = KgpCommand.Parse("a=T,unknown=99,i=1");

        Assert.AreEqual(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.AreEqual(1u, cmd.ImageId);
    }

    [TestMethod]
    public void Parse_KeyWithNoValue_Skipped()
    {
        var cmd = KgpCommand.Parse("a=T,i=");
        Assert.AreEqual(KgpAction.TransmitAndDisplay, cmd.Action);
        Assert.AreEqual(0u, cmd.ImageId);
    }

    [TestMethod]
    public void Parse_ChunkedContinuationMinimalKeys_ParsedCorrectly()
    {
        // Continuation chunks only need m and optionally q
        var cmd = KgpCommand.Parse("m=1");
        Assert.AreEqual(1, cmd.MoreData);
        Assert.AreEqual(KgpAction.Transmit, cmd.Action);
    }

    [TestMethod]
    public void Parse_AnimationControlKeys_ParsedCorrectly()
    {
        var cmd = KgpCommand.Parse("a=a,i=3,s=3,v=5");
        Assert.AreEqual(KgpAction.AnimationControl, cmd.Action);
        Assert.AreEqual(3u, cmd.ImageId);
        // Note: 's' maps to AnimationState for animation control, but in our parser
        // it maps to Width. This is acceptable since the same key has different
        // semantics based on action type — the caller interprets based on Action.
    }
}
