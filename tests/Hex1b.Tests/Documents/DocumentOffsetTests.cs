using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

public class DocumentOffsetTests
{
    [Fact]
    public void Constructor_Zero_Succeeds()
    {
        var offset = new DocumentOffset(0);
        Assert.Equal(0, offset.Value);
    }

    [Fact]
    public void Constructor_Positive_Succeeds()
    {
        var offset = new DocumentOffset(42);
        Assert.Equal(42, offset.Value);
    }

    [Fact]
    public void Constructor_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentOffset(-1));
    }

    [Fact]
    public void Zero_ReturnsOffsetWithValueZero()
    {
        Assert.Equal(0, DocumentOffset.Zero.Value);
    }

    // --- Implicit/Explicit Conversion ---

    [Fact]
    public void ImplicitToInt_ReturnsValue()
    {
        var offset = new DocumentOffset(7);
        int val = offset;
        Assert.Equal(7, val);
    }

    [Fact]
    public void ExplicitFromInt_CreatesOffset()
    {
        var offset = (DocumentOffset)5;
        Assert.Equal(5, offset.Value);
    }

    [Fact]
    public void ExplicitFromInt_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (DocumentOffset)(-3));
    }

    // --- Arithmetic Operators ---

    [Fact]
    public void Add_Int_ReturnsNewOffset()
    {
        var offset = new DocumentOffset(5);
        var result = offset + 3;
        Assert.Equal(8, result.Value);
    }

    [Fact]
    public void Add_Zero_ReturnsSame()
    {
        var offset = new DocumentOffset(5);
        var result = offset + 0;
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void SubtractInt_ReturnsNewOffset()
    {
        var offset = new DocumentOffset(5);
        var result = offset - 3;
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void SubtractInt_ToZero_ReturnsZero()
    {
        var offset = new DocumentOffset(5);
        var result = offset - 5;
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void SubtractInt_BelowZero_Throws()
    {
        var offset = new DocumentOffset(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => offset - 3);
    }

    [Fact]
    public void SubtractOffset_ReturnsDifference()
    {
        var a = new DocumentOffset(10);
        var b = new DocumentOffset(3);
        Assert.Equal(7, a - b);
    }

    [Fact]
    public void SubtractOffset_SameValue_ReturnsZero()
    {
        var a = new DocumentOffset(5);
        Assert.Equal(0, a - a);
    }

    [Fact]
    public void SubtractOffset_Negative_Allowed()
    {
        // Offset subtraction returns int, which can be negative
        var a = new DocumentOffset(3);
        var b = new DocumentOffset(10);
        Assert.Equal(-7, a - b);
    }

    // --- Comparison Operators ---

    [Fact]
    public void LessThan_WhenSmaller_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(1) < new DocumentOffset(2));
    }

    [Fact]
    public void LessThan_WhenEqual_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(2) < new DocumentOffset(2));
    }

    [Fact]
    public void LessThan_WhenGreater_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(3) < new DocumentOffset(2));
    }

    [Fact]
    public void GreaterThan_WhenGreater_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(3) > new DocumentOffset(2));
    }

    [Fact]
    public void GreaterThan_WhenEqual_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(2) > new DocumentOffset(2));
    }

    [Fact]
    public void LessThanOrEqual_WhenEqual_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(2) <= new DocumentOffset(2));
    }

    [Fact]
    public void LessThanOrEqual_WhenSmaller_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(1) <= new DocumentOffset(2));
    }

    [Fact]
    public void LessThanOrEqual_WhenGreater_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(3) <= new DocumentOffset(2));
    }

    [Fact]
    public void GreaterThanOrEqual_WhenEqual_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(2) >= new DocumentOffset(2));
    }

    [Fact]
    public void GreaterThanOrEqual_WhenGreater_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(3) >= new DocumentOffset(2));
    }

    [Fact]
    public void GreaterThanOrEqual_WhenSmaller_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(1) >= new DocumentOffset(2));
    }

    // --- Equality ---

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(5).Equals(new DocumentOffset(5)));
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(5).Equals(new DocumentOffset(6)));
    }

    [Fact]
    public void Equals_BoxedSameValue_ReturnsTrue()
    {
        object boxed = new DocumentOffset(5);
        Assert.True(new DocumentOffset(5).Equals(boxed));
    }

    [Fact]
    public void Equals_BoxedDifferentType_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(5).Equals("not an offset"));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Assert.False(new DocumentOffset(5).Equals(null));
    }

    [Fact]
    public void OperatorEquals_SameValue_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(5) == new DocumentOffset(5));
    }

    [Fact]
    public void OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        Assert.True(new DocumentOffset(5) != new DocumentOffset(6));
    }

    // --- GetHashCode / CompareTo / ToString ---

    [Fact]
    public void GetHashCode_SameValue_SameHash()
    {
        Assert.Equal(new DocumentOffset(5).GetHashCode(), new DocumentOffset(5).GetHashCode());
    }

    [Fact]
    public void CompareTo_LessThan_ReturnsNegative()
    {
        Assert.True(new DocumentOffset(1).CompareTo(new DocumentOffset(2)) < 0);
    }

    [Fact]
    public void CompareTo_Equal_ReturnsZero()
    {
        Assert.Equal(0, new DocumentOffset(5).CompareTo(new DocumentOffset(5)));
    }

    [Fact]
    public void CompareTo_GreaterThan_ReturnsPositive()
    {
        Assert.True(new DocumentOffset(5).CompareTo(new DocumentOffset(1)) > 0);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        Assert.Equal("42", new DocumentOffset(42).ToString());
    }
}
