using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

public class DocumentPositionTests
{
    [Fact]
    public void Constructor_ValidLineAndColumn_Succeeds()
    {
        var pos = new DocumentPosition(1, 1);
        Assert.Equal(1, pos.Line);
        Assert.Equal(1, pos.Column);
    }

    [Fact]
    public void Constructor_LargeValues_Succeeds()
    {
        var pos = new DocumentPosition(10000, 500);
        Assert.Equal(10000, pos.Line);
        Assert.Equal(500, pos.Column);
    }

    [Fact]
    public void Constructor_ZeroLine_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentPosition(0, 1));
    }

    [Fact]
    public void Constructor_NegativeLine_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentPosition(-1, 1));
    }

    [Fact]
    public void Constructor_ZeroColumn_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentPosition(1, 0));
    }

    [Fact]
    public void Constructor_NegativeColumn_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentPosition(1, -1));
    }

    // --- Comparison ---

    [Fact]
    public void CompareTo_SameLine_ComparesByColumn()
    {
        var a = new DocumentPosition(1, 3);
        var b = new DocumentPosition(1, 5);
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_DifferentLines_ComparesByLine()
    {
        var a = new DocumentPosition(1, 100);
        var b = new DocumentPosition(2, 1);
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_Equal_ReturnsZero()
    {
        var a = new DocumentPosition(3, 7);
        Assert.Equal(0, a.CompareTo(new DocumentPosition(3, 7)));
    }

    [Fact]
    public void LessThan_DifferentLines_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(1, 5) < new DocumentPosition(2, 1));
    }

    [Fact]
    public void LessThan_SameLineSmallerCol_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(1, 3) < new DocumentPosition(1, 5));
    }

    [Fact]
    public void LessThan_Equal_ReturnsFalse()
    {
        Assert.False(new DocumentPosition(1, 1) < new DocumentPosition(1, 1));
    }

    [Fact]
    public void GreaterThan_DifferentLines_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(3, 1) > new DocumentPosition(2, 99));
    }

    [Fact]
    public void GreaterThan_SameLineLargerCol_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(1, 5) > new DocumentPosition(1, 3));
    }

    [Fact]
    public void LessThanOrEqual_Equal_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(2, 3) <= new DocumentPosition(2, 3));
    }

    [Fact]
    public void GreaterThanOrEqual_Equal_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(2, 3) >= new DocumentPosition(2, 3));
    }

    // --- Equality ---

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(2, 3).Equals(new DocumentPosition(2, 3)));
    }

    [Fact]
    public void Equals_DifferentLine_ReturnsFalse()
    {
        Assert.False(new DocumentPosition(1, 3).Equals(new DocumentPosition(2, 3)));
    }

    [Fact]
    public void Equals_DifferentColumn_ReturnsFalse()
    {
        Assert.False(new DocumentPosition(2, 3).Equals(new DocumentPosition(2, 4)));
    }

    [Fact]
    public void Equals_BoxedSameValue_ReturnsTrue()
    {
        object boxed = new DocumentPosition(2, 3);
        Assert.True(new DocumentPosition(2, 3).Equals(boxed));
    }

    [Fact]
    public void Equals_BoxedDifferentType_ReturnsFalse()
    {
        Assert.False(new DocumentPosition(2, 3).Equals(42));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Assert.False(new DocumentPosition(2, 3).Equals(null));
    }

    [Fact]
    public void OperatorEquals_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(2, 3) == new DocumentPosition(2, 3));
    }

    [Fact]
    public void OperatorNotEquals_ReturnsTrue()
    {
        Assert.True(new DocumentPosition(2, 3) != new DocumentPosition(2, 4));
    }

    // --- GetHashCode / ToString ---

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        Assert.Equal(
            new DocumentPosition(2, 3).GetHashCode(),
            new DocumentPosition(2, 3).GetHashCode());
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        Assert.Equal("(2,3)", new DocumentPosition(2, 3).ToString());
    }
}
