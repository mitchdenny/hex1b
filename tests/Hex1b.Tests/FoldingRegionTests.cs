using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="FoldingRegion"/>, <see cref="FoldingRegionKind"/>,
/// and their integration with <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
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

    [Fact]
    public void SetFoldingRegions_WithRegions_StoresAndReturnsRegions()
    {
        var session = CreateSession();
        var regions = new List<FoldingRegion>
        {
            new(1, 5),
            new(2, 4, FoldingRegionKind.Comment)
        };

        session.SetFoldingRegions(regions);

        Assert.Equal(2, session.FoldingRegions.Count);
        Assert.Equal(1, session.FoldingRegions[0].StartLine);
        Assert.Equal(5, session.FoldingRegions[0].EndLine);
    }

    [Fact]
    public void SetFoldingRegions_CalledTwice_ReplacesPreviousRegions()
    {
        var session = CreateSession();
        session.SetFoldingRegions([new FoldingRegion(1, 3)]);

        session.SetFoldingRegions([new FoldingRegion(2, 5)]);

        Assert.Single(session.FoldingRegions);
        Assert.Equal(2, session.FoldingRegions[0].StartLine);
    }

    [Fact]
    public void SetFoldingRegions_EmptyList_ClearsPreviousRegions()
    {
        var session = CreateSession();
        session.SetFoldingRegions([new FoldingRegion(1, 3)]);

        session.SetFoldingRegions([]);

        Assert.Empty(session.FoldingRegions);
    }

    [Fact]
    public void FoldingRegions_Default_ReturnsEmptyList()
    {
        var session = CreateSession();

        Assert.Empty(session.FoldingRegions);
    }

    // ── FoldingRegion construction ───────────────────────────

    [Fact]
    public void FoldingRegion_DefaultKind_IsRegion()
    {
        var region = new FoldingRegion(1, 10);

        Assert.Equal(FoldingRegionKind.Region, region.Kind);
    }

    [Fact]
    public void FoldingRegion_DefaultIsCollapsed_IsFalse()
    {
        var region = new FoldingRegion(1, 10);

        Assert.False(region.IsCollapsed);
    }

    [Fact]
    public void FoldingRegion_WithIsCollapsedTrue_PreservesValue()
    {
        var region = new FoldingRegion(1, 10) { IsCollapsed = true };

        Assert.True(region.IsCollapsed);
    }

    [Theory]
    [InlineData(FoldingRegionKind.Region)]
    [InlineData(FoldingRegionKind.Comment)]
    [InlineData(FoldingRegionKind.Imports)]
    public void FoldingRegion_AllKinds_AreValid(FoldingRegionKind kind)
    {
        var region = new FoldingRegion(1, 5, kind);

        Assert.Equal(kind, region.Kind);
    }

    [Fact]
    public void FoldingRegion_CommentKind_SetsCorrectly()
    {
        var region = new FoldingRegion(1, 5, FoldingRegionKind.Comment);

        Assert.Equal(FoldingRegionKind.Comment, region.Kind);
        Assert.Equal(1, region.StartLine);
        Assert.Equal(5, region.EndLine);
    }

    [Fact]
    public void FoldingRegion_ImportsKind_SetsCorrectly()
    {
        var region = new FoldingRegion(1, 3, FoldingRegionKind.Imports);

        Assert.Equal(FoldingRegionKind.Imports, region.Kind);
    }

    // ── Overlapping / nested regions ─────────────────────────

    [Fact]
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

        Assert.Equal(3, session.FoldingRegions.Count);
        Assert.Equal(1, session.FoldingRegions[0].StartLine);
        Assert.Equal(10, session.FoldingRegions[0].EndLine);
        Assert.Equal(3, session.FoldingRegions[1].StartLine);
        Assert.Equal(7, session.FoldingRegions[1].EndLine);
        Assert.Equal(4, session.FoldingRegions[2].StartLine);
        Assert.Equal(6, session.FoldingRegions[2].EndLine);
    }

    [Fact]
    public void SetFoldingRegions_MixedCollapsedState_PreservesEach()
    {
        var session = CreateSession();
        session.SetFoldingRegions([
            new FoldingRegion(1, 5) { IsCollapsed = true },
            new FoldingRegion(6, 10) { IsCollapsed = false },
            new FoldingRegion(11, 15) { IsCollapsed = true }
        ]);

        Assert.True(session.FoldingRegions[0].IsCollapsed);
        Assert.False(session.FoldingRegions[1].IsCollapsed);
        Assert.True(session.FoldingRegions[2].IsCollapsed);
    }

    // ── Record equality ──────────────────────────────────────

    [Fact]
    public void FoldingRegion_RecordEquality_EqualWhenSameValues()
    {
        var a = new FoldingRegion(1, 10, FoldingRegionKind.Comment);
        var b = new FoldingRegion(1, 10, FoldingRegionKind.Comment);

        Assert.Equal(a, b);
    }

    [Fact]
    public void FoldingRegion_RecordEquality_NotEqualWhenDifferentCollapsed()
    {
        var a = new FoldingRegion(1, 10) { IsCollapsed = false };
        var b = new FoldingRegion(1, 10) { IsCollapsed = true };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void FoldingRegion_RecordEquality_NotEqualWhenDifferentKind()
    {
        var a = new FoldingRegion(1, 10, FoldingRegionKind.Region);
        var b = new FoldingRegion(1, 10, FoldingRegionKind.Comment);

        Assert.NotEqual(a, b);
    }
}
