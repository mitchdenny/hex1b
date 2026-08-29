namespace Hex1b.Tests;

public partial class KgpUnicodePlaceholderTests
{
    [TestMethod]
    [DataRow("a=d,d=a")]
    [DataRow("a=d,d=A")]
    [DataRow("a=d,d=c")]
    [DataRow("a=d,d=C")]
    [DataRow("a=d,d=p,x=1,y=1")]
    [DataRow("a=d,d=P,x=1,y=1")]
    [DataRow("a=d,d=q,x=1,y=1,z=-1")]
    [DataRow("a=d,d=Q,x=1,y=1,z=-1")]
    [DataRow("a=d,d=x,x=1")]
    [DataRow("a=d,d=X,x=1")]
    [DataRow("a=d,d=y,y=1")]
    [DataRow("a=d,d=Y,y=1")]
    [DataRow("a=d,d=z,z=-1")]
    [DataRow("a=d,d=Z,z=-1")]
    public void Delete_PositionalAllAndZSelectors_DoNotAffectVirtualPrototype(
        string controlData)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(controlData));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpVirtualReferenceCount(42));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    [DataRow('i')]
    [DataRow('I')]
    public void Delete_ByImageId_RemovesVirtualPrototype(char selector)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            $"a=d,d={selector},i=42"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpVirtualReferenceCount(42));
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    [DataRow('r')]
    [DataRow('R')]
    public void Delete_ByImageRange_RemovesOnlySelectedVirtualPrototypes(
        char selector)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 2);
        AddVirtualImage(terminal, 41, 10, 20, columns: 1, rows: 1);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        AddVirtualImage(terminal, 43, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[H" +
            Foreground(41) + Placeholder(row: 0, column: 0) +
            Foreground(42) + Placeholder(row: 0, column: 0) +
            Foreground(43) + Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            $"a=d,d={selector},x=42,y=43"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        using var snapshot = terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.KgpPlacements);
        Assert.AreEqual(41u, placement.ImageId);
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(41));
    }

    [TestMethod]
    [DataRow('n')]
    [DataRow('N')]
    public void Delete_ByImageNumber_RemovesNewestImageVirtualPrototype(
        char selector)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,U=1,f=24,s=10,v=20,I=9,p=7,c=1,r=1,q=2",
            KgpTestHelper.CreatePixelData(10, 20, KgpFormat.Rgb24)));
        var image = terminal.KgpImageStore.GetImageByNumber(9);
        Assert.IsNotNull(image);
        Apply(terminal,
            Foreground(image.ImageId) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            $"a=d,d={selector},I=9,p=7"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public void Delete_ByImageNumberAndPlacementId_RemovesOnlyExactPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,U=1,f=24,s=10,v=20,I=9,p=1,c=1,r=1,q=2",
            KgpTestHelper.CreatePixelData(10, 20, KgpFormat.Rgb24)));
        var image = terminal.KgpImageStore.GetImageByNumber(9);
        Assert.IsNotNull(image);
        Apply(terminal, KgpTestHelper.BuildCommand(
            $"a=p,U=1,i={image.ImageId},p=2,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(image.ImageId) +
            UnderlineColor(2) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=n,I=9,p=1"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpVirtualReferenceCount(image.ImageId));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(image.ImageId));
        Assert.AreEqual(
            2u,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).PlacementId);
    }

    [TestMethod]
    public void Delete_ByIdFreeDataWithPlacementId_RetainsSurvivingPrototypeData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 10, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=1,c=1,r=1,q=2"));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=2,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(2) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=I,i=42,p=1"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        Assert.AreEqual(
            2u,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).PlacementId);
    }

    [TestMethod]
    public void Delete_ByImageIdAndPlacementId_RemovesOnlyExactPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 2);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=1,c=1,r=1,q=2"));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=2,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(1) +
            Placeholder(row: 0, column: 0) +
            UnderlineColor(2) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=i,i=42,p=1"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpVirtualReferenceCount(42));
        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(2u, placement.PlacementId);
        Assert.AreEqual(1, placement.Column);
    }

    [TestMethod]
    public void Delete_AllFreeData_RetainsImageOwnedByVirtualPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=A"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public void Delete_ByIdFreeDataWithVirtualOwner_RemovesSelectedHistoryPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            scrollbackCapacity: 2);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 10, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=1,c=1,r=1,C=1,q=2"));
        Apply(terminal, "\x1b[S");
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=2,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(2) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=I,i=42,p=1"));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        var placement = TestSeq.Single(snapshot.KgpPlacements);
        Assert.AreEqual(2u, placement.PlacementId);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        terminal.ValidateKgpDeletionInvariants();
    }

    [TestMethod]
    public void Delete_AllFreeDataDoesNotSelectHistoryOrVirtualOwners()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            scrollbackCapacity: 2);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 10, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=1,c=1,r=1,C=1,q=2"));
        Apply(terminal, "\x1b[S");
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=2,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(2) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=A"));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 1));
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 2));
        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        terminal.ValidateKgpDeletionInvariants();
    }

    [TestMethod]
    public void Delete_ByIdAfterScrollback_RemovesPrototypeWithoutDanglingSnapshot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            scrollbackCapacity: 2);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m\x1b[S");
        TestSeq.Single(
            terminal.CreateSnapshot(scrollbackLines: 1).KgpPlacements);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=i,i=42"));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }
}
