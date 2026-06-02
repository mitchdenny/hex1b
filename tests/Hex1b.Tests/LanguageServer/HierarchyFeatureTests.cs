using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

[TestClass]
public class HierarchyFeatureTests
{
    // ── SelectionRange ──────────────────────────────────────

    [TestMethod]
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

        Assert.AreEqual(4, outer.Range.Start.Line);
        Assert.IsNotNull(outer.Parent);
        Assert.AreEqual(5, outer.Parent.Range.Start.Line);
        Assert.IsNull(inner.Parent);
    }

    // ── CallHierarchyItem ───────────────────────────────────

    [TestMethod]
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

        Assert.AreEqual("DoWork", item.Name);
        Assert.AreEqual(12, item.Kind);
        Assert.AreEqual("file:///src/worker.cs", item.Uri);
        Assert.AreEqual(10, item.Range.Start.Line);
        Assert.AreEqual(16, item.SelectionRange.Start.Character);
    }

    [TestMethod]
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

        Assert.AreEqual("Caller", call.From.Name);
        TestSeq.Single(call.FromRanges);
        Assert.AreEqual(3, call.FromRanges[0].Start.Line);
    }

    [TestMethod]
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

        Assert.AreEqual("Callee", call.To.Name);
        TestSeq.Single(call.FromRanges);
    }

    // ── TypeHierarchyItem ───────────────────────────────────

    [TestMethod]
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

        Assert.AreEqual("BaseClass", item.Name);
        Assert.AreEqual(5, item.Kind);
        Assert.AreEqual("file:///src/base.cs", item.Uri);
        Assert.AreEqual(0, item.Range.Start.Line);
        Assert.AreEqual(14, item.SelectionRange.Start.Character);
    }

    [TestMethod]
    public void CallHierarchyItem_DefaultValues_AreEmpty()
    {
        var item = new CallHierarchyItem();

        Assert.AreEqual("", item.Name);
        Assert.AreEqual(0, item.Kind);
        Assert.AreEqual("", item.Uri);
        Assert.IsNotNull(item.Range);
        Assert.IsNotNull(item.SelectionRange);
        Assert.IsNull(item.Data);
    }

    [TestMethod]
    public void TypeHierarchyItem_DefaultValues_AreEmpty()
    {
        var item = new TypeHierarchyItem();

        Assert.AreEqual("", item.Name);
        Assert.AreEqual(0, item.Kind);
        Assert.AreEqual("", item.Uri);
        Assert.IsNotNull(item.Range);
        Assert.IsNotNull(item.SelectionRange);
        Assert.IsNull(item.Data);
    }

    [TestMethod]
    public void CallHierarchyIncomingCall_DefaultFromRanges_IsEmpty()
    {
        var call = new CallHierarchyIncomingCall();

        Assert.IsNotNull(call.From);
        Assert.IsEmpty(call.FromRanges);
    }

    [TestMethod]
    public void CallHierarchyOutgoingCall_DefaultFromRanges_IsEmpty()
    {
        var call = new CallHierarchyOutgoingCall();

        Assert.IsNotNull(call.To);
        Assert.IsEmpty(call.FromRanges);
    }
}
