using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

public class EditOperationTests
{
    [Fact]
    public void InsertOperation_Invert_ReturnsDelete()
    {
        var insert = new InsertOperation(new DocumentOffset(5), "Hello");
        var inverse = insert.Invert("");
        Assert.IsType<DeleteOperation>(inverse);
        var delete = (DeleteOperation)inverse;
        Assert.Equal(new DocumentOffset(5), delete.Range.Start);
        Assert.Equal(new DocumentOffset(10), delete.Range.End);
    }

    [Fact]
    public void DeleteOperation_Invert_ReturnsInsert()
    {
        var delete = new DeleteOperation(new DocumentRange(new DocumentOffset(5), new DocumentOffset(10)));
        var inverse = delete.Invert("Hello");
        Assert.IsType<InsertOperation>(inverse);
        var insert = (InsertOperation)inverse;
        Assert.Equal(new DocumentOffset(5), insert.Offset);
        Assert.Equal("Hello", insert.Text);
    }

    [Fact]
    public void ReplaceOperation_Invert_ReturnsReplace()
    {
        var replace = new ReplaceOperation(
            new DocumentRange(new DocumentOffset(5), new DocumentOffset(10)),
            "World");
        var inverse = replace.Invert("Hello");
        Assert.IsType<ReplaceOperation>(inverse);
        var inv = (ReplaceOperation)inverse;
        Assert.Equal(new DocumentOffset(5), inv.Range.Start);
        Assert.Equal(new DocumentOffset(10), inv.Range.End); // 5 + "World".Length
        Assert.Equal("Hello", inv.NewText);
    }
}
