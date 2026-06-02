using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="FoldingRegion"/>, <see cref="FoldingRegionKind"/>,
/// and their integration with <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
[TestClass]
public class FoldingRegionTests
{
    private static IEditorSession CreateSession(string text = "line1\nline2\nline3\nline4\nline5")
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        return (IEditorSession)node;
    }

    // ── IEditorSession integration ───────────────────────────

    [TestMethod]
    public void SetFoldingRegions_WithRegions_StoresAndReturnsRegions()
    {
        var session = CreateSession();
        var regions = new List<FoldingRegion>
        {
            new(1, 5),
            new(2, 4, FoldingRegionKind.Comment)
        };

        session.SetFoldingRegions(regions);

        Assert.AreEqual(2, session.FoldingRegions.Count);
        Assert.AreEqual(1, session.FoldingRegions[0].StartLine);
        Assert.AreEqual(5, session.FoldingRegions[0].EndLine);
    }

    [TestMethod]
    public void SetFoldingRegions_CalledTwice_ReplacesPreviousRegions()
    {
        var session = CreateSession();
        session.SetFoldingRegions([new FoldingRegion(1, 3)]);

        session.SetFoldingRegions([new FoldingRegion(2, 5)]);

        TestSeq.Single(session.FoldingRegions);
        Assert.AreEqual(2, session.FoldingRegions[0].StartLine);
    }

    [TestMethod]
    public void SetFoldingRegions_EmptyList_ClearsPreviousRegions()
    {
        var session = CreateSession();
        session.SetFoldingRegions([new FoldingRegion(1, 3)]);

        session.SetFoldingRegions([]);

        Assert.IsEmpty(session.FoldingRegions);
    }

    [TestMethod]
    public void FoldingRegions_Default_ReturnsEmptyList()
    {
        var session = CreateSession();

        Assert.IsEmpty(session.FoldingRegions);
    }

    // ── FoldingRegion construction ───────────────────────────

    [TestMethod]
    public void FoldingRegion_DefaultKind_IsRegion()
    {
        var region = new FoldingRegion(1, 10);

        Assert.AreEqual(FoldingRegionKind.Region, region.Kind);
    }

    [TestMethod]
    public void FoldingRegion_DefaultIsCollapsed_IsFalse()
    {
        var region = new FoldingRegion(1, 10);

        Assert.IsFalse(region.IsCollapsed);
    }

    [TestMethod]
    public void FoldingRegion_WithIsCollapsedTrue_PreservesValue()
    {
        var region = new FoldingRegion(1, 10) { IsCollapsed = true };

        Assert.IsTrue(region.IsCollapsed);
    }

    [TestMethod]
    [DataRow(FoldingRegionKind.Region)]
    [DataRow(FoldingRegionKind.Comment)]
    [DataRow(FoldingRegionKind.Imports)]
    public void FoldingRegion_AllKinds_AreValid(FoldingRegionKind kind)
    {
        var region = new FoldingRegion(1, 5, kind);

        Assert.AreEqual(kind, region.Kind);
    }

    [TestMethod]
    public void FoldingRegion_CommentKind_SetsCorrectly()
    {
        var region = new FoldingRegion(1, 5, FoldingRegionKind.Comment);

        Assert.AreEqual(FoldingRegionKind.Comment, region.Kind);
        Assert.AreEqual(1, region.StartLine);
        Assert.AreEqual(5, region.EndLine);
    }

    [TestMethod]
    public void FoldingRegion_ImportsKind_SetsCorrectly()
    {
        var region = new FoldingRegion(1, 3, FoldingRegionKind.Imports);

        Assert.AreEqual(FoldingRegionKind.Imports, region.Kind);
    }

    // ── Overlapping / nested regions ─────────────────────────

    [TestMethod]
    public void SetFoldingRegions_OverlappingNestedRegions_StoresAll()
    {
        var session = CreateSession();
        var regions = new List<FoldingRegion>
        {
            new(1, 10, FoldingRegionKind.Region),
            new(3, 7, FoldingRegionKind.Comment),
            new(4, 6, FoldingRegionKind.Region)
        };

        session.SetFoldingRegions(regions);

        Assert.AreEqual(3, session.FoldingRegions.Count);
        Assert.AreEqual(1, session.FoldingRegions[0].StartLine);
        Assert.AreEqual(10, session.FoldingRegions[0].EndLine);
        Assert.AreEqual(3, session.FoldingRegions[1].StartLine);
        Assert.AreEqual(7, session.FoldingRegions[1].EndLine);
        Assert.AreEqual(4, session.FoldingRegions[2].StartLine);
        Assert.AreEqual(6, session.FoldingRegions[2].EndLine);
    }

    [TestMethod]
    public void SetFoldingRegions_MixedCollapsedState_PreservesEach()
    {
        var session = CreateSession();
        session.SetFoldingRegions([
            new FoldingRegion(1, 5) { IsCollapsed = true },
            new FoldingRegion(6, 10) { IsCollapsed = false },
            new FoldingRegion(11, 15) { IsCollapsed = true }
        ]);

        Assert.IsTrue(session.FoldingRegions[0].IsCollapsed);
        Assert.IsFalse(session.FoldingRegions[1].IsCollapsed);
        Assert.IsTrue(session.FoldingRegions[2].IsCollapsed);
    }

    // ── Record equality ──────────────────────────────────────

    [TestMethod]
    public void FoldingRegion_RecordEquality_EqualWhenSameValues()
    {
        var a = new FoldingRegion(1, 10, FoldingRegionKind.Comment);
        var b = new FoldingRegion(1, 10, FoldingRegionKind.Comment);

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void FoldingRegion_RecordEquality_NotEqualWhenDifferentCollapsed()
    {
        var a = new FoldingRegion(1, 10) { IsCollapsed = false };
        var b = new FoldingRegion(1, 10) { IsCollapsed = true };

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void FoldingRegion_RecordEquality_NotEqualWhenDifferentKind()
    {
        var a = new FoldingRegion(1, 10, FoldingRegionKind.Region);
        var b = new FoldingRegion(1, 10, FoldingRegionKind.Comment);

        Assert.AreNotEqual(a, b);
    }
}
