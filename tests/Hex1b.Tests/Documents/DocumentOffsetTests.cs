using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

[TestClass]
public class DocumentOffsetTests
{
    [TestMethod]
    public void Constructor_Zero_Succeeds()
    {
        var offset = new DocumentOffset(0);
        Assert.AreEqual(0, offset.Value);
    }

    [TestMethod]
    public void Constructor_Positive_Succeeds()
    {
        var offset = new DocumentOffset(42);
        Assert.AreEqual(42, offset.Value);
    }

    [TestMethod]
    public void Constructor_Negative_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DocumentOffset(-1));
    }

    [TestMethod]
    public void Zero_ReturnsOffsetWithValueZero()
    {
        Assert.AreEqual(0, DocumentOffset.Zero.Value);
    }

    // --- Implicit/Explicit Conversion ---

    [TestMethod]
    public void ImplicitToInt_ReturnsValue()
    {
        var offset = new DocumentOffset(7);
        int val = offset;
        Assert.AreEqual(7, val);
    }

    [TestMethod]
    public void ExplicitFromInt_CreatesOffset()
    {
        var offset = (DocumentOffset)5;
        Assert.AreEqual(5, offset.Value);
    }

    [TestMethod]
    public void ExplicitFromInt_Negative_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => (DocumentOffset)(-3));
    }

    // --- Arithmetic Operators ---

    [TestMethod]
    public void Add_Int_ReturnsNewOffset()
    {
        var offset = new DocumentOffset(5);
        var result = offset + 3;
        Assert.AreEqual(8, result.Value);
    }

    [TestMethod]
    public void Add_Zero_ReturnsSame()
    {
        var offset = new DocumentOffset(5);
        var result = offset + 0;
        Assert.AreEqual(5, result.Value);
    }

    [TestMethod]
    public void SubtractInt_ReturnsNewOffset()
    {
        var offset = new DocumentOffset(5);
        var result = offset - 3;
        Assert.AreEqual(2, result.Value);
    }

    [TestMethod]
    public void SubtractInt_ToZero_ReturnsZero()
    {
        var offset = new DocumentOffset(5);
        var result = offset - 5;
        Assert.AreEqual(0, result.Value);
    }

    [TestMethod]
    public void SubtractInt_BelowZero_Throws()
    {
        var offset = new DocumentOffset(2);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => offset - 3);
    }

    [TestMethod]
    public void SubtractOffset_ReturnsDifference()
    {
        var a = new DocumentOffset(10);
        var b = new DocumentOffset(3);
        Assert.AreEqual(7, a - b);
    }

    [TestMethod]
    public void SubtractOffset_SameValue_ReturnsZero()
    {
        var a = new DocumentOffset(5);
        Assert.AreEqual(0, a - a);
    }

    [TestMethod]
    public void SubtractOffset_Negative_Allowed()
    {
        // Offset subtraction returns int, which can be negative
        var a = new DocumentOffset(3);
        var b = new DocumentOffset(10);
        Assert.AreEqual(-7, a - b);
    }

    // --- Comparison Operators ---

    [TestMethod]
    public void LessThan_WhenSmaller_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(1) < new DocumentOffset(2));
    }

    [TestMethod]
    public void LessThan_WhenEqual_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(2) < new DocumentOffset(2));
    }

    [TestMethod]
    public void LessThan_WhenGreater_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(3) < new DocumentOffset(2));
    }

    [TestMethod]
    public void GreaterThan_WhenGreater_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(3) > new DocumentOffset(2));
    }

    [TestMethod]
    public void GreaterThan_WhenEqual_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(2) > new DocumentOffset(2));
    }

    [TestMethod]
    public void LessThanOrEqual_WhenEqual_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(2) <= new DocumentOffset(2));
    }

    [TestMethod]
    public void LessThanOrEqual_WhenSmaller_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(1) <= new DocumentOffset(2));
    }

    [TestMethod]
    public void LessThanOrEqual_WhenGreater_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(3) <= new DocumentOffset(2));
    }

    [TestMethod]
    public void GreaterThanOrEqual_WhenEqual_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(2) >= new DocumentOffset(2));
    }

    [TestMethod]
    public void GreaterThanOrEqual_WhenGreater_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(3) >= new DocumentOffset(2));
    }

    [TestMethod]
    public void GreaterThanOrEqual_WhenSmaller_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(1) >= new DocumentOffset(2));
    }

    // --- Equality ---

    [TestMethod]
    public void Equals_SameValue_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(5).Equals(new DocumentOffset(5)));
    }

    [TestMethod]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(5).Equals(new DocumentOffset(6)));
    }

    [TestMethod]
    public void Equals_BoxedSameValue_ReturnsTrue()
    {
        object boxed = new DocumentOffset(5);
        Assert.IsTrue(new DocumentOffset(5).Equals(boxed));
    }

    [TestMethod]
    public void Equals_BoxedDifferentType_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(5).Equals("not an offset"));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentOffset(5).Equals(null));
    }

    [TestMethod]
    public void OperatorEquals_SameValue_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(5) == new DocumentOffset(5));
    }

    [TestMethod]
    public void OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentOffset(5) != new DocumentOffset(6));
    }

    // --- GetHashCode / CompareTo / ToString ---

    [TestMethod]
    public void GetHashCode_SameValue_SameHash()
    {
        Assert.AreEqual(new DocumentOffset(5).GetHashCode(), new DocumentOffset(5).GetHashCode());
    }

    [TestMethod]
    public void CompareTo_LessThan_ReturnsNegative()
    {
        Assert.IsTrue(new DocumentOffset(1).CompareTo(new DocumentOffset(2)) < 0);
    }

    [TestMethod]
    public void CompareTo_Equal_ReturnsZero()
    {
        Assert.AreEqual(0, new DocumentOffset(5).CompareTo(new DocumentOffset(5)));
    }

    [TestMethod]
    public void CompareTo_GreaterThan_ReturnsPositive()
    {
        Assert.IsTrue(new DocumentOffset(5).CompareTo(new DocumentOffset(1)) > 0);
    }

    [TestMethod]
    public void ToString_ReturnsValue()
    {
        Assert.AreEqual("42", new DocumentOffset(42).ToString());
    }
}
