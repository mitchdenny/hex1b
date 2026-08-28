using System.Text;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Relative-placement conformance derived from the Kitty and Ghostty revisions
/// pinned by issue #399.
/// </summary>
[TestClass]
public class KgpRelativePlacementTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };

    [TestMethod]
    public void Put_RelativeChildAndGrandchild_AccumulateSignedOffsetsWithoutMovingCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 12, height: 8);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Transmit(terminal, 3);
        Apply(terminal, "\x1b[3;4H");
        Put(terminal, "i=1,p=1,c=2,r=2,C=1");
        Apply(terminal, "\x1b[8;12H");
        using var before = terminal.CreateSnapshot();

        Put(terminal, "i=2,p=2,c=2,r=1,P=1,Q=1,H=-2,V=3,C=0");
        Put(terminal, "i=3,p=3,c=1,r=1,P=2,Q=2,H=4,V=-1,C=1");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(before.CursorX, snapshot.CursorX);
        Assert.AreEqual(before.CursorY, snapshot.CursorY);
        AssertPlacement(snapshot, imageId: 1, placementId: 1, row: 2, column: 3);
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 5, column: 1);
        AssertPlacement(snapshot, imageId: 3, placementId: 3, row: 4, column: 5);
    }

    [TestMethod]
    public void Put_ExactAndWildcardParentSelection_UseConcreteDeterministicPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 12, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Transmit(terminal, 3);
        Apply(terminal, "\x1b[1;2H");
        Put(terminal, "i=1,c=1,r=1,C=1");
        Apply(terminal, "\x1b[1;10H");
        Put(terminal, "i=1,c=1,r=1,C=1");
        Apply(terminal, "\x1b[1;7H");
        Put(terminal, "i=1,p=7,c=1,r=1,C=1");

        Put(terminal, "i=2,p=2,c=1,r=1,P=1,H=2,V=1,C=1");
        Put(terminal, "i=3,p=3,c=1,r=1,P=1,Q=7,H=-1,V=2,C=1");

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 1, column: 3);
        AssertPlacement(snapshot, imageId: 3, placementId: 3, row: 2, column: 5);
    }

    [TestMethod]
    public void Put_MissingParentsAndSelfParent_EmitExactErrorsWithoutMutation()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[2;3H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        using var expected = terminal.CreateSnapshot();

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=999,p=9,P=1,Q=1,C=1"));
        Assert.AreEqual(
            "\x1b_Gi=999;ENOENT:Image not found\x1b\\",
            workload.ReadResponse());

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=2,p=2,P=99,Q=1,C=1"));
        Assert.AreEqual(
            "\x1b_Gi=2;ENOPARENT:Parent image not found\x1b\\",
            workload.ReadResponse());

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=2,p=2,P=2,Q=77,C=1"));
        Assert.AreEqual(
            "\x1b_Gi=2;ENOPARENT:Parent placement not found\x1b\\",
            workload.ReadResponse());

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=2,p=2,P=2,C=1"));
        Assert.AreEqual(
            "\x1b_Gi=2;ENOPARENT:Parent placement not found\x1b\\",
            workload.ReadResponse());

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=1,p=1,P=1,Q=1,H=4,V=4,C=1"));
        Assert.AreEqual(
            "\x1b_Gi=1;EINVAL:Placement cannot be its own parent\x1b\\",
            workload.ReadResponse());

        using var actual = terminal.CreateSnapshot();
        Assert.AreEqual(expected.CursorX, actual.CursorX);
        Assert.AreEqual(expected.CursorY, actual.CursorY);
        Assert.AreEqual(1, actual.KgpPlacements.Count);
        AssertPlacement(actual, imageId: 1, placementId: 1, row: 1, column: 2);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    [DataRow(1, true)]
    [DataRow(2, false)]
    public void Put_MissingParent_QuietModePreservesErrorSuppression(
        int quiet,
        bool expectsResponse)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);

        Apply(terminal, KgpTestHelper.BuildCommand(
            $"a=p,i=1,p=1,P=99,Q=1,q={quiet}"));

        if (expectsResponse)
        {
            Assert.AreEqual(
                "\x1b_Gi=1;ENOPARENT:Parent image not found\x1b\\",
                workload.ReadResponse());
        }
        else
        {
            workload.AssertNoResponse();
        }
        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    public void Put_CycleReplacement_EmitsExactErrorAndPreservesOriginalGraph()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[2;2H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=2,V=1,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=1,p=1,P=2,Q=2,H=5,V=5,C=1"));

        Assert.AreEqual(
            "\x1b_Gi=1;ECYCLE:Parent chain creates a cycle\x1b\\",
            workload.ReadResponse());
        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 1, placementId: 1, row: 1, column: 1);
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 2, column: 3);
    }

    [TestMethod]
    public void Put_EightParentLinksSucceed_NinthEmitsTooDeep()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 12, height: 4);
        Transmit(terminal, 1);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        for (var placementId = 2; placementId <= 9; placementId++)
        {
            Put(
                terminal,
                $"i=1,p={placementId},c=1,r=1,P=1,Q={placementId - 1},C=1");
        }

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=1,p=10,c=1,r=1,P=1,Q=9,C=1"));

        Assert.AreEqual(
            "\x1b_Gi=1;ETOODEEP:Parent chain too deep\x1b\\",
            workload.ReadResponse());
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(9, snapshot.KgpPlacements.Count);
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 10));
    }

    [TestMethod]
    public void Put_ReparentingAncestorThatWouldDeepenDescendantPastLimit_IsRejected()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 12, height: 4);
        Transmit(terminal, 1);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        for (var placementId = 2; placementId <= 8; placementId++)
        {
            Put(
                terminal,
                $"i=1,p={placementId},c=1,r=1,P=1,Q={placementId - 1},C=1");
        }
        Put(terminal, "i=1,p=20,c=1,r=1,C=1");
        Put(terminal, "i=1,p=21,c=1,r=1,P=1,Q=20,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=1,p=1,c=1,r=1,P=1,Q=21,C=1"));

        Assert.AreEqual(
            "\x1b_Gi=1;ETOODEEP:Parent chain too deep\x1b\\",
            workload.ReadResponse());
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(10, snapshot.KgpPlacements.Count);
        Assert.IsTrue(snapshot.KgpPlacements.Any(
            placement => placement.PlacementId == 8));
    }

    [TestMethod]
    public void Put_ZeroPlacementIdRelativeChildren_AreDistinctOwners()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");

        Put(terminal, "i=2,c=1,r=1,P=1,Q=1,H=1,C=1");
        Put(terminal, "i=2,c=1,r=1,P=1,Q=1,H=2,C=1");

        using var snapshot = terminal.CreateSnapshot();
        var children = snapshot.KgpPlacements
            .Where(placement => placement.ImageId == 2)
            .OrderBy(placement => placement.Column)
            .ToArray();
        Assert.AreEqual(2, children.Length);
        Assert.AreEqual(0u, children[0].PlacementId);
        Assert.AreEqual(1, children[0].Column);
        Assert.AreEqual(2, children[1].Column);
        Assert.AreEqual(2, snapshot.KgpImages.Count);
    }

    [TestMethod]
    public void Put_WildcardVirtualParentAndPlaceholderChooseSameOldestPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Transmit(terminal, 42);
        Transmit(terminal, 2);
        Put(terminal, "i=42,U=1,c=1,r=1,C=1");
        Put(terminal, "i=42,U=1,c=2,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,H=1,C=1");
        Apply(
            terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, terminal.KgpVirtualPlacementCount);
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 0, column: 1);
    }

    [TestMethod]
    public void TransmitAndDisplay_MissingRelativeParentStoresImageAndDoesNotMoveCursor()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Apply(terminal, "\x1b[3;4H");
        using var before = terminal.CreateSnapshot();

        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=1,v=1,i=2,p=2,c=1,r=1,P=99,Q=1,C=0",
                KgpTestHelper.CreatePixelData(1, 1)));

        Assert.AreEqual(
            "\x1b_Gi=2;ENOPARENT:Parent image not found\x1b\\",
            workload.ReadResponse());
        using var after = terminal.CreateSnapshot();
        Assert.AreEqual(before.CursorX, after.CursorX);
        Assert.AreEqual(before.CursorY, after.CursorY);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsEmpty(after.KgpPlacements);
    }

    [TestMethod]
    public void Put_VirtualParentWithoutCellsDefersChild_ThenUsesIndependentMinimumOrigin()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 7);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Transmit(terminal, 3);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,H=1,V=2,C=1");
        Put(terminal, "i=3,p=3,c=1,r=1,P=2,Q=2,H=2,V=-1,C=1");

        using (var unresolved = terminal.CreateSnapshot())
        {
            Assert.IsEmpty(unresolved.KgpPlacements);
            Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        }

        Apply(
            terminal,
            "\x1b[2;6H" +
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[5;2H" +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using (var realized = terminal.CreateSnapshot())
        {
            Assert.AreEqual(
                2,
                realized.KgpPlacements.Count(
                    placement => placement.ImageId == 42));
            AssertPlacement(
                realized,
                imageId: 2,
                placementId: 2,
                row: 3,
                column: 2);
            AssertPlacement(
                realized,
                imageId: 3,
                placementId: 3,
                row: 2,
                column: 4);
        }

        Apply(terminal, "\x1b[2J");
        using var erased = terminal.CreateSnapshot();
        Assert.IsEmpty(erased.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(3));
    }

    [TestMethod]
    public void VirtualParent_LetterboxedPlaceholderWithoutRenderedFragment_DoesNotSetOrigin()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 5);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,U=1,f=24,s=10,v=10,i=42,p=7,c=1,r=4,q=2",
                KgpTestHelper.CreatePixelData(10, 10, KgpFormat.Rgb24)));
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,C=1");
        Apply(
            terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[3;4H" +
            Placeholder(row: 2, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 2, column: 3);
    }

    [TestMethod]
    public void VirtualParent_LeadingLetterboxCellsDoNotOffsetRenderedOrigin()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 3);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,U=1,f=24,s=10,v=10,i=42,p=7,c=4,r=1,q=2",
                KgpTestHelper.CreatePixelData(10, 10, KgpFormat.Rgb24)));
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,C=1");
        Apply(
            terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            Placeholder() +
            Placeholder() +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 0, column: 1);
    }

    [TestMethod]
    public void VirtualParent_ReflowedPlaceholderMovesRelativeChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 3,
            scrollbackCapacity: 4,
            reflow: KittyReflowStrategy.Instance);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,H=-1,V=1,C=1");
        Apply(
            terminal,
            "A" +
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0mBCDE");

        terminal.Resize(2, 3);

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 42, placementId: 7, row: 0, column: 1);
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 1, column: 0);
    }

    [TestMethod]
    public void Put_ExplicitParentReplacementAndCrossKindReplacement_MoveDescendant()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 7);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[1;1H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,V=1,C=1");
        Apply(terminal, "\x1b[4;5H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");

        using (var moved = terminal.CreateSnapshot())
            AssertPlacement(moved, imageId: 2, placementId: 2, row: 4, column: 5);

        Put(terminal, "i=1,p=1,U=1,c=1,r=1,C=1");
        Apply(
            terminal,
            "\x1b[2;3H" +
            Foreground(1) +
            UnderlineColor(1) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var virtualParent = terminal.CreateSnapshot();
        AssertPlacement(
            virtualParent,
            imageId: 2,
            placementId: 2,
            row: 2,
            column: 3);

        Apply(terminal, "\x1b[5;2H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        using var ordinaryParent = terminal.CreateSnapshot();
        AssertPlacement(
            ordinaryParent,
            imageId: 2,
            placementId: 2,
            row: 5,
            column: 2);
    }

    [TestMethod]
    public void Delete_ParentCascadesAndReclaimsOnlyUnownedDescendantImages()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 7);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Transmit(terminal, 3);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,C=1");
        Put(terminal, "i=3,p=3,c=1,r=1,P=2,Q=2,H=1,C=1");
        Apply(terminal, "\x1b[4;4H");
        Put(terminal, "i=2,p=9,c=1,r=1,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=i,i=1,p=1"));

        using var snapshot = terminal.CreateSnapshot();
        var survivor = TestSeq.Single(snapshot.KgpPlacements);
        Assert.AreEqual(2u, survivor.ImageId);
        Assert.AreEqual(9u, survivor.PlacementId);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(3));
        Assert.IsFalse(snapshot.KgpImages.ContainsKey(3));
    }

    [TestMethod]
    public void Delete_ParentAndSameImageDescendant_ReclaimsNowUnownedImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=1,p=2,c=1,r=1,P=1,Q=1,H=1,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=i,i=1,p=1"));

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void Scrollback_ParentAnchorMovesChildAndPruningRemovesSubtree()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 2);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,V=1,C=1");

        Apply(terminal, "\x1b[S");

        using (var active = terminal.CreateSnapshot())
            AssertPlacement(active, imageId: 2, placementId: 2, row: 0, column: 0);
        using (var history = terminal.CreateSnapshot(scrollbackLines: 1))
        {
            AssertPlacement(history, imageId: 1, placementId: 1, row: 0, column: 0);
            AssertPlacement(history, imageId: 2, placementId: 2, row: 1, column: 0);
        }

        Apply(terminal, "\x1b[S\x1b[S");

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        using var pruned = terminal.CreateSnapshot(scrollbackLines: 2);
        Assert.IsEmpty(pruned.KgpPlacements);
        Assert.IsEmpty(pruned.KgpImages);
    }

    [TestMethod]
    public void Scrollback_CapacityReanchorPreservesParentsLogicalOriginForChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 3,
            scrollbackCapacity: 1);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=32,s=10,v=60,i=1,q=2",
                KgpTestHelper.CreatePixelData(10, 60)));
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=3,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,V=2,C=1");

        Apply(terminal, "\x1b[S\x1b[S");

        using var active = terminal.CreateSnapshot();
        AssertPlacement(active, imageId: 2, placementId: 2, row: 0, column: 0);
        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
    }

    [TestMethod]
    public void Put_HistoryParentResolvesAndSameIdentityReplacementReturnsItToActiveScreen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 2);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Apply(terminal, "\x1b[S");

        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,V=1,C=1");
        using (var active = terminal.CreateSnapshot())
            AssertPlacement(active, imageId: 2, placementId: 2, row: 0, column: 0);

        Apply(terminal, "\x1b[3;4H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        using var moved = terminal.CreateSnapshot(scrollbackLines: 1);
        AssertPlacement(moved, imageId: 1, placementId: 1, row: 3, column: 3);
        AssertPlacement(moved, imageId: 2, placementId: 2, row: 4, column: 3);
    }

    [TestMethod]
    public void VirtualParent_ScrollbackAndPruningUpdateThenUnresolveChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 3,
            scrollbackCapacity: 1);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,V=1,C=1");
        Apply(
            terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Apply(terminal, "\x1b[S");

        using (var active = terminal.CreateSnapshot())
            AssertPlacement(active, imageId: 2, placementId: 2, row: 0, column: 0);
        using (var history = terminal.CreateSnapshot(scrollbackLines: 1))
        {
            AssertPlacement(history, imageId: 42, placementId: 7, row: 0, column: 0);
            AssertPlacement(history, imageId: 2, placementId: 2, row: 1, column: 0);
        }

        Apply(terminal, "\x1b[S");

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        using var unresolved = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(unresolved.KgpPlacements);
    }

    [TestMethod]
    public void VirtualParent_OffWidthHistoryCellCanAnchorVisibleNegativeOffsetChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 3,
            scrollbackCapacity: 2,
            reflow: NoReflowStrategy.Instance);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,H=-7,V=1,C=1");
        Apply(
            terminal,
            "\x1b[1;8H" +
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m\x1b[S");

        terminal.Resize(4, 3);

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 0, column: 0);
    }

    [TestMethod]
    public void Reflow_ParentAnchorMovesRelativeChildWithMappedText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 3,
            reflow: KittyReflowStrategy.Instance);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "ABCDEF\x1b[1;5H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,C=1");

        terminal.Resize(3, 3);

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 1, placementId: 1, row: 1, column: 1);
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 1, column: 2);
    }

    [TestMethod]
    public void MarginScroll_MovesRootAndRelativeChildTogether()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 5);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[3;2H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,V=1,C=1");

        Apply(terminal, "\x1b[2;4r\x1b[S");

        using var snapshot = terminal.CreateSnapshot();
        AssertPlacement(snapshot, imageId: 1, placementId: 1, row: 1, column: 1);
        AssertPlacement(snapshot, imageId: 2, placementId: 2, row: 2, column: 1);
    }

    [TestMethod]
    public void Resize_ClippedParentCascadesChildWithoutFreeingParentData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 3);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[1;5H");
        Put(terminal, "i=1,p=1,c=2,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,C=1");

        terminal.Resize(3, 3);

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    public void Screens_ResolveParentsOnlyWithinActiveScreenAndRestoreMainGraph()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[2;2H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,C=1");

        Apply(terminal, "\x1b[?1049h");
        Transmit(terminal, 2);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=2,p=2,P=1,Q=1,C=1"));
        Assert.AreEqual(
            "\x1b_Gi=2;ENOPARENT:Parent image not found\x1b\\",
            workload.ReadResponse());

        Transmit(terminal, 1);
        Apply(terminal, "\x1b[4;5H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,C=1");
        using (var alternate = terminal.CreateSnapshot())
        {
            Assert.IsTrue(alternate.InAlternateScreen);
            AssertPlacement(
                alternate,
                imageId: 2,
                placementId: 2,
                row: 3,
                column: 5);
        }

        Apply(terminal, "\x1b[?1049l");
        using var main = terminal.CreateSnapshot();
        Assert.IsFalse(main.InAlternateScreen);
        AssertPlacement(main, imageId: 2, placementId: 2, row: 1, column: 2);
    }

    [TestMethod]
    public void RelativePlacement_ClipsDestinationAndSourceAtViewportEdges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 4);
        Transmit(terminal, 1);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=32,s=20,v=20,i=2,q=2",
                KgpTestHelper.CreatePixelData(20, 20)));
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=2,r=2,P=1,Q=1,H=-1,V=-1,C=1");

        using var snapshot = terminal.CreateSnapshot();
        var child = TestSeq.Single(snapshot.KgpPlacements.Where(
            placement => placement.ImageId == 2));
        Assert.AreEqual(0, child.Row);
        Assert.AreEqual(0, child.Column);
        Assert.AreEqual(1u, child.DisplayColumns);
        Assert.AreEqual(1u, child.DisplayRows);
        Assert.AreEqual(10u, child.SourceX);
        Assert.AreEqual(10u, child.SourceY);
        Assert.AreEqual(10u, child.SourceWidth);
        Assert.AreEqual(10u, child.SourceHeight);
    }

    [TestMethod]
    public void Delete_PositionalParentMatch_CascadesDescendant()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[2;3H");
        Put(terminal, "i=1,p=1,c=2,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=2,C=1");
        Apply(terminal, "\x1b[2;3H");

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=c"));

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    public void DeleteAll_VisibleChildOfHistoryParent_RemovesChildPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 2);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,V=1,C=1");
        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(),
            imageId: 2,
            placementId: 2,
            row: 0,
            column: 0);

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=a"));

        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    public void Ed2_VisibleChildOfHistoryParent_RemovesChildAndItsUnownedData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 2);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,V=1,C=1");
        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(),
            imageId: 2,
            placementId: 2,
            row: 0,
            column: 0);

        Apply(terminal, "\x1b[2J");

        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    public void DeleteByZ_UnresolvedRelativePlacement_DoesNotReappear()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,z=5,P=42,Q=7,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand("a=d,d=z,z=5"));
        Apply(
            terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsFalse(snapshot.KgpPlacements.Any(
            placement => placement.ImageId == 2));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    [DataRow("a=d,d=z,z=5")]
    [DataRow("a=d,d=r,x=5,y=5")]
    public void Delete_ZAndRangeParentMatches_CascadeDescendant(string controls)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Transmit(terminal, 5);
        Transmit(terminal, 6);
        Put(terminal, "i=5,p=1,c=1,r=1,z=5,C=1");
        Put(terminal, "i=6,p=2,c=1,r=1,P=5,Q=1,H=1,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand(controls));

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(5));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(6));
    }

    [TestMethod]
    public void DeleteByRange_HistoryParent_CascadesActiveRelativeChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 8,
            height: 4,
            scrollbackCapacity: 2);
        Transmit(terminal, 5);
        Transmit(terminal, 6);
        Put(terminal, "i=5,p=1,c=1,r=1,C=1");
        Put(terminal, "i=6,p=2,c=1,r=1,P=5,Q=1,V=1,C=1");
        Apply(terminal, "\x1b[S");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=r,x=5,y=5"));

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.IsEmpty(terminal.CreateSnapshot(scrollbackLines: 1).KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(5));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(6));
    }

    [TestMethod]
    public void Delete_UnrealizedVirtualParent_CascadesStoredChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,C=1");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=i,i=42,p=7"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public void DeleteById_DataLessVirtualParentStillCascadesStoredChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, imageId: 42, placementId: 7);
        Transmit(terminal, 2);
        Put(terminal, "i=2,p=2,c=1,r=1,P=42,Q=7,C=1");
        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(42));

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=d,d=i,i=42,p=7"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public void Retransmit_ParentImage_RemovesSubtreeBeforeReplacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,C=1");

        Apply(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                1,
                width: 2,
                height: 1,
                quiet: 2,
                fillByte: 0x44));

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.AreEqual(2u, terminal.KgpImageStore.GetImageById(1)!.Width);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    public void AnonymousImageRelocation_PreservesRelativePlacementAndParentEdge()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        Transmit(terminal, 10);
        Put(terminal, "i=10,p=1,c=1,r=1,C=1");
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=1,v=1,c=1,r=1,P=10,Q=1,H=1,C=1,q=2",
                KgpTestHelper.CreatePixelData(1, 1, fillByte: 0x33)));
        var anonymous = TestSeq.Single(terminal.KgpPlacements.Where(
            placement => placement.ImageId != 10));
        Assert.AreEqual(1u, anonymous.ImageId);

        Apply(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                1,
                width: 1,
                height: 1,
                quiet: 2,
                fillByte: 0x44));

        using var snapshot = terminal.CreateSnapshot();
        var relocated = TestSeq.Single(snapshot.KgpPlacements.Where(
            placement => placement.ImageId != 10));
        Assert.AreEqual(2u, relocated.ImageId);
        Assert.AreEqual(1, relocated.Column);
        Assert.AreEqual(0x33, snapshot.KgpImages[2].Data[0]);
        Assert.AreEqual(0x44, terminal.KgpImageStore.GetImageById(1)!.Data[0]);
    }

    [TestMethod]
    [DataRow("\x1b[2J")]
    [DataRow("\u001bc")]
    public void TerminalClear_ParentRemovalCascadesAllDescendants(string operation)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,C=1");

        Apply(terminal, operation);

        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void Dispose_ParentGraphReleasesBothPlacementAndImageOwners()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminal = CreateTerminal(workload);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,C=1");

        terminal.Dispose();

        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    public void Snapshot_ParentReplacementDoesNotMutateCapturedDescendant()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,V=1,C=1");
        using var before = terminal.CreateSnapshot();

        Apply(terminal, "\x1b[4;5H");
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");

        AssertPlacement(before, imageId: 2, placementId: 2, row: 1, column: 1);
        using var after = terminal.CreateSnapshot();
        AssertPlacement(after, imageId: 2, placementId: 2, row: 4, column: 5);
    }

    [TestMethod]
    public async Task Snapshot_ConcurrentParentReplacement_ContainsOneAtomicGraphGeneration()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Put(terminal, "i=1,p=1,c=1,r=1,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,P=1,Q=1,H=1,V=1,C=1");
        var replacement = AnsiTokenizer.Tokenize(
            "\x1b[4;5H" +
            KgpTestHelper.BuildCommand(
                "a=p,i=1,p=1,c=1,r=1,C=1,q=2"));
        using var barrier = new Barrier(2);
        var writer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            terminal.ApplyTokens(replacement);
        });

        barrier.SignalAndWait();
        using var snapshot = terminal.CreateSnapshot();
        await writer;

        var parent = TestSeq.Single(snapshot.KgpPlacements.Where(
            placement => placement.PlacementId == 1));
        var child = TestSeq.Single(snapshot.KgpPlacements.Where(
            placement => placement.PlacementId == 2));
        Assert.AreEqual(parent.Row + 1, child.Row);
        Assert.AreEqual(parent.Column + 1, child.Column);
        Assert.IsTrue(
            (parent.Row == 0 && parent.Column == 0) ||
            (parent.Row == 3 && parent.Column == 4));
    }

    [TestMethod]
    public void Svg_RelativePlacementsUseEffectiveCoordinatesAndOwnZOrder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 6);
        Transmit(terminal, 1);
        Transmit(terminal, 2);
        Apply(terminal, "\x1b[2;3H");
        Put(terminal, "i=1,p=1,c=1,r=1,z=5,C=1");
        Put(terminal, "i=2,p=2,c=1,r=1,z=-2,P=1,Q=1,H=2,V=1,C=1");

        var svg = terminal.CreateSnapshot().ToSvg();
        var parent = GetSvgImageElement(svg, imageId: 1);
        var child = GetSvgImageElement(svg, imageId: 2);
        Assert.Contains("x=\"20\"", parent);
        Assert.Contains("y=\"20\"", parent);
        Assert.Contains("x=\"40\"", child);
        Assert.Contains("y=\"40\"", child);
        Assert.IsTrue(
            svg.IndexOf(child, StringComparison.Ordinal) <
            svg.IndexOf(parent, StringComparison.Ordinal));
    }

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        int width = 20,
        int height = 10,
        int? scrollbackCapacity = null,
        ITerminalReflowProvider? reflow = null)
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height);
        if (scrollbackCapacity.HasValue)
            builder.WithScrollback(scrollbackCapacity.Value);
        if (reflow is not null)
            builder.WithReflow(reflow);
        return builder.Build();
    }

    private static void Transmit(Hex1bTerminal terminal, uint imageId)
        => Apply(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                imageId,
                width: 1,
                height: 1,
                quiet: 2));

    private static void Put(Hex1bTerminal terminal, string controls)
        => Apply(
            terminal,
            KgpTestHelper.BuildCommand($"a=p,{controls},q=2"));

    private static void AddVirtualImage(
        Hex1bTerminal terminal,
        uint imageId,
        uint placementId)
        => Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=T,U=1,f=24,s=10,v=20,i={imageId},p={placementId},c=1,r=1,q=2",
                KgpTestHelper.CreatePixelData(
                    10,
                    20,
                    KgpFormat.Rgb24)));

    private static void Apply(Hex1bTerminal terminal, string value)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(value));

    private static void AssertPlacement(
        Hex1bTerminalSnapshot snapshot,
        uint imageId,
        uint placementId,
        int row,
        int column)
    {
        var placement = TestSeq.Single(snapshot.KgpPlacements.Where(
            placement => placement.ImageId == imageId &&
                placement.PlacementId == placementId));
        Assert.AreEqual(row, placement.Row);
        Assert.AreEqual(column, placement.Column);
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(imageId));
    }

    private static string GetSvgImageElement(string svg, uint imageId)
    {
        var marker = $"data-image-id=\"{imageId}\"";
        var markerIndex = svg.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, markerIndex);
        var start = svg.LastIndexOf("<image ", markerIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        var end = svg.IndexOf("/>", markerIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, end);
        return svg[start..(end + 2)];
    }

    private static string Foreground(uint imageId)
        => $"\x1b[38;2;{(imageId >> 16) & 0xFF};{(imageId >> 8) & 0xFF};{imageId & 0xFF}m";

    private static string UnderlineColor(uint placementId)
        => $"\x1b[58;2;{(placementId >> 16) & 0xFF};{(placementId >> 8) & 0xFF};{placementId & 0xFF}m";

    private static string Placeholder(
        int? row = null,
        int? column = null)
    {
        var builder = new StringBuilder(
            new Rune(KgpUnicodePlaceholder.CodePoint).ToString());
        if (row.HasValue)
            builder.Append(Diacritic(row.Value));
        if (column.HasValue)
            builder.Append(Diacritic(column.Value));
        return builder.ToString();
    }

    private static string Diacritic(int index)
        => new Rune(KgpUnicodePlaceholderDiacritics.CodePoints[index]).ToString();

    private sealed class RecordingWorkloadAdapter : IHex1bTerminalWorkloadAdapter
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

        internal string ReadResponse()
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

        internal void AssertNoResponse()
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
