using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

[TestClass]
public class DocumentPositionTests
{
    [TestMethod]
    public void Constructor_ValidLineAndColumn_Succeeds()
    {
        var pos = new DocumentPosition(1, 1);
        Assert.AreEqual(1, pos.Line);
        Assert.AreEqual(1, pos.Column);
    }

    [TestMethod]
    public void Constructor_LargeValues_Succeeds()
    {
        var pos = new DocumentPosition(10000, 500);
        Assert.AreEqual(10000, pos.Line);
        Assert.AreEqual(500, pos.Column);
    }

    [TestMethod]
    public void Constructor_ZeroLine_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DocumentPosition(0, 1));
    }

    [TestMethod]
    public void Constructor_NegativeLine_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DocumentPosition(-1, 1));
    }

    [TestMethod]
    public void Constructor_ZeroColumn_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DocumentPosition(1, 0));
    }

    [TestMethod]
    public void Constructor_NegativeColumn_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DocumentPosition(1, -1));
    }

    // --- Comparison ---

    [TestMethod]
    public void CompareTo_SameLine_ComparesByColumn()
    {
        var a = new DocumentPosition(1, 3);
        var b = new DocumentPosition(1, 5);
        Assert.IsTrue(a.CompareTo(b) < 0);
    }

    [TestMethod]
    public void CompareTo_DifferentLines_ComparesByLine()
    {
        var a = new DocumentPosition(1, 100);
        var b = new DocumentPosition(2, 1);
        Assert.IsTrue(a.CompareTo(b) < 0);
    }

    [TestMethod]
    public void CompareTo_Equal_ReturnsZero()
    {
        var a = new DocumentPosition(3, 7);
        Assert.AreEqual(0, a.CompareTo(new DocumentPosition(3, 7)));
    }

    [TestMethod]
    public void LessThan_DifferentLines_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(1, 5) < new DocumentPosition(2, 1));
    }

    [TestMethod]
    public void LessThan_SameLineSmallerCol_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(1, 3) < new DocumentPosition(1, 5));
    }

    [TestMethod]
    public void LessThan_Equal_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentPosition(1, 1) < new DocumentPosition(1, 1));
    }

    [TestMethod]
    public void GreaterThan_DifferentLines_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(3, 1) > new DocumentPosition(2, 99));
    }

    [TestMethod]
    public void GreaterThan_SameLineLargerCol_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(1, 5) > new DocumentPosition(1, 3));
    }

    [TestMethod]
    public void LessThanOrEqual_Equal_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(2, 3) <= new DocumentPosition(2, 3));
    }

    [TestMethod]
    public void GreaterThanOrEqual_Equal_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(2, 3) >= new DocumentPosition(2, 3));
    }

    // --- Equality ---

    [TestMethod]
    public void Equals_SameValues_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(2, 3).Equals(new DocumentPosition(2, 3)));
    }

    [TestMethod]
    public void Equals_DifferentLine_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentPosition(1, 3).Equals(new DocumentPosition(2, 3)));
    }

    [TestMethod]
    public void Equals_DifferentColumn_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentPosition(2, 3).Equals(new DocumentPosition(2, 4)));
    }

    [TestMethod]
    public void Equals_BoxedSameValue_ReturnsTrue()
    {
        object boxed = new DocumentPosition(2, 3);
        Assert.IsTrue(new DocumentPosition(2, 3).Equals(boxed));
    }

    [TestMethod]
    public void Equals_BoxedDifferentType_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentPosition(2, 3).Equals(42));
    }

    [TestMethod]
    public void Equals_Null_ReturnsFalse()
    {
        Assert.IsFalse(new DocumentPosition(2, 3).Equals(null));
    }

    [TestMethod]
    public void OperatorEquals_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(2, 3) == new DocumentPosition(2, 3));
    }

    [TestMethod]
    public void OperatorNotEquals_ReturnsTrue()
    {
        Assert.IsTrue(new DocumentPosition(2, 3) != new DocumentPosition(2, 4));
    }

    // --- GetHashCode / ToString ---

    [TestMethod]
    public void GetHashCode_SameValues_SameHash()
    {
        Assert.AreEqual(new DocumentPosition(2, 3).GetHashCode(), new DocumentPosition(2, 3).GetHashCode());
    }

    [TestMethod]
    public void ToString_FormatsCorrectly()
    {
        Assert.AreEqual("(2,3)", new DocumentPosition(2, 3).ToString());
    }
}
