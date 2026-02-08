using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

public class DocumentRangeTests
{
    [Fact]
    public void Constructor_ValidRange_Succeeds()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.Equal(new DocumentOffset(2), range.Start);
        Assert.Equal(new DocumentOffset(5), range.End);
    }

    [Fact]
    public void Constructor_EmptyRange_Succeeds()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.Equal(new DocumentOffset(3), range.Start);
        Assert.Equal(new DocumentOffset(3), range.End);
    }

    [Fact]
    public void Constructor_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(3)));
    }

    // --- Properties ---

    [Fact]
    public void Length_ReturnsCorrectLength()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(7));
        Assert.Equal(5, range.Length);
    }

    [Fact]
    public void Length_EmptyRange_ReturnsZero()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.Equal(0, range.Length);
    }

    [Fact]
    public void IsEmpty_EmptyRange_ReturnsTrue()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.True(range.IsEmpty);
    }

    [Fact]
    public void IsEmpty_NonEmptyRange_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(4));
        Assert.False(range.IsEmpty);
    }

    // --- Contains ---

    [Fact]
    public void Contains_OffsetInside_ReturnsTrue()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.True(range.Contains(new DocumentOffset(3)));
    }

    [Fact]
    public void Contains_OffsetAtStart_ReturnsTrue()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.True(range.Contains(new DocumentOffset(2)));
    }

    [Fact]
    public void Contains_OffsetAtEnd_ReturnsFalse()
    {
        // Ranges are [start, end) — exclusive end
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.False(range.Contains(new DocumentOffset(5)));
    }

    [Fact]
    public void Contains_OffsetBefore_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.False(range.Contains(new DocumentOffset(1)));
    }

    [Fact]
    public void Contains_OffsetAfter_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.False(range.Contains(new DocumentOffset(6)));
    }

    [Fact]
    public void Contains_EmptyRange_NeverContains()
    {
        var range = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.False(range.Contains(new DocumentOffset(3)));
    }

    // --- Overlaps ---

    [Fact]
    public void Overlaps_PartialOverlap_ReturnsTrue()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(4), new DocumentOffset(7));
        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a)); // Symmetric
    }

    [Fact]
    public void Overlaps_FullyContained_ReturnsTrue()
    {
        var outer = new DocumentRange(new DocumentOffset(1), new DocumentOffset(10));
        var inner = new DocumentRange(new DocumentOffset(3), new DocumentOffset(5));
        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
    }

    [Fact]
    public void Overlaps_Adjacent_ReturnsFalse()
    {
        // [2,5) and [5,8) — they share boundary but no overlapping character
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(5), new DocumentOffset(8));
        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_Disjoint_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(1), new DocumentOffset(3));
        var b = new DocumentRange(new DocumentOffset(5), new DocumentOffset(8));
        Assert.False(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_EmptyRange_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.False(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_BothEmpty_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        var b = new DocumentRange(new DocumentOffset(3), new DocumentOffset(3));
        Assert.False(a.Overlaps(b));
    }

    // --- Equality ---

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentStart_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(3), new DocumentOffset(5));
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentEnd_ReturnsFalse()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(6));
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_BoxedDifferentType_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.False(range.Equals("not a range"));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.False(range.Equals(null));
    }

    [Fact]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        Assert.True(
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)) ==
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)));
    }

    [Fact]
    public void OperatorNotEquals_DifferentValues_ReturnsTrue()
    {
        Assert.True(
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(5)) !=
            new DocumentRange(new DocumentOffset(2), new DocumentOffset(6)));
    }

    // --- GetHashCode / ToString ---

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        var a = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        var b = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var range = new DocumentRange(new DocumentOffset(2), new DocumentOffset(5));
        Assert.Equal("[2..5)", range.ToString());
    }
}
