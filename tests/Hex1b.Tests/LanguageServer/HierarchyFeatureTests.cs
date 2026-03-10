using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

public class HierarchyFeatureTests
{
    // ── SelectionRange ──────────────────────────────────────

    [Fact]
    public void SelectionRange_HasRangeAndParent()
    {
        var inner = new SelectionRange
        {
            Range = new LspRange
            {
                Start = new LspPosition { Line = 5, Character = 10 },
                End = new LspPosition { Line = 5, Character = 20 },
            },
        };

        var outer = new SelectionRange
        {
            Range = new LspRange
            {
                Start = new LspPosition { Line = 4, Character = 0 },
                End = new LspPosition { Line = 6, Character = 0 },
            },
            Parent = inner,
        };

        Assert.Equal(4, outer.Range.Start.Line);
        Assert.NotNull(outer.Parent);
        Assert.Equal(5, outer.Parent.Range.Start.Line);
        Assert.Null(inner.Parent);
    }

    // ── CallHierarchyItem ───────────────────────────────────

    [Fact]
    public void CallHierarchyItem_HasExpectedProperties()
    {
        var item = new CallHierarchyItem
        {
            Name = "DoWork",
            Kind = 12, // Function
            Uri = "file:///src/worker.cs",
            Range = new LspRange
            {
                Start = new LspPosition { Line = 10, Character = 0 },
                End = new LspPosition { Line = 15, Character = 1 },
            },
            SelectionRange = new LspRange
            {
                Start = new LspPosition { Line = 10, Character = 16 },
                End = new LspPosition { Line = 10, Character = 22 },
            },
        };

        Assert.Equal("DoWork", item.Name);
        Assert.Equal(12, item.Kind);
        Assert.Equal("file:///src/worker.cs", item.Uri);
        Assert.Equal(10, item.Range.Start.Line);
        Assert.Equal(16, item.SelectionRange.Start.Character);
    }

    [Fact]
    public void CallHierarchyIncomingCall_HasFromAndRanges()
    {
        var call = new CallHierarchyIncomingCall
        {
            From = new CallHierarchyItem
            {
                Name = "Caller",
                Kind = 6,
                Uri = "file:///src/caller.cs",
                Range = new LspRange(),
                SelectionRange = new LspRange(),
            },
            FromRanges =
            [
                new LspRange
                {
                    Start = new LspPosition { Line = 3, Character = 8 },
                    End = new LspPosition { Line = 3, Character = 14 },
                },
            ],
        };

        Assert.Equal("Caller", call.From.Name);
        Assert.Single(call.FromRanges);
        Assert.Equal(3, call.FromRanges[0].Start.Line);
    }

    [Fact]
    public void CallHierarchyOutgoingCall_HasToAndRanges()
    {
        var call = new CallHierarchyOutgoingCall
        {
            To = new CallHierarchyItem
            {
                Name = "Callee",
                Kind = 12,
                Uri = "file:///src/callee.cs",
                Range = new LspRange(),
                SelectionRange = new LspRange(),
            },
            FromRanges =
            [
                new LspRange
                {
                    Start = new LspPosition { Line = 7, Character = 4 },
                    End = new LspPosition { Line = 7, Character = 10 },
                },
            ],
        };

        Assert.Equal("Callee", call.To.Name);
        Assert.Single(call.FromRanges);
    }

    // ── TypeHierarchyItem ───────────────────────────────────

    [Fact]
    public void TypeHierarchyItem_HasExpectedProperties()
    {
        var item = new TypeHierarchyItem
        {
            Name = "BaseClass",
            Kind = 5, // Class
            Uri = "file:///src/base.cs",
            Range = new LspRange
            {
                Start = new LspPosition { Line = 0, Character = 0 },
                End = new LspPosition { Line = 20, Character = 1 },
            },
            SelectionRange = new LspRange
            {
                Start = new LspPosition { Line = 0, Character = 14 },
                End = new LspPosition { Line = 0, Character = 23 },
            },
        };

        Assert.Equal("BaseClass", item.Name);
        Assert.Equal(5, item.Kind);
        Assert.Equal("file:///src/base.cs", item.Uri);
        Assert.Equal(0, item.Range.Start.Line);
        Assert.Equal(14, item.SelectionRange.Start.Character);
    }

    [Fact]
    public void CallHierarchyItem_DefaultValues_AreEmpty()
    {
        var item = new CallHierarchyItem();

        Assert.Equal("", item.Name);
        Assert.Equal(0, item.Kind);
        Assert.Equal("", item.Uri);
        Assert.NotNull(item.Range);
        Assert.NotNull(item.SelectionRange);
        Assert.Null(item.Data);
    }

    [Fact]
    public void TypeHierarchyItem_DefaultValues_AreEmpty()
    {
        var item = new TypeHierarchyItem();

        Assert.Equal("", item.Name);
        Assert.Equal(0, item.Kind);
        Assert.Equal("", item.Uri);
        Assert.NotNull(item.Range);
        Assert.NotNull(item.SelectionRange);
        Assert.Null(item.Data);
    }

    [Fact]
    public void CallHierarchyIncomingCall_DefaultFromRanges_IsEmpty()
    {
        var call = new CallHierarchyIncomingCall();

        Assert.NotNull(call.From);
        Assert.Empty(call.FromRanges);
    }

    [Fact]
    public void CallHierarchyOutgoingCall_DefaultFromRanges_IsEmpty()
    {
        var call = new CallHierarchyOutgoingCall();

        Assert.NotNull(call.To);
        Assert.Empty(call.FromRanges);
    }
}
