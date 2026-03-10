using System.Text.Json;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

public class IncrementalSyncTests
{
    [Fact]
    public void TextDocumentContentChangeEvent_FullSync_HasNullRange()
    {
        var change = new TextDocumentContentChangeEvent { Text = "full text" };
        Assert.Null(change.Range);
        Assert.Null(change.RangeLength);
        Assert.Equal("full text", change.Text);
    }

    [Fact]
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

        Assert.NotNull(change.Range);
        Assert.Equal(0, change.Range.Start.Line);
        Assert.Equal(5, change.Range.Start.Character);
        Assert.Equal(0, change.Range.End.Line);
        Assert.Equal(10, change.Range.End.Character);
        Assert.Equal("replacement", change.Text);
    }

    [Fact]
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

        Assert.NotNull(deserialized);
        Assert.Equal("hello", deserialized.Text);
        Assert.Equal(5, deserialized.RangeLength);
        Assert.NotNull(deserialized.Range);
        Assert.Equal(1, deserialized.Range.Start.Line);
        Assert.Equal(0, deserialized.Range.Start.Character);
        Assert.Equal(1, deserialized.Range.End.Line);
        Assert.Equal(5, deserialized.Range.End.Character);
    }

    [Fact]
    public void TextDocumentContentChangeEvent_FullSync_Serialization_OmitsNullRange()
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        var change = new TextDocumentContentChangeEvent { Text = "full content" };

        var json = JsonSerializer.Serialize(change, options);
        var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("range", out _));
        Assert.False(doc.RootElement.TryGetProperty("rangeLength", out _));
        Assert.Equal("full content", doc.RootElement.GetProperty("text").GetString());
    }
}
