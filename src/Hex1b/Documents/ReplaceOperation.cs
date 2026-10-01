using System.Text.Json.Serialization;

namespace Hex1b.Documents;

/// <summary>
/// Replace text in a given range with new text.
/// </summary>
public sealed record ReplaceOperation(DocumentRange Range, string NewText) : EditOperation
{
    public override EditOperation Invert(string deletedText)
        => new ReplaceOperation(
            new DocumentRange(Range.Start, Range.Start + NewText.Length),
            deletedText);
}
