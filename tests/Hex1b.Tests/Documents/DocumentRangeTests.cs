using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

[TestClass]
public class DocumentRangeTests
{
    [TestMethod]
    public void Constructor_ValidRange_Succeeds()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.AreEqual(new DocumentOffset(2), range.Start);
        Assert.AreEqual(new DocumentOffset(5), range.End);
    }

    [TestMethod]
    public void Constructor_EmptyRange_Succeeds()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.AreEqual(new DocumentOffset(3), range.Start);
        Assert.AreEqual(new DocumentOffset(3), range.End);
    }

    [TestMethod]
    public void Constructor_EndBeforeStart_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(3)));
    }

    // --- Properties ---

    [TestMethod]
    public void Length_ReturnsCorrectLength()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(7));
        Assert.AreEqual(5, range.Length);
    }

    [TestMethod]
    public void Length_EmptyRange_ReturnsZero()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.AreEqual(0, range.Length);
    }

    [TestMethod]
    public void IsEmpty_EmptyRange_ReturnsTrue()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.IsTrue(range.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_NonEmptyRange_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(4));
        Assert.IsFalse(range.IsEmpty);
    }

    // --- Contains ---

    [TestMethod]
    public void Contains_OffsetInside_ReturnsTrue()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsTrue(range.Contains(new DocumentOffset(3)));
    }

    [TestMethod]
    public void Contains_OffsetAtStart_ReturnsTrue()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsTrue(range.Contains(new DocumentOffset(2)));
    }

    [TestMethod]
    public void Contains_OffsetAtEnd_ReturnsFalse()
    {
        // Ranges are [start, end) — exclusive end
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsFalse(range.Contains(new DocumentOffset(5)));
    }

    [TestMethod]
    public void Contains_OffsetBefore_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsFalse(range.Contains(new DocumentOffset(1)));
    }

    [TestMethod]
    public void Contains_OffsetAfter_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsFalse(range.Contains(new DocumentOffset(6)));
    }

    [TestMethod]
    public void Contains_EmptyRange_NeverContains()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.IsFalse(range.Contains(new DocumentOffset(3)));
    }

    // --- Overlaps ---

    [TestMethod]
    public void Overlaps_PartialOverlap_ReturnsTrue()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(4), new DocumentOffset(7));
        Assert.IsTrue(a.Overlaps(b));
        Assert.IsTrue(b.Overlaps(a)); // Symmetric
    }

    [TestMethod]
    public void Overlaps_FullyContained_ReturnsTrue()
    {
        var outer = new DocumentRange(new DocumentOffset(1), new DocumentOffset(10));
        var inner = new DocumentRange(new DocumentOffset(3), new DocumentOffset(5));
        Assert.IsTrue(outer.Overlaps(inner));
        Assert.IsTrue(inner.Overlaps(outer));
    }

    [TestMethod]
    public void Overlaps_Adjacent_ReturnsFalse()
    {
        // [2,5) and [5,8) — they share boundary but no overlapping character
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(5), new DocumentOffset(8));
        Assert.IsFalse(a.Overlaps(b));
        Assert.IsFalse(b.Overlaps(a));
    }

    [TestMethod]
    public void Overlaps_Disjoint_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(1), new DocumentOffset(3));
        var b = new DocumentRange(new DocumentOffset(5), new DocumentOffset(8));
        Assert.IsFalse(a.Overlaps(b));
    }

    [TestMethod]
    public void Overlaps_EmptyRange_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsFalse(a.Overlaps(b));
    }

    [TestMethod]
    public void Overlaps_BothEmpty_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        var b = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.IsFalse(a.Overlaps(b));
    }

    // --- Equality ---

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentStart_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(3), new DocumentOffset(5));
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentEnd_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(6));
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_BoxedDifferentType_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsFalse(range.Equals("not a range"));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.IsFalse(range.Equals(null));
    }

    [TestMethod]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)) ==
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)));
    }

    [TestMethod]
    public void OperatorNotEquals_DifferentValues_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)) !=
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(6)));
    }

    // --- GetHashCode / ToString ---

    [TestMethod]
    public void GetHashCode_SameValues_SameHash()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void ToString_FormatsCorrectly()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.AreEqual("[2..5)", range.ToString());
    }
}
