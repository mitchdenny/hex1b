namespace Hex1b.Tests;

[TestClass]
public class KgpCommandParserTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_EmptyOrNullControlData_ReturnsDefaultTransmit()
    {
        var empty = KgpCommand.Parse("");
        var nullValue = KgpCommand.Parse(null!);

        Assert.AreEqual(KgpAction.Transmit, empty.Action);
        Assert.AreEqual(KgpFormat.Rgba32, empty.Format);
        Assert.AreEqual(KgpTransmissionMedium.Direct, empty.Medium);
        Assert.AreEqual(0, empty.Quiet);
        Assert.AreEqual(0, empty.MoreData);
        Assert.AreEqual(0u, empty.UsageHints);
        Assert.AreEqual(KgpAction.Transmit, nullValue.Action);
    }

    [TestMethod]
    [DataRow("a=t", KgpAction.Transmit)]
    [DataRow("a=T", KgpAction.TransmitAndDisplay)]
    [DataRow("a=q", KgpAction.Query)]
    [DataRow("a=p", KgpAction.Put)]
    [DataRow("a=d", KgpAction.Delete)]
    [DataRow("a=f", KgpAction.AnimationFrame)]
    [DataRow("a=a", KgpAction.AnimationControl)]
    [DataRow("a=c", KgpAction.Compose)]
    [DataRow("i=1,a=p,p=2", KgpAction.Put)]
    public void Parse_ActionValue_ReturnsExpectedAction(
        string controlData,
        KgpAction expected)
    {
        var command = KgpCommand.Parse(controlData);

        Assert.AreEqual(expected, command.Action);
    }

    [TestMethod]
    public void Parse_TransmissionKeys_ReturnsTypedTransmission()
    {
        var parsed = ParseTyped<KgpParsedCommand.Transmit>(
            "a=t,q=1,f=24,t=s,s=10,v=20,S=30,O=40,i=50,p=70,o=z,m=1,N=3");

        Assert.AreEqual(KgpParsedCommand.QuietMode.SuppressSuccess, parsed.Quiet);
        Assert.AreEqual(KgpFormat.Rgb24, parsed.Transmission.Format);
        Assert.AreEqual(KgpTransmissionMedium.SharedMemory, parsed.Transmission.Medium);
        Assert.AreEqual(10u, parsed.Transmission.Width);
        Assert.AreEqual(20u, parsed.Transmission.Height);
        Assert.AreEqual(30u, parsed.Transmission.FileSize);
        Assert.AreEqual(40u, parsed.Transmission.FileOffset);
        Assert.AreEqual(50u, parsed.Transmission.ImageId);
        Assert.AreEqual(0u, parsed.Transmission.ImageNumber);
        Assert.AreEqual(
            KgpParsedCommand.ImageIdentityKind.ExplicitId,
            parsed.Transmission.IdentityKind);
        Assert.AreEqual(70u, parsed.Transmission.PlacementId);
        Assert.AreEqual(KgpParsedCommand.CompressionMode.Zlib, parsed.Transmission.Compression);
        Assert.IsTrue(parsed.Transmission.MoreData);
        Assert.AreEqual(3u, parsed.Transmission.UsageHints);

        var compatibility = KgpCommand.Parse(
            "a=t,q=1,f=24,t=s,s=10,v=20,S=30,O=40,i=50,p=70,o=z,m=1,N=3");
        Assert.AreEqual(3u, compatibility.UsageHints);
        Assert.AreEqual('z', compatibility.Compression);

        var numbered = ParseTyped<KgpParsedCommand.Transmit>("a=t,I=60");
        Assert.AreEqual(60u, numbered.Transmission.ImageNumber);
        Assert.AreEqual(
            KgpParsedCommand.ImageIdentityKind.Number,
            numbered.Transmission.IdentityKind);
    }

    [TestMethod]
    public void Parse_QueryKeys_ReturnsTypedTransmissionWithoutChangingAction()
    {
        var parsed = ParseTyped<KgpParsedCommand.Query>(
            "i=31,s=1,v=1,a=q,t=d,f=24,N=1");

        Assert.AreEqual(31u, parsed.Transmission.ImageId);
        Assert.AreEqual(1u, parsed.Transmission.Width);
        Assert.AreEqual(1u, parsed.Transmission.Height);
        Assert.AreEqual(KgpTransmissionMedium.Direct, parsed.Transmission.Medium);
        Assert.AreEqual(KgpFormat.Rgb24, parsed.Transmission.Format);
        Assert.AreEqual(1u, parsed.Transmission.UsageHints);
    }

    [TestMethod]
    public void Parse_TransmitAndDisplayKeys_ReturnsSeparateTypedComponents()
    {
        var parsed = ParseTyped<KgpParsedCommand.TransmitAndDisplay>(
            "a=T,f=24,t=d,s=100,v=200,S=300,O=400,i=42,p=7,o=z,m=1,N=1," +
            "x=10,y=20,w=30,h=40,X=3,Y=5,c=6,r=8,C=1,U=1,z=-9,P=11,Q=12,H=-13,V=14");

        Assert.AreEqual(100u, parsed.Transmission.Width);
        Assert.AreEqual(200u, parsed.Transmission.Height);
        Assert.AreEqual(1u, parsed.Transmission.UsageHints);
        Assert.AreEqual(42u, parsed.Display.ImageId);
        Assert.AreEqual(0u, parsed.Display.ImageNumber);
        Assert.AreEqual(7u, parsed.Display.PlacementId);
        Assert.AreEqual(10u, parsed.Display.SourceX);
        Assert.AreEqual(20u, parsed.Display.SourceY);
        Assert.AreEqual(30u, parsed.Display.SourceWidth);
        Assert.AreEqual(40u, parsed.Display.SourceHeight);
        Assert.AreEqual(3u, parsed.Display.CellOffsetX);
        Assert.AreEqual(5u, parsed.Display.CellOffsetY);
        Assert.AreEqual(6u, parsed.Display.Columns);
        Assert.AreEqual(8u, parsed.Display.Rows);
        Assert.IsTrue(parsed.Display.SuppressCursorMovement);
        Assert.IsTrue(parsed.Display.UnicodePlaceholder);
        Assert.AreEqual(-9, parsed.Display.ZIndex);
        Assert.AreEqual(11u, parsed.Display.ParentImageId);
        Assert.AreEqual(12u, parsed.Display.ParentPlacementId);
        Assert.AreEqual(-13, parsed.Display.ParentOffsetHorizontal);
        Assert.AreEqual(14, parsed.Display.ParentOffsetVertical);

        var numbered = ParseTyped<KgpParsedCommand.TransmitAndDisplay>("a=T,I=43");
        Assert.AreEqual(43u, numbered.Transmission.ImageNumber);
        Assert.AreEqual(43u, numbered.Display.ImageNumber);
    }

    [TestMethod]
    public void Parse_PutKeys_ReturnsTypedDisplay()
    {
        var parsed = ParseTyped<KgpParsedCommand.Put>(
            "a=p,i=1,p=3,x=4,y=5,w=6,h=7,X=8,Y=9,c=10,r=11,C=2,U=3," +
            "z=-12,P=13,Q=14,H=-15,V=16");

        Assert.AreEqual(1u, parsed.Display.ImageId);
        Assert.AreEqual(0u, parsed.Display.ImageNumber);
        Assert.AreEqual(3u, parsed.Display.PlacementId);
        Assert.AreEqual(4u, parsed.Display.SourceX);
        Assert.AreEqual(5u, parsed.Display.SourceY);
        Assert.AreEqual(6u, parsed.Display.SourceWidth);
        Assert.AreEqual(7u, parsed.Display.SourceHeight);
        Assert.AreEqual(8u, parsed.Display.CellOffsetX);
        Assert.AreEqual(9u, parsed.Display.CellOffsetY);
        Assert.AreEqual(10u, parsed.Display.Columns);
        Assert.AreEqual(11u, parsed.Display.Rows);
        Assert.IsFalse(parsed.Display.SuppressCursorMovement);
        Assert.IsTrue(parsed.Display.UnicodePlaceholder);
        Assert.AreEqual(-12, parsed.Display.ZIndex);
        Assert.AreEqual(13u, parsed.Display.ParentImageId);
        Assert.AreEqual(14u, parsed.Display.ParentPlacementId);
        Assert.AreEqual(-15, parsed.Display.ParentOffsetHorizontal);
        Assert.AreEqual(16, parsed.Display.ParentOffsetVertical);

        var numbered = ParseTyped<KgpParsedCommand.Put>("a=p,I=2");
        Assert.AreEqual(2u, numbered.Display.ImageNumber);
    }

    [TestMethod]
    [DataRow("a", KgpDeleteTarget.All)]
    [DataRow("A", KgpDeleteTarget.AllFreeData)]
    [DataRow("i", KgpDeleteTarget.ById)]
    [DataRow("I", KgpDeleteTarget.ByIdFreeData)]
    [DataRow("n", KgpDeleteTarget.ByNumber)]
    [DataRow("N", KgpDeleteTarget.ByNumberFreeData)]
    [DataRow("c", KgpDeleteTarget.AtCursor)]
    [DataRow("C", KgpDeleteTarget.AtCursorFreeData)]
    [DataRow("f", KgpDeleteTarget.AnimationFrames)]
    [DataRow("F", KgpDeleteTarget.AnimationFramesFreeData)]
    [DataRow("p", KgpDeleteTarget.AtCell)]
    [DataRow("P", KgpDeleteTarget.AtCellFreeData)]
    [DataRow("q", KgpDeleteTarget.AtCellWithZIndex)]
    [DataRow("Q", KgpDeleteTarget.AtCellWithZIndexFreeData)]
    [DataRow("r", KgpDeleteTarget.ByRange)]
    [DataRow("R", KgpDeleteTarget.ByRangeFreeData)]
    [DataRow("x", KgpDeleteTarget.ByColumn)]
    [DataRow("X", KgpDeleteTarget.ByColumnFreeData)]
    [DataRow("y", KgpDeleteTarget.ByRow)]
    [DataRow("Y", KgpDeleteTarget.ByRowFreeData)]
    [DataRow("z", KgpDeleteTarget.ByZIndex)]
    [DataRow("Z", KgpDeleteTarget.ByZIndexFreeData)]
    public void Parse_DeleteTarget_ReturnsExpectedCompatibilityTarget(
        string target,
        KgpDeleteTarget expected)
    {
        var command = KgpCommand.Parse($"a=d,d={target}");

        Assert.AreEqual(expected, command.DeleteTarget);
    }

    [TestMethod]
    public void Parse_DeleteSelectors_ReturnTypedOperands()
    {
        var byId = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.ById>(
            "a=d,d=I,i=10,p=11");
        Assert.IsTrue(byId.FreeData);
        Assert.AreEqual(10u, byId.ImageId);
        Assert.AreEqual(11u, byId.PlacementId);

        var byNumber = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.ByNumber>(
            "a=d,d=n,I=12,p=13");
        Assert.IsFalse(byNumber.FreeData);
        Assert.AreEqual(12u, byNumber.ImageNumber);
        Assert.AreEqual(13u, byNumber.PlacementId);

        var atCell = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.AtCell>(
            "a=d,d=P,x=14,y=15");
        Assert.IsTrue(atCell.FreeData);
        Assert.AreEqual(14u, atCell.X);
        Assert.AreEqual(15u, atCell.Y);

        var atCellWithZ = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.AtCellWithZIndex>(
            "a=d,d=q,x=16,y=17,z=-18");
        Assert.AreEqual(16u, atCellWithZ.X);
        Assert.AreEqual(17u, atCellWithZ.Y);
        Assert.AreEqual(-18, atCellWithZ.ZIndex);

        var column = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.ByColumn>(
            "a=d,d=X,x=19");
        Assert.IsTrue(column.FreeData);
        Assert.AreEqual(19u, column.Column);

        var row = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.ByRow>(
            "a=d,d=y,y=20");
        Assert.AreEqual(20u, row.Row);

        var zIndex = ParseDeleteSelector<KgpParsedCommand.DeleteSelector.ByZIndex>(
            "a=d,d=Z,z=-21");
        Assert.IsTrue(zIndex.FreeData);
        Assert.AreEqual(-21, zIndex.ZIndex);
    }

    [TestMethod]
    public void Parse_DeleteFrame_UsesRAsFrameNumberAndSingleIdentity()
    {
        var parsed = ParseTyped<KgpParsedCommand.Delete>("a=d,d=F,i=3,r=5");
        var selector = TestSeq.IsType<KgpParsedCommand.DeleteSelector.AnimationFrames>(
            parsed.Selector);

        Assert.IsTrue(selector.FreeData);
        Assert.AreEqual(3u, selector.ImageId);
        Assert.AreEqual(0u, selector.ImageNumber);
        Assert.AreEqual(5u, selector.FrameNumber);

        var byNumber = ParseTyped<KgpParsedCommand.Delete>("a=d,d=F,I=4,r=5");
        var numberedSelector = TestSeq.IsType<KgpParsedCommand.DeleteSelector.AnimationFrames>(
            byNumber.Selector);
        Assert.AreEqual(0u, numberedSelector.ImageId);
        Assert.AreEqual(4u, numberedSelector.ImageNumber);

        var compatibility = KgpCommand.Parse("a=d,d=F,i=3,r=5");
        Assert.AreEqual(5u, compatibility.DisplayRows);
    }

    [TestMethod]
    public void Parse_DeleteRange_UsesXAndYAndDoesNotReinterpretR()
    {
        var parsed = ParseTyped<KgpParsedCommand.Delete>(
            "a=d,d=R,x=10,y=20,r=99");
        var selector = TestSeq.IsType<KgpParsedCommand.DeleteSelector.ByRange>(
            parsed.Selector);

        Assert.IsTrue(selector.FreeData);
        Assert.AreEqual(10u, selector.FirstImageId);
        Assert.AreEqual(20u, selector.LastImageId);

        var compatibility = KgpCommand.Parse("a=d,d=R,x=10,y=20,r=99");
        Assert.AreEqual(10u, compatibility.SourceX);
        Assert.AreEqual(20u, compatibility.SourceY);
        Assert.AreEqual(0u, compatibility.DisplayRows);
    }

    [TestMethod]
    public void Parse_AnimationFrameKeys_ReturnsActionSpecificMeanings()
    {
        var parsed = ParseTyped<KgpParsedCommand.AnimationFrame>(
            "a=f,f=32,t=d,s=100,v=200,S=300,O=400,i=5,p=7,o=z,m=1,N=1," +
            "x=8,y=9,c=10,r=11,z=-12,X=1,Y=4278190335");

        Assert.AreEqual(100u, parsed.Transmission.Width);
        Assert.AreEqual(200u, parsed.Transmission.Height);
        Assert.AreEqual(1u, parsed.Transmission.UsageHints);
        Assert.AreEqual(8u, parsed.Frame.X);
        Assert.AreEqual(9u, parsed.Frame.Y);
        Assert.AreEqual(10u, parsed.Frame.BaseFrameNumber);
        Assert.AreEqual(11u, parsed.Frame.EditFrameNumber);
        Assert.AreEqual(-12, parsed.Frame.Gap);
        Assert.AreEqual(KgpParsedCommand.CompositionMode.Overwrite, parsed.Frame.Composition);
        Assert.AreEqual(4278190335u, parsed.Frame.BackgroundColor);

        var numbered = ParseTyped<KgpParsedCommand.AnimationFrame>("a=f,I=6");
        Assert.AreEqual(6u, numbered.Transmission.ImageNumber);

        var compatibility = KgpCommand.Parse(
            "a=f,s=100,v=200,x=8,y=9,c=10,r=11,z=-12,X=1,Y=4278190335");
        Assert.AreEqual(10u, compatibility.DisplayColumns);
        Assert.AreEqual(11u, compatibility.DisplayRows);
        Assert.AreEqual(-12, compatibility.ZIndex);
        Assert.AreEqual(1u, compatibility.CellOffsetX);
        Assert.AreEqual(4278190335u, compatibility.CellOffsetY);
    }

    [TestMethod]
    public void Parse_AnimationControlKeys_DoNotPopulateImageDimensions()
    {
        var parsed = ParseTyped<KgpParsedCommand.AnimationControl>(
            "a=a,i=3,p=5,s=3,v=6,c=7,r=8,z=-9");

        Assert.AreEqual(3u, parsed.Control.ImageId);
        Assert.AreEqual(0u, parsed.Control.ImageNumber);
        Assert.AreEqual(5u, parsed.Control.PlacementId);
        Assert.AreEqual(KgpParsedCommand.AnimationPlaybackState.Running, parsed.Control.State);
        Assert.AreEqual(6u, parsed.Control.LoopCount);
        Assert.AreEqual(7u, parsed.Control.CurrentFrameNumber);
        Assert.AreEqual(8u, parsed.Control.AffectedFrameNumber);
        Assert.AreEqual(-9, parsed.Control.Gap);

        var numbered = ParseTyped<KgpParsedCommand.AnimationControl>("a=a,I=4");
        Assert.AreEqual(4u, numbered.Control.ImageNumber);

        var compatibility = KgpCommand.Parse("a=a,i=3,s=3,v=6,c=7,r=8,z=-9");
        Assert.AreEqual(3, compatibility.AnimationState);
        Assert.AreEqual(6u, compatibility.LoopCount);
        Assert.AreEqual(0u, compatibility.Width);
        Assert.AreEqual(0u, compatibility.Height);
        Assert.AreEqual(7u, compatibility.DisplayColumns);
        Assert.AreEqual(8u, compatibility.DisplayRows);
        Assert.AreEqual(-9, compatibility.ZIndex);
    }

    [TestMethod]
    public void Parse_UnknownAnimationState_RemainsExplicitNoOp()
    {
        var parsed = ParseTyped<KgpParsedCommand.AnimationControl>("a=a,s=4294967295");

        Assert.AreEqual(KgpParsedCommand.AnimationPlaybackState.None, parsed.Control.State);
        Assert.AreEqual(0, KgpCommand.Parse("a=a,s=4294967295").AnimationState);
    }

    [TestMethod]
    public void Parse_CompositionKeys_ReturnsActionSpecificMeanings()
    {
        var parsed = ParseTyped<KgpParsedCommand.Compose>(
            "a=c,i=1,p=3,c=4,r=5,x=6,y=7,w=8,h=9,X=10,Y=11,C=2");

        Assert.AreEqual(1u, parsed.Composition.ImageId);
        Assert.AreEqual(0u, parsed.Composition.ImageNumber);
        Assert.AreEqual(3u, parsed.Composition.PlacementId);
        Assert.AreEqual(4u, parsed.Composition.DestinationFrameNumber);
        Assert.AreEqual(5u, parsed.Composition.SourceFrameNumber);
        Assert.AreEqual(6u, parsed.Composition.DestinationX);
        Assert.AreEqual(7u, parsed.Composition.DestinationY);
        Assert.AreEqual(8u, parsed.Composition.Width);
        Assert.AreEqual(9u, parsed.Composition.Height);
        Assert.AreEqual(10u, parsed.Composition.SourceX);
        Assert.AreEqual(11u, parsed.Composition.SourceY);
        Assert.AreEqual(KgpParsedCommand.CompositionMode.Overwrite, parsed.Composition.Composition);

        var numbered = ParseTyped<KgpParsedCommand.Compose>("a=c,I=2");
        Assert.AreEqual(2u, numbered.Composition.ImageNumber);
    }

    [TestMethod]
    public void Parse_InapplicableKnownKeys_AreValidatedButNotExposed()
    {
        var parsed = ParseTyped<KgpParsedCommand.Put>("a=p,f=24,t=s,s=7,v=8,N=1,i=9");

        Assert.AreEqual(9u, parsed.Display.ImageId);
        var compatibility = KgpCommand.Parse("a=p,f=24,t=s,s=7,v=8,N=1,i=9");
        Assert.AreEqual(KgpFormat.Rgba32, compatibility.Format);
        Assert.AreEqual(KgpTransmissionMedium.Direct, compatibility.Medium);
        Assert.AreEqual(0u, compatibility.Width);
        Assert.AreEqual(0u, compatibility.Height);
        Assert.AreEqual(0u, compatibility.UsageHints);

        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse("a=p,f=99"));
    }

    [TestMethod]
    public void Parse_MinimalContinuations_RemainRepresentable()
    {
        var transmit = ParseTyped<KgpParsedCommand.Transmit>("m=1,q=1");
        var animationFrame = ParseTyped<KgpParsedCommand.AnimationFrame>("a=f,m=0,q=2");

        Assert.IsTrue(transmit.Transmission.MoreData);
        Assert.AreEqual(KgpParsedCommand.QuietMode.SuppressSuccess, transmit.Quiet);
        Assert.IsFalse(animationFrame.Transmission.MoreData);
        Assert.AreEqual(KgpParsedCommand.QuietMode.SuppressAll, animationFrame.Quiet);
    }

    [TestMethod]
    public void Parse_QuietAndBooleanLikeControls_UseKittyCompatiblePolicies()
    {
        var quiet = ParseTyped<KgpParsedCommand.Transmit>("q=4294967295,m=2");
        var display = ParseTyped<KgpParsedCommand.Put>("a=p,C=2,U=2");
        var frame = ParseTyped<KgpParsedCommand.AnimationFrame>("a=f,X=2");
        var composition = ParseTyped<KgpParsedCommand.Compose>("a=c,C=2");

        Assert.AreEqual(KgpParsedCommand.QuietMode.SuppressAll, quiet.Quiet);
        Assert.IsTrue(quiet.Transmission.MoreData);
        Assert.IsFalse(display.Display.SuppressCursorMovement);
        Assert.IsTrue(display.Display.UnicodePlaceholder);
        Assert.AreEqual(KgpParsedCommand.CompositionMode.AlphaBlend, frame.Frame.Composition);
        Assert.AreEqual(
            KgpParsedCommand.CompositionMode.Overwrite,
            composition.Composition.Composition);
    }

    [TestMethod]
    public void Parse_DuplicateValidKeys_LastValueWins()
    {
        var command = KgpCommand.Parse("a=t,a=p,i=1,i=2,i=3");

        Assert.AreEqual(KgpAction.Put, command.Action);
        Assert.AreEqual(3u, command.ImageId);
    }

    [TestMethod]
    public void Parse_DuplicateWithInvalidOccurrence_RejectsWholeCommand()
    {
        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse("a=t,i=bad,i=2"));
        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse("a=t,f=99,f=24"));
        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse("a=d,d=?,d=a"));
    }

    [TestMethod]
    public void Parse_UnknownSingleAlphabeticKey_IsIgnored()
    {
        var command = KgpCommand.Parse("a=T,e=future,i=1");

        Assert.AreEqual(KgpAction.TransmitAndDisplay, command.Action);
        Assert.AreEqual(1u, command.ImageId);
    }

    [TestMethod]
    [DataRow("a=T,unknown=99,i=1")]
    [DataRow("a=T,1=2")]
    [DataRow("a=T,i")]
    [DataRow("a=T,=2")]
    [DataRow("a=T,i=")]
    [DataRow("a=T,i=1=2")]
    [DataRow(",a=T")]
    [DataRow("a=T,")]
    [DataRow("a=T,,i=1")]
    [DataRow("a=T;i=1")]
    [DataRow("a=T,i=1;payload")]
    public void Parse_MalformedGrammar_RejectsCommand(string controlData)
    {
        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse(controlData));
    }

    [TestMethod]
    [DataRow("a=Z")]
    [DataRow("a=tt")]
    [DataRow("f=0")]
    [DataRow("f=99")]
    [DataRow("t=x")]
    [DataRow("t=dd")]
    [DataRow("o=x")]
    [DataRow("o=zz")]
    [DataRow("a=d,d=?")]
    [DataRow("a=d,d=aa")]
    public void Parse_InvalidEnumeratedControl_RejectsCommand(string controlData)
    {
        var failure = ParseFailure(controlData);

        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse(controlData));
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(failure.FormatReason(controlData.AsSpan())));
    }

    [TestMethod]
    [DataRow("a=t,i=1,I=2")]
    [DataRow("a=T,i=1,I=2")]
    [DataRow("a=q,i=1,I=2")]
    [DataRow("a=p,i=1,I=2")]
    [DataRow("a=d,d=i,i=1,I=2")]
    [DataRow("a=f,i=1,I=2")]
    [DataRow("a=a,i=1,I=2")]
    [DataRow("a=c,i=1,I=2")]
    public void Parse_NonZeroImageIdAndNumber_RejectsEveryAction(string controlData)
    {
        var failure = ParseFailure(controlData);

        Assert.AreEqual(
            KgpCommandParser.ErrorCode.ConflictingImageIdentity,
            failure.Code);
        Assert.AreEqual('I', failure.Key);
        Assert.AreEqual(1u, failure.ImageId);
        Assert.AreEqual(2u, failure.ImageNumber);
        Assert.AreEqual(
            "Must not specify both image id and image number",
            failure.FormatReason(controlData.AsSpan()));
        Assert.ThrowsExactly<FormatException>(() => KgpCommand.Parse(controlData));
    }

    [TestMethod]
    [DataRow("a=t,i=0,I=2")]
    [DataRow("a=t,i=1,I=0")]
    public void Parse_OnlyOneNonZeroImageIdentity_AcceptsCommand(string controlData)
    {
        ParseSuccess(controlData);
    }

    [TestMethod]
    public void TryParse_MixedErrors_UsesApprovedDiagnosticPrecedence()
    {
        const string grammarAndValidation = "f=99,broken";
        const string actionAndValidation = "f=99,a=Z";

        var grammarFailure = ParseFailure(grammarAndValidation);
        var actionFailure = ParseFailure(actionAndValidation);

        Assert.AreEqual(
            KgpCommandParser.ErrorCode.MalformedControlPair,
            grammarFailure.Code);
        Assert.AreEqual(
            "Malformed control pair 'broken'.",
            grammarFailure.FormatReason(grammarAndValidation.AsSpan()));
        Assert.AreEqual(
            KgpCommandParser.ErrorCode.InvalidAction,
            actionFailure.Code);
        Assert.AreEqual(
            "Invalid action value 'Z'.",
            actionFailure.FormatReason(actionAndValidation.AsSpan()));
    }

    [TestMethod]
    public void Parse_AllUnsignedControls_AcceptUInt32Boundaries()
    {
        foreach (var (prefix, key) in UnsignedControlContexts())
        {
            ParseSuccess($"{prefix},{key}=0");
            ParseSuccess($"{prefix},{key}=4294967295");
        }
    }

    [TestMethod]
    public void Parse_AllSignedControls_AcceptInt32Boundaries()
    {
        foreach (var (prefix, key) in SignedControlContexts())
        {
            ParseSuccess($"{prefix},{key}=-2147483648");
            ParseSuccess($"{prefix},{key}=2147483647");
        }
    }

    [TestMethod]
    public void Parse_AllUnsignedControls_RejectMalformedAndOverflowValues()
    {
        var invalidValues = new[]
        {
            "",
            "abc",
            "-1",
            "+1",
            " 1",
            "1 ",
            "0x1",
            "1.0",
            "4294967296",
        };

        foreach (var (prefix, key) in UnsignedControlContexts())
        {
            foreach (var value in invalidValues)
                ParseFailure($"{prefix},{key}={value}");
        }
    }

    [TestMethod]
    public void Parse_AllSignedControls_RejectMalformedAndOverflowValues()
    {
        var invalidValues = new[]
        {
            "",
            "abc",
            "+1",
            " 1",
            "1 ",
            "0x1",
            "1.0",
            "-2147483649",
            "2147483648",
        };

        foreach (var (prefix, key) in SignedControlContexts())
        {
            foreach (var value in invalidValues)
                ParseFailure($"{prefix},{key}={value}");
        }
    }

    [TestMethod]
    public void TryParse_FailurePreservesRecoverableContext()
    {
        var nonDelete = ParseFailure("a=t,i=7,I=8,p=9,q=2,f=99");
        var delete = ParseFailure("i=10,a=d,q=1,d=?,I=11,p=12");
        var invalidThenDelete = ParseFailure("a=x,a=d,d=?");
        var deleteThenInvalid = ParseFailure("a=d,a=x");

        Assert.AreEqual(KgpAction.Transmit, nonDelete.Action);
        Assert.AreEqual(
            KgpCommandParser.ErrorCode.InvalidImageFormat,
            nonDelete.Code);
        Assert.AreEqual(7u, nonDelete.ImageId);
        Assert.AreEqual(8u, nonDelete.ImageNumber);
        Assert.AreEqual(9u, nonDelete.PlacementId);
        Assert.AreEqual(KgpParsedCommand.QuietMode.SuppressAll, nonDelete.Quiet);

        Assert.AreEqual(KgpAction.Delete, delete.Action);
        Assert.AreEqual(10u, delete.ImageId);
        Assert.AreEqual(11u, delete.ImageNumber);
        Assert.AreEqual(12u, delete.PlacementId);
        Assert.AreEqual(KgpParsedCommand.QuietMode.SuppressSuccess, delete.Quiet);
        Assert.AreEqual(KgpAction.Delete, invalidThenDelete.Action);
        Assert.AreEqual(KgpAction.Delete, deleteThenInvalid.Action);
    }

    [TestMethod]
    [DoNotParallelize]
    public void TryParse_RepeatedCommands_StaysWithinAllocationBudget()
    {
        const int iterations = 10_000;
        const string validControlData =
            "a=T,f=24,t=d,s=100,v=200,S=300,O=400,i=42,p=7,m=0,q=1,N=1," +
            "x=10,y=20,w=30,h=40,X=3,Y=5,c=6,r=8,C=1,U=1,z=-9,P=11,Q=12,H=-13,V=14";
        const string invalidDeleteControlData = "a=d,d=f,i=42,r=bad,q=1";

        var validBytes = MeasureParserAllocations(
            validControlData,
            expectedSuccess: true,
            iterations);
        var invalidDeleteBytes = MeasureParserAllocations(
            invalidDeleteControlData,
            expectedSuccess: false,
            iterations);

        TestContext.WriteLine(
            $"Valid parse: {validBytes} bytes total, {(double)validBytes / iterations:F2} bytes/op.");
        TestContext.WriteLine(
            $"Invalid delete parse: {invalidDeleteBytes} bytes total, " +
            $"{(double)invalidDeleteBytes / iterations:F2} bytes/op.");

        Assert.IsLessThanOrEqualTo(256L, validBytes / iterations);
        Assert.AreEqual(0L, invalidDeleteBytes);
    }

    private static TCommand ParseTyped<TCommand>(string controlData)
        where TCommand : KgpParsedCommand
    {
        var success = KgpCommandParser.TryParse(
            controlData,
            out var command,
            out var failure);

        Assert.IsTrue(
            success,
            success ? null : failure.FormatReason(controlData.AsSpan()));
        Assert.IsNotNull(command);
        Assert.AreEqual(KgpCommandParser.ErrorCode.None, failure.Code);
        return TestSeq.IsType<TCommand>(command);
    }

    private static TSelector ParseDeleteSelector<TSelector>(string controlData)
        where TSelector : KgpParsedCommand.DeleteSelector
    {
        var command = ParseTyped<KgpParsedCommand.Delete>(controlData);
        return TestSeq.IsType<TSelector>(command.Selector);
    }

    private static KgpCommandParser.Failure ParseFailure(string controlData)
    {
        var success = KgpCommandParser.TryParse(
            controlData,
            out var command,
            out var failure);

        Assert.IsFalse(success, $"Expected parsing to fail: {controlData}");
        Assert.IsNull(command);
        Assert.AreNotEqual(KgpCommandParser.ErrorCode.None, failure.Code);
        return failure;
    }

    private static void ParseSuccess(string controlData)
    {
        var success = KgpCommandParser.TryParse(
            controlData,
            out var command,
            out var failure);

        Assert.IsTrue(
            success,
            success ? null : $"{controlData}: {failure.FormatReason(controlData.AsSpan())}");
        Assert.IsNotNull(command);
    }

    private static long MeasureParserAllocations(
        string controlData,
        bool expectedSuccess,
        int iterations)
    {
        for (var i = 0; i < 1_000; i++)
        {
            var success = KgpCommandParser.TryParse(
                controlData,
                out _,
                out _);
            Assert.AreEqual(expectedSuccess, success);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var successfulParses = 0;
        KgpParsedCommand? lastCommand = null;
        var lastFailure = default(KgpCommandParser.Failure);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            if (KgpCommandParser.TryParse(
                controlData,
                out lastCommand,
                out lastFailure))
            {
                successfulParses++;
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(expectedSuccess ? iterations : 0, successfulParses);
        GC.KeepAlive(lastCommand);
        GC.KeepAlive(lastFailure);
        return allocated;
    }

    private static (string Prefix, string Key)[] UnsignedControlContexts()
        =>
        [
            ("a=t", "q"),
            ("a=t", "s"),
            ("a=t", "v"),
            ("a=t", "S"),
            ("a=t", "O"),
            ("a=t", "i"),
            ("a=t", "I"),
            ("a=t", "p"),
            ("a=t", "m"),
            ("a=t", "N"),
            ("a=p", "i"),
            ("a=p", "I"),
            ("a=p", "p"),
            ("a=p", "x"),
            ("a=p", "y"),
            ("a=p", "w"),
            ("a=p", "h"),
            ("a=p", "X"),
            ("a=p", "Y"),
            ("a=p", "c"),
            ("a=p", "r"),
            ("a=p", "C"),
            ("a=p", "U"),
            ("a=p", "P"),
            ("a=p", "Q"),
            ("a=d,d=f", "i"),
            ("a=d,d=f", "I"),
            ("a=d,d=i", "p"),
            ("a=d,d=p", "x"),
            ("a=d,d=p", "y"),
            ("a=d,d=f", "r"),
            ("a=f", "s"),
            ("a=f", "v"),
            ("a=f", "x"),
            ("a=f", "y"),
            ("a=f", "c"),
            ("a=f", "r"),
            ("a=f", "X"),
            ("a=f", "Y"),
            ("a=a", "i"),
            ("a=a", "I"),
            ("a=a", "p"),
            ("a=a", "s"),
            ("a=a", "v"),
            ("a=a", "c"),
            ("a=a", "r"),
            ("a=c", "i"),
            ("a=c", "I"),
            ("a=c", "p"),
            ("a=c", "c"),
            ("a=c", "r"),
            ("a=c", "x"),
            ("a=c", "y"),
            ("a=c", "w"),
            ("a=c", "h"),
            ("a=c", "X"),
            ("a=c", "Y"),
            ("a=c", "C"),
        ];

    private static (string Prefix, string Key)[] SignedControlContexts()
        =>
        [
            ("a=p", "z"),
            ("a=p", "H"),
            ("a=p", "V"),
            ("a=d,d=z", "z"),
            ("a=f", "z"),
            ("a=a", "z"),
        ];
}
