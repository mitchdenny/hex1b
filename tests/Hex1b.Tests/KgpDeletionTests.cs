using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Deletion conformance derived from the Kitty and Ghostty revisions pinned by
/// issue #406.
/// </summary>
[TestClass]
public class KgpDeletionTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };

    public static IEnumerable<object[]> DeleteCommands
    {
        get
        {
            yield return ["a=d"];
            yield return ["a=d,d=a"];
            yield return ["a=d,d=A"];
            yield return ["a=d,d=i,i=99"];
            yield return ["a=d,d=I,i=99"];
            yield return ["a=d,d=n,I=99"];
            yield return ["a=d,d=N,I=99"];
            yield return ["a=d,d=c"];
            yield return ["a=d,d=C"];
            yield return ["a=d,d=f,i=99"];
            yield return ["a=d,d=F,i=99"];
            yield return ["a=d,d=p,x=1,y=1"];
            yield return ["a=d,d=P,x=1,y=1"];
            yield return ["a=d,d=q,x=1,y=1"];
            yield return ["a=d,d=Q,x=1,y=1"];
            yield return ["a=d,d=r,x=0,y=99"];
            yield return ["a=d,d=R,x=0,y=99"];
            yield return ["a=d,d=x,x=1"];
            yield return ["a=d,d=X,x=1"];
            yield return ["a=d,d=y,y=1"];
            yield return ["a=d,d=Y,y=1"];
            yield return ["a=d,d=z"];
            yield return ["a=d,d=Z"];
        }
    }

    public static IEnumerable<object[]> EmptyOrUnmatchedDeleteCommands
    {
        get
        {
            yield return ["a=d,d=i"];
            yield return ["a=d,d=I"];
            yield return ["a=d,d=i,i=777,p=9"];
            yield return ["a=d,d=I,i=777,p=9"];
            yield return ["a=d,d=n"];
            yield return ["a=d,d=N"];
            yield return ["a=d,d=n,I=777,p=9"];
            yield return ["a=d,d=N,I=777,p=9"];
            yield return ["a=d,d=p,x=1"];
            yield return ["a=d,d=P,y=1"];
            yield return ["a=d,d=q,x=1"];
            yield return ["a=d,d=Q,y=1"];
            yield return ["a=d,d=r,y=0"];
            yield return ["a=d,d=R,x=2,y=1"];
            yield return ["a=d,d=x"];
            yield return ["a=d,d=X,x=0"];
            yield return ["a=d,d=y"];
            yield return ["a=d,d=Y,y=0"];
            yield return ["a=d,d=f"];
            yield return ["a=d,d=F"];
            yield return ["a=d,d=z"];
            yield return ["a=d,d=Z"];
        }
    }

    [TestMethod]
    [DataRow('a', 'A', "all")]
    [DataRow('i', 'I', "id")]
    [DataRow('n', 'N', "number")]
    [DataRow('c', 'C', "cursor")]
    [DataRow('f', 'F', "frame")]
    [DataRow('p', 'P', "cell")]
    [DataRow('q', 'Q', "cell-z")]
    [DataRow('r', 'R', "range")]
    [DataRow('x', 'X', "column")]
    [DataRow('y', 'Y', "row")]
    [DataRow('z', 'Z', "z")]
    public void DeleteSelector_ActiveMatch_UsesCaseSpecificDataLifetime(
        char lowercase,
        char uppercase,
        string scenario)
    {
        AssertSelectorCase(lowercase, scenario, freeData: false);
        AssertSelectorCase(uppercase, scenario, freeData: true);
    }

    [TestMethod]
    public void DeleteAll_OmittedSelectorMatchesLowercaseA()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        PutAt(terminal, 1, placementId: 1, row: 0, column: 0);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d"));

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DataRow('A', "all")]
    [DataRow('C', "cursor")]
    [DataRow('F', "frame")]
    [DataRow('P', "cell")]
    [DataRow('Q', "cell-z")]
    [DataRow('R', "range")]
    [DataRow('X', "column")]
    [DataRow('Y', "row")]
    [DataRow('Z', "z")]
    public void NonIdentitySelector_PlacementControlDoesNotNarrowTarget(
        char selector,
        string scenario)
        => AssertSelectorCase(
            selector,
            scenario,
            freeData: true,
            extraControls: ",p=999");

    [TestMethod]
    [DataRow("a=d,d=A")]
    [DataRow("a=d,d=C")]
    [DataRow("a=d,d=P,x=1,y=3")]
    [DataRow("a=d,d=Q,x=1,y=3")]
    [DataRow("a=d,d=X,x=1")]
    [DataRow("a=d,d=Y,y=3")]
    [DataRow("a=d,d=Z")]
    public void UppercaseGeometrySelectors_SurvivingOwnersRetainImageData(
        string controlData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 4);
        CreateSharedOwnerScenario(terminal, numbered: false);

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 4);
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 5));
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 2));
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 4));
        Assert.AreEqual(2, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(8L, terminal.KgpImageStore.TotalSize);
        AssertIntegrity(terminal, scrollbackLines: 4);
    }

    [TestMethod]
    [DataRow('I', false)]
    [DataRow('N', true)]
    public void UppercaseIdentitySelector_ExactPlacementRetainsOtherOwners(
        char selector,
        bool numbered)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 4);
        var imageId = CreateSharedOwnerScenario(terminal, numbered);
        var identity = numbered ? "I=9" : $"i={imageId}";

        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=d,d={selector},{identity},p=5"));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 4);
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 5));
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == imageId &&
                placement.PlacementId != 5));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(imageId));
        AssertIntegrity(terminal, scrollbackLines: 4);
    }

    [TestMethod]
    [DataRow('I', false)]
    [DataRow('N', true)]
    public void UppercaseIdentitySelector_UnmatchedPlacementDoesNotFreeData(
        char selector,
        bool numbered)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var imageId = numbered
            ? TransmitNumbered(terminal, 9)
            : Transmit(terminal, 1);
        PutAt(terminal, imageId, placementId: 1, row: 0, column: 0);
        var before = terminal.CreateSnapshot();
        var identity = numbered ? "I=9" : $"i={imageId}";

        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=d,d={selector},{identity},p=999"));

        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(imageId));
        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1u, placement.PlacementId);
        Assert.AreSame(
            before.KgpImages[imageId],
            terminal.KgpImageStore.GetImageById(imageId));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("number")]
    [DataRow("range")]
    public void IdentitySelector_UnplacedDataIsFreedOnlyByUppercase(
        string scenario)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        uint imageId;
        string lowercase;
        string uppercase;
        if (scenario == "number")
        {
            imageId = TransmitNumbered(terminal, 9);
            lowercase = "a=d,d=n,I=9";
            uppercase = "a=d,d=N,I=9";
        }
        else
        {
            imageId = Transmit(terminal, 10);
            lowercase = scenario == "id"
                ? "a=d,d=i,i=10"
                : "a=d,d=r,x=10,y=10";
            uppercase = scenario == "id"
                ? "a=d,d=I,i=10"
                : "a=d,d=R,x=10,y=10";
        }

        Apply(terminal, KgpTestHelper.BuildCommand(lowercase));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(imageId));

        Apply(terminal, KgpTestHelper.BuildCommand(uppercase));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(imageId));
        Assert.AreEqual(0L, terminal.KgpImageStore.TotalSize);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void NumberSelector_ResolvesNewestOnceThenFallsBackToOlderImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var olderId = TransmitNumbered(terminal, 77);
        var newerId = TransmitNumbered(terminal, 77);
        Assert.AreNotEqual(olderId, newerId);
        PutAt(terminal, olderId, placementId: 1, row: 0, column: 0);
        PutAt(terminal, newerId, placementId: 2, row: 1, column: 0);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=n,I=77"));

        Assert.IsTrue(terminal.KgpPlacements.Any(
            placement => placement.ImageId == olderId));
        Assert.IsFalse(terminal.KgpPlacements.Any(
            placement => placement.ImageId == newerId));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(newerId));
        Assert.AreEqual(
            newerId,
            terminal.KgpImageStore.GetImageByNumber(77)!.ImageId);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=N,I=77"));

        Assert.IsNull(terminal.KgpImageStore.GetImageById(newerId));
        Assert.AreEqual(
            olderId,
            terminal.KgpImageStore.GetImageByNumber(77)!.ImageId);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DataRow('a', false)]
    [DataRow('A', true)]
    public void DeleteAll_HistorySpanningRootIsVisibleButHistoryOnlyRootIsNot(
        char selector,
        bool freeData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 5,
            height: 2,
            scrollbackCapacity: 4);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        PutAt(
            terminal,
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            rows: 2);
        PutAt(
            terminal,
            imageId: 2,
            placementId: 2,
            row: 0,
            column: 3);
        Apply(terminal, "\x1b[S");

        Apply(
            terminal,
            KgpTestHelper.BuildCommand($"a=d,d={selector}"));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 4);
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == 1));
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == 2));
        Assert.AreEqual(freeData, terminal.KgpImageStore.GetImageById(1) is null);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        AssertIntegrity(terminal, scrollbackLines: 4);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("number")]
    [DataRow("range")]
    public void IdentitySelector_RemovesActiveHistoryVirtualAndRelativeOwners(
        string scenario)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 4);
        var imageId = CreateSharedOwnerScenario(
            terminal,
            numbered: scenario == "number");
        var controlData = scenario switch
        {
            "id" => $"a=d,d=i,i={imageId}",
            "number" => "a=d,d=n,I=9",
            _ => $"a=d,d=r,x={imageId},y={imageId}",
        };

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 4);
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == imageId));
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(imageId));
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == 100));
        AssertIntegrity(terminal, scrollbackLines: 4);
    }

    [TestMethod]
    [DataRow("a=d,d=c")]
    [DataRow("a=d,d=p,x=4,y=3")]
    [DataRow("a=d,d=q,x=4,y=3,z=5")]
    [DataRow("a=d,d=x,x=4")]
    [DataRow("a=d,d=y,y=3")]
    public void GeometrySelector_UsesEffectiveRelativeRectangle(
        string controlData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 5);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        PutAt(terminal, 1, placementId: 1, row: 1, column: 1);
        Put(
            terminal,
            "i=2,p=2,c=1,r=1,z=5,P=1,Q=1,H=2,V=1,C=1");
        MoveCursor(terminal, row: 2, column: 3);

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == 1));
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == 2));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DataRow('z', false)]
    [DataRow('Z', true)]
    public void ZIndexSelector_SelectsHistoryAndUnresolvedRelativeNodes(
        char selector,
        bool freeData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 3,
            scrollbackCapacity: 4);
        Transmit(terminal, 1);
        PutAt(
            terminal,
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            zIndex: 5);
        Apply(terminal, "\x1b[S");
        Transmit(terminal, 2);
        AddVirtual(terminal, imageId: 2, placementId: 2);
        Transmit(terminal, 3);
        Put(
            terminal,
            "i=3,p=3,c=1,r=1,z=5,P=2,Q=2,H=1,V=1,C=1");
        Transmit(terminal, 4);
        AddVirtual(terminal, imageId: 4, placementId: 4);

        Apply(
            terminal,
            KgpTestHelper.BuildCommand($"a=d,d={selector},z=5"));

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(2, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(freeData, terminal.KgpImageStore.GetImageById(1) is null);
        Assert.AreEqual(freeData, terminal.KgpImageStore.GetImageById(3) is null);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(4));
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 4);
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.ImageId is 1 or 3));
        AssertIntegrity(terminal, scrollbackLines: 4);
    }

    [TestMethod]
    [DataRow("a=d,d=z", false)]
    [DataRow("a=d,d=z,z=0", false)]
    [DataRow("a=d,d=Z", true)]
    [DataRow("a=d,d=Z,z=0", true)]
    [DataRow("a=d,d=q,x=1,y=1", false)]
    [DataRow("a=d,d=q,x=1,y=1,z=0", false)]
    [DataRow("a=d,d=Q,x=1,y=1", true)]
    [DataRow("a=d,d=Q,x=1,y=1,z=0", true)]
    public void ZIndexDefault_OmittedAndExplicitZeroAreEquivalent(
        string controlData,
        bool freeData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        PutAt(
            terminal,
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            zIndex: 0);
        PutAt(
            terminal,
            imageId: 2,
            placementId: 2,
            row: 0,
            column: 0,
            zIndex: 1);

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        Assert.IsFalse(terminal.KgpPlacements.Any(
            placement => placement.ImageId == 1));
        Assert.IsTrue(terminal.KgpPlacements.Any(
            placement => placement.ImageId == 2));
        Assert.AreEqual(freeData, terminal.KgpImageStore.GetImageById(1) is null);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void RangeSelector_DefaultLowerBoundSparseIdsAndUIntBoundaries()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Transmit(terminal, 1);
        Transmit(terminal, 1000);
        Transmit(terminal, uint.MaxValue);
        PutAt(terminal, 1, placementId: 1, row: 0, column: 0);
        PutAt(terminal, 1000, placementId: 2, row: 1, column: 0);
        PutAt(
            terminal,
            uint.MaxValue,
            placementId: 3,
            row: 2,
            column: 0);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=r,y=1"));
        Assert.IsFalse(terminal.KgpPlacements.Any(
            placement => placement.ImageId == 1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(2, terminal.KgpPlacements.Count);

        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=d,d=R,x=4294967295,y=4294967295"));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(uint.MaxValue));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1000));

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=R,x=0,y=999"));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1000));

        var before = terminal.CreateSnapshot();
        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=R,y=0"));
        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=R,x=1001,y=1000"));
        Assert.AreEqual(
            before.KgpPlacements.Count,
            terminal.KgpPlacements.Count);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1000));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void RangeSelector_DataLessVirtualParentStillCascadesDescendants()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 42);
        AddVirtual(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(
            terminal,
            "i=2,p=2,c=1,r=1,P=42,Q=7,H=1,V=1,C=1");
        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(42));

        Apply(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=r,x=42,y=42"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsEmpty(terminal.KgpPlacements);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void AnonymousImage_IdentitySelectorsCannotAddressItAndLowercaseCleanupFreesIt()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=1,v=1,p=7,c=1,r=1,C=1,q=2",
                KgpTestHelper.CreatePixelData(1, 1)));
        var placement = TestSeq.Single(terminal.KgpPlacements);
        var privateImageId = placement.ImageId;

        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=d,d=I,i={privateImageId}"));
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=d,d=R,x={privateImageId},y={privateImageId}"));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(privateImageId));
        TestSeq.Single(terminal.KgpPlacements);

        MoveCursor(terminal, row: 0, column: 0);
        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=c"));

        Assert.IsNull(terminal.KgpImageStore.GetImageById(privateImageId));
        Assert.IsEmpty(terminal.KgpPlacements);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DataRow("a=d,d=I,i=1")]
    [DataRow("a=d,d=R,x=1,y=1")]
    public void IdentitySelector_VirtualClientCollisionDoesNotBindOrDeletePrivateData(
        string controlData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        AddVirtual(terminal, imageId: 1, placementId: 7);
        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(1));
        MoveCursor(terminal, row: 2, column: 2);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=1,v=1,c=1,r=1,C=1,q=2",
                KgpTestHelper.CreatePixelData(1, 1)));
        var anonymous = TestSeq.Single(
            terminal.KgpPlacements.Where(
                placement => placement.PlacementId == 0));
        Assert.AreEqual(1u, anonymous.ImageId);
        Assert.IsNull(terminal.KgpImageStore.GetImageByClientId(1));
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        using (var beforeDelete = terminal.CreateSnapshot())
        {
            Assert.IsFalse(beforeDelete.KgpPlacements.Any(
                placement => placement.PlacementId == 7));
        }

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        var survivor = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(0u, survivor.PlacementId);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void IdentitySelector_PrivateCollisionRemovesVirtualButNotPrivateData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        AddVirtual(terminal, imageId: 1, placementId: 7);
        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(1));
        var privateImage = terminal.KgpImageStore.StoreImage(
            new KgpParsedCommand.TransmissionData(
                KgpFormat.Rgba32,
                KgpTransmissionMedium.Direct,
                Width: 1,
                Height: 1,
                FileSize: 0,
                FileOffset: 0,
                ImageId: 0,
                ImageNumber: 0,
                PlacementId: 0,
                KgpParsedCommand.CompressionMode.None,
                MoreData: false,
                UsageHints: 0),
            [0x22, 0, 0, 0xFF]).Image;
        Assert.AreEqual(1u, privateImage.ImageId);

        Apply(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=I,i=1"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.AreSame(
            privateImage,
            terminal.KgpImageStore.GetImageById(privateImage.ImageId));
        Assert.IsNull(
            terminal.KgpImageStore.GetImageByClientId(privateImage.ImageId));
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void GeometrySelector_VirtualClientCollisionDoesNotRetainPrivateData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        AddVirtual(terminal, imageId: 1, placementId: 7);
        RealizeVirtual(
            terminal,
            imageId: 1,
            placementId: 7,
            row: 0,
            column: 0);
        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(1));
        MoveCursor(terminal, row: 2, column: 2);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=1,v=1,c=1,r=1,C=1,q=2",
                KgpTestHelper.CreatePixelData(1, 1)));
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        using (var collision = terminal.CreateSnapshot())
        {
            var privatePlacement = TestSeq.Single(collision.KgpPlacements);
            Assert.AreEqual(0u, privatePlacement.PlacementId);
            Assert.AreEqual(2, privatePlacement.Row);
            Assert.AreEqual(2, privatePlacement.Column);
        }

        MoveCursor(terminal, row: 2, column: 2);
        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=c"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=i,i=1"));
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void ExplicitRetransmission_RelocatesPrivateDataButKeepsVirtualClientIdentity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        AddVirtual(terminal, imageId: 1, placementId: 7);
        RealizeVirtual(
            terminal,
            imageId: 1,
            placementId: 7,
            row: 0,
            column: 0);
        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(1));
        var privateImage = terminal.KgpImageStore.StoreImage(
            new KgpParsedCommand.TransmissionData(
                KgpFormat.Rgba32,
                KgpTransmissionMedium.Direct,
                Width: 1,
                Height: 1,
                FileSize: 0,
                FileOffset: 0,
                ImageId: 0,
                ImageNumber: 0,
                PlacementId: 0,
                KgpParsedCommand.CompressionMode.None,
                MoreData: false,
                UsageHints: 0),
            [0x22, 0, 0, 0xFF]).Image;
        Assert.AreEqual(1u, privateImage.ImageId);

        Apply(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                1,
                width: 1,
                height: 1,
                quiet: 2,
                fillByte: 0x44));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(
            0x44,
            terminal.KgpImageStore.GetImageByClientId(1)!.Data[0]);
        Assert.IsNull(terminal.KgpImageStore.GetImageByClientId(2));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        using var snapshot = terminal.CreateSnapshot();
        var virtualPlacement = TestSeq.Single(snapshot.KgpPlacements);
        Assert.AreEqual(7u, virtualPlacement.PlacementId);
        Assert.AreEqual(0x44, snapshot.KgpImages[1].Data[0]);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void LowercaseParentDelete_ReclaimsOnlyUnownedDescendantImages()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Transmit(terminal, 3);
        PutAt(terminal, 1, placementId: 1, row: 0, column: 0);
        Put(
            terminal,
            "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,C=1");
        Put(
            terminal,
            "i=3,p=3,c=1,r=1,P=2,Q=2,H=1,C=1");
        PutAt(terminal, 2, placementId: 9, row: 3, column: 3);

        Apply(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=i,i=1,p=1"));

        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(3));
        var survivor = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(9u, survivor.PlacementId);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DynamicData(nameof(DeleteCommands))]
    public void DeleteSelector_AlternateScreenDoesNotMutateMainScreen(
        string controlData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        PutAt(terminal, 1, placementId: 1, row: 0, column: 0);

        Apply(terminal, "\x1b[?1049h");
        Apply(terminal, KgpTestHelper.BuildCommand(controlData));
        Apply(terminal, "\x1b[?1049l");

        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1u, placement.ImageId);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DynamicData(nameof(DeleteCommands))]
    public void DeleteSelector_RecognizedCommandAbortsPendingUploadWithoutResponse(
        string controlData)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=32,s=1,v=2,i=99,m=1,q=2",
                [1, 2, 3]));
        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        workload.AssertNoResponse();
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DynamicData(nameof(DeleteCommands))]
    public void DeleteSelector_RecognizedCommandAbortsPendingFrameUpload(
        string controlData)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.KgpImageStore.StoreImage(CreateImage(200, 0x11));
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=f,f=32,s=1,v=1,i=200,m=1,q=2",
                [1, 2, 3]));
        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(1, terminal.KgpImageStore.GetImageById(200)!.FrameCount);
        workload.AssertNoResponse();
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DynamicData(nameof(EmptyOrUnmatchedDeleteCommands))]
    public void DeleteSelector_EmptyOrUnmatchedTargetIsNoOpAfterUploadAbort(
        string controlData)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        PutAt(
            terminal,
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            zIndex: 9);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=32,s=1,v=2,i=99,m=1,q=2",
                [1, 2, 3]));

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1u, placement.PlacementId);
        workload.AssertNoResponse();
        AssertIntegrity(terminal);
    }

    [TestMethod]
    [DataRow("a=d,d=?", false)]
    [DataRow("a=d,d=f,r=bad", false)]
    [DataRow("d=p,x=1,y=4294967296,a=d", false)]
    [DataRow("a=x,a=d,d=?", false)]
    [DataRow("a=d,a=x", false)]
    [DataRow("a=d,d=I,i=1,I=2", true)]
    public void MalformedDelete_AbortsPendingUploadAndUsesDeleteResponseRule(
        string controlData,
        bool expectsConflictingIdentityResponse)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=32,s=1,v=2,i=99,m=1,q=2",
                [1, 2, 3]));

        Apply(terminal, $"\x1b_G{controlData}\x1b\\");

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        if (expectsConflictingIdentityResponse)
        {
            Assert.AreEqual(
                "\x1b_Gi=1,I=2;EINVAL:Must not specify both image id and image number\x1b\\",
                workload.ReadResponse());
        }
        else
        {
            workload.AssertNoResponse();
        }
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void MalformedDelete_AbortsPendingFrameUploadWithoutResponse()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.KgpImageStore.StoreImage(CreateImage(200, 0x11));
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=f,f=32,s=1,v=1,i=200,m=1,q=2",
                [1, 2, 3]));

        Apply(terminal, "\x1b_Ga=d,d=?\x1b\\");

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(1, terminal.KgpImageStore.GetImageById(200)!.FrameCount);
        workload.AssertNoResponse();
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void Delete_ExistingSnapshotRemainsImmutable()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1, fillByte: 0x5A);
        PutAt(terminal, 1, placementId: 7, row: 1, column: 2);
        using var before = terminal.CreateSnapshot();
        var placement = TestSeq.Single(before.KgpPlacements);
        var image = before.KgpImages[1];

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=I,i=1"));

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(1u, placement.ImageId);
        Assert.AreEqual(7u, placement.PlacementId);
        Assert.AreEqual(1, placement.Row);
        Assert.AreEqual(2, placement.Column);
        Assert.AreEqual(0x5A, image.Data[0]);
        Assert.AreSame(image, before.KgpImages[1]);
        AssertIntegrity(terminal);
    }

    [TestMethod]
    public void DeleteIfUnreferenced_OnlyRemovesFiniteUnownedCandidates()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, 0x11));
        store.StoreImage(CreateImage(2, 0x22));
        store.StoreImage(CreateImage(3, 0x33));
        var deletionIndex = store.CaptureDeletionIndex();

        store.DeleteIfUnreferenced(
            new HashSet<uint> { 1, 2 },
            new HashSet<uint> { 2 },
            deletionIndex.Images);

        Assert.IsNull(store.GetImageById(1));
        Assert.IsNotNull(store.GetImageById(2));
        Assert.IsNotNull(store.GetImageById(3));
        Assert.AreEqual(2, store.ImageCount);
        Assert.AreEqual(8L, store.TotalSize);
    }

    [TestMethod]
    public void DeleteIfUnreferenced_SnapshotDoesNotDeleteReplacementGeneration()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, 0x11));
        var deletionIndex = store.CaptureDeletionIndex();
        var replacement = CreateImage(1, 0x22);
        store.StoreImage(replacement);

        store.DeleteIfUnreferenced(
            new HashSet<uint> { 1 },
            new HashSet<uint>(),
            deletionIndex.Images);

        Assert.AreSame(replacement, store.GetImageById(1));
        Assert.AreEqual(4L, store.TotalSize);
    }

    [TestMethod]
    public async Task ExecuteDeletion_ConcurrentStoreWaitsForTransaction()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, 0x11));
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var deletion = Task.Factory.StartNew(
            () => store.ExecuteDeletion(() =>
            {
                var index = store.CaptureDeletionIndex();
                Assert.IsTrue(index.Images.ContainsKey(1));
                entered.SetResult();
                release.Task.GetAwaiter().GetResult();
                Assert.AreEqual(0x11, store.GetImageById(1)!.Data[0]);
            }),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        var replacement = CreateImage(1, 0x22);
        var storeReplacement = Task.Factory.StartNew(
            () =>
            {
                replacementStarted.SetResult();
                store.StoreImage(replacement);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await replacementStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        Assert.IsFalse(storeReplacement.IsCompleted);

        release.SetResult();
        await Task.WhenAll(deletion, storeReplacement).WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        Assert.AreSame(replacement, store.GetImageById(1));
    }

    private static void AssertSelectorCase(
        char selector,
        string scenario,
        bool freeData,
        string extraControls = "")
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 5);
        uint targetImageId;
        uint survivorImageId;
        string controlData;

        switch (scenario)
        {
            case "number":
                targetImageId = TransmitNumbered(terminal, 9);
                survivorImageId = Transmit(terminal, 100);
                PutAt(terminal, targetImageId, 11, row: 0, column: 0);
                PutAt(terminal, survivorImageId, 22, row: 3, column: 3);
                controlData = $"a=d,d={selector},I=9";
                break;
            case "range":
                targetImageId = Transmit(terminal, 1);
                survivorImageId = Transmit(terminal, 3);
                PutAt(terminal, targetImageId, 11, row: 0, column: 0);
                PutAt(terminal, survivorImageId, 22, row: 3, column: 3);
                controlData = $"a=d,d={selector},x=0,y=1";
                break;
            default:
                targetImageId = Transmit(terminal, 1);
                survivorImageId = Transmit(terminal, 2);
                PutAt(
                    terminal,
                    targetImageId,
                    placementId: 11,
                    row: 0,
                    column: 0,
                    zIndex: 0);
                if (scenario is not ("all" or "frame"))
                {
                    PutAt(
                        terminal,
                        survivorImageId,
                        placementId: 22,
                        row: scenario is "cell-z" or "z" ? 0 : 3,
                        column: scenario is "cell-z" or "z" ? 0 : 3,
                        zIndex: 1);
                }

                controlData = scenario switch
                {
                    "all" => $"a=d,d={selector}",
                    "id" => $"a=d,d={selector},i=1",
                    "cursor" => $"a=d,d={selector}",
                    "frame" => $"a=d,d={selector},i=1",
                    "cell" => $"a=d,d={selector},x=1,y=1",
                    "cell-z" => $"a=d,d={selector},x=1,y=1",
                    "column" => $"a=d,d={selector},x=1",
                    "row" => $"a=d,d={selector},y=1",
                    "z" => $"a=d,d={selector}",
                    _ => throw new InvalidOperationException(
                        $"Unknown selector scenario {scenario}."),
                };
                break;
        }

        controlData += extraControls;
        MoveCursor(terminal, row: 0, column: 0);
        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        var targetPlacementSurvives = scenario == "frame" && !freeData;
        Assert.AreEqual(
            targetPlacementSurvives,
            terminal.KgpPlacements.Any(
                placement => placement.ImageId == targetImageId));
        Assert.AreEqual(
            freeData,
            terminal.KgpImageStore.GetImageById(targetImageId) is null);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(survivorImageId));
        Assert.AreEqual(freeData ? 4L : 8L, terminal.KgpImageStore.TotalSize);
        AssertIntegrity(terminal);
    }

    private static uint CreateSharedOwnerScenario(
        Hex1bTerminal terminal,
        bool numbered)
    {
        var imageId = numbered
            ? TransmitNumbered(terminal, 9)
            : Transmit(terminal, 1);
        Transmit(terminal, 100);
        PutAt(
            terminal,
            imageId,
            placementId: 3,
            row: 0,
            column: 2,
            zIndex: 9);
        PutAt(
            terminal,
            imageId: 100,
            placementId: 1,
            row: 0,
            column: 6,
            zIndex: 9);
        Put(
            terminal,
            $"i={imageId},p=2,c=1,r=1,z=9,P=100,Q=1,H=-1,V=1,C=1");
        Apply(terminal, "\x1b[S");
        AddVirtual(terminal, imageId, placementId: 4);
        RealizeVirtual(
            terminal,
            imageId,
            placementId: 4,
            row: 1,
            column: 3);
        PutAt(
            terminal,
            imageId,
            placementId: 5,
            row: 2,
            column: 0,
            zIndex: 0);
        MoveCursor(terminal, row: 2, column: 0);
        return imageId;
    }

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        int width = 8,
        int height = 5,
        int? scrollbackCapacity = null)
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height);
        if (scrollbackCapacity.HasValue)
            builder.WithScrollback(scrollbackCapacity.Value);
        return builder.Build();
    }

    private static uint Transmit(
        Hex1bTerminal terminal,
        uint imageId,
        byte fillByte = 0x11)
    {
        Apply(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                imageId,
                width: 1,
                height: 1,
                quiet: 2,
                fillByte: fillByte));
        return imageId;
    }

    private static uint TransmitNumbered(
        Hex1bTerminal terminal,
        uint imageNumber)
    {
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=t,f=32,s=1,v=1,I={imageNumber},q=2",
                KgpTestHelper.CreatePixelData(1, 1)));
        return terminal.KgpImageStore.GetImageByNumber(imageNumber)!.ImageId;
    }

    private static void PutAt(
        Hex1bTerminal terminal,
        uint imageId,
        uint placementId,
        int row,
        int column,
        uint columns = 1,
        uint rows = 1,
        int zIndex = 0)
    {
        MoveCursor(terminal, row, column);
        Put(
            terminal,
            $"i={imageId},p={placementId},c={columns},r={rows}," +
            $"z={zIndex},C=1");
    }

    private static void Put(Hex1bTerminal terminal, string controls)
        => Apply(
            terminal,
            KgpTestHelper.BuildCommand($"a=p,{controls},q=2"));

    private static void AddVirtual(
        Hex1bTerminal terminal,
        uint imageId,
        uint placementId)
        => Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=p,U=1,i={imageId},p={placementId},c=1,r=1,q=2"));

    private static void RealizeVirtual(
        Hex1bTerminal terminal,
        uint imageId,
        uint placementId,
        int row,
        int column)
    {
        MoveCursor(terminal, row, column);
        Apply(
            terminal,
            Foreground(imageId) +
            UnderlineColor(placementId) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");
    }

    private static void MoveCursor(
        Hex1bTerminal terminal,
        int row,
        int column)
        => Apply(terminal, $"\x1b[{row + 1};{column + 1}H");

    private static void Apply(Hex1bTerminal terminal, string value)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(value));

    private static void AssertIntegrity(
        Hex1bTerminal terminal,
        int scrollbackLines = 32)
    {
        terminal.ValidateKgpDeletionInvariants();
        using var snapshot = terminal.CreateSnapshot(
            scrollbackLines: scrollbackLines);
        foreach (var placement in snapshot.KgpPlacements)
        {
            Assert.IsTrue(
                snapshot.KgpImages.ContainsKey(placement.ImageId),
                $"Placement {placement.PlacementId} references missing image {placement.ImageId}.");
        }
    }

    private static KgpImageData CreateImage(uint imageId, byte value)
        => new(
            imageId,
            imageNumber: 0,
            data: [value, 0, 0, 255],
            width: 1,
            height: 1,
            KgpFormat.Rgba32);

    private static string Foreground(uint imageId)
        => $"\x1b[38;2;{(imageId >> 16) & 0xFF};" +
            $"{(imageId >> 8) & 0xFF};{imageId & 0xFF}m";

    private static string UnderlineColor(uint placementId)
        => $"\x1b[58;2;{(placementId >> 16) & 0xFF};" +
            $"{(placementId >> 8) & 0xFF};{placementId & 0xFF}m";

    private static string Placeholder(int row, int column)
        => new Rune(KgpUnicodePlaceholder.CodePoint).ToString() +
            new Rune(KgpUnicodePlaceholderDiacritics.CodePoints[row]) +
            new Rune(KgpUnicodePlaceholderDiacritics.CodePoints[column]);

    private sealed class RecordingWorkloadAdapter :
        IHex1bTerminalWorkloadAdapter
    {
        private readonly Queue<byte[]> _responses = new();
        private readonly object _lock = new();

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(
            CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                _responses.Enqueue(data.ToArray());
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(
            int width,
            int height,
            CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public string ReadResponse()
        {
            byte[]? response = null;
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                    {
                        return _responses.TryDequeue(out response);
                    }
                },
                TimeSpan.FromSeconds(1));
            Assert.IsTrue(received, "Expected a KGP protocol response.");
            return Encoding.UTF8.GetString(response!);
        }

        public void AssertNoResponse()
        {
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                    {
                        return _responses.Count > 0;
                    }
                },
                TimeSpan.FromMilliseconds(100));
            Assert.IsFalse(received, "Expected no KGP protocol response.");
        }
    }
}
