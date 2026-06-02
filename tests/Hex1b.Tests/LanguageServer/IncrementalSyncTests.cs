using System.Text.Json;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

[TestClass]
public class IncrementalSyncTests
{
    [TestMethod]
    public void TextDocumentContentChangeEvent_FullSync_HasNullRange()
    {
        var change = new TextDocumentContentChangeEvent { Text = "full text" };
        Assert.IsNull(change.Range);
        Assert.IsNull(change.RangeLength);
        Assert.AreEqual("full text", change.Text);
    }

    [TestMethod]
    public void TextDocumentContentChangeEvent_IncrementalSync_HasRange()
    {
        var change = new TextDocumentContentChangeEvent
        {
            Range = new LspRange
            {
                Start = new LspPosition { Line = 0, Character = 5 },
                End = new LspPosition { Line = 0, Character = 10 }
            },
            Text = "replacement"
        };

        Assert.IsNotNull(change.Range);
        Assert.AreEqual(0, change.Range.Start.Line);
        Assert.AreEqual(5, change.Range.Start.Character);
        Assert.AreEqual(0, change.Range.End.Line);
        Assert.AreEqual(10, change.Range.End.Character);
        Assert.AreEqual("replacement", change.Text);
    }

    [TestMethod]
    public void TextDocumentContentChangeEvent_Serialization_RoundTrips()
    {
        var change = new TextDocumentContentChangeEvent
        {
            Range = new LspRange
            {
                Start = new LspPosition { Line = 1, Character = 0 },
                End = new LspPosition { Line = 1, Character = 5 }
            },
            RangeLength = 5,
            Text = "hello"
        };

        var json = JsonSerializer.Serialize(change);
        var deserialized = JsonSerializer.Deserialize<TextDocumentContentChangeEvent>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual("hello", deserialized.Text);
        Assert.AreEqual(5, deserialized.RangeLength);
        Assert.IsNotNull(deserialized.Range);
        Assert.AreEqual(1, deserialized.Range.Start.Line);
        Assert.AreEqual(0, deserialized.Range.Start.Character);
        Assert.AreEqual(1, deserialized.Range.End.Line);
        Assert.AreEqual(5, deserialized.Range.End.Character);
    }

    [TestMethod]
    public void TextDocumentContentChangeEvent_FullSync_Serialization_OmitsNullRange()
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        var change = new TextDocumentContentChangeEvent { Text = "full content" };

        var json = JsonSerializer.Serialize(change, options);
        var doc = JsonDocument.Parse(json);

        Assert.IsFalse(doc.RootElement.TryGetProperty("range", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("rangeLength", out _));
        Assert.AreEqual("full content", doc.RootElement.GetProperty("text").GetString());
    }
}
