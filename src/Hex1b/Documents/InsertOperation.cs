using System.Text.Json.Serialization;

namespace Hex1b.Documents;

/// <summary>
/// Insert text at a given offset.
/// </summary>
public sealed record InsertOperation(DocumentOffset Offset, string Text) : EditOperation
{
    public override EditOperation Invert(string deletedText)
        => new DeleteOperation(new DocumentRange(Offset, Offset + Text.Length));
}
