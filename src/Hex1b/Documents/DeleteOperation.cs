using System.Text.Json.Serialization;

namespace Hex1b.Documents;

/// <summary>
/// Delete text in a given range.
/// </summary>
public sealed record DeleteOperation(DocumentRange Range) : EditOperation
{
    public override EditOperation Invert(string deletedText)
        => new InsertOperation(Range.Start, deletedText);
}
