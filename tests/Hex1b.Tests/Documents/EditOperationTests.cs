using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

[TestClass]
public class EditOperationTests
{
    [TestMethod]
    public void InsertOperation_Invert_ReturnsDelete()
    {
        var insert = new InsertOperation(new DocumentOffset(5), "Hello");
        var inverse = insert.Invert("");
        TestSeq.IsType<DeleteOperation>(inverse);
        var delete = (DeleteOperation)inverse;
        Assert.AreEqual(new DocumentOffset(5), delete.Range.Start);
        Assert.AreEqual(new DocumentOffset(10), delete.Range.End);
    }

    [TestMethod]
    public void DeleteOperation_Invert_ReturnsInsert()
    {
        var delete = new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(10)));
        var inverse = delete.Invert("Hello");
        TestSeq.IsType<InsertOperation>(inverse);
        var insert = (InsertOperation)inverse;
        Assert.AreEqual(new DocumentOffset(5), insert.Offset);
        Assert.AreEqual("Hello", insert.Text);
    }

    [TestMethod]
    public void ReplaceOperation_Invert_ReturnsReplace()
    {
        var replace = new ReplaceOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(10)),
            "World");
        var inverse = replace.Invert("Hello");
        TestSeq.IsType<ReplaceOperation>(inverse);
        var inv = (ReplaceOperation)inverse;
        Assert.AreEqual(new DocumentOffset(5), inv.Range.Start);
        Assert.AreEqual(new DocumentOffset(10), inv.Range.End); // 5 + "World".Length
        Assert.AreEqual("Hello", inv.NewText);
    }
}
