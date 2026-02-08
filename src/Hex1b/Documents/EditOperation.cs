using System.Text.Json.Serialization;

namespace Hex1b.Documents;

/// <summary>
/// Base type for document edit operations. Sealed hierarchy for serialization.
/// </summary>
[JsonDerivedType(typeof(InsertOperation), "insert")]
[JsonDerivedType(typeof(DeleteOperation), "delete")]
[JsonDerivedType(typeof(ReplaceOperation), "replace")]
public abstract record EditOperation
{
    /// <summary>
    /// Returns the inverse operation that undoes this edit.
    /// </summary>
    /// <param name="deletedText">The text that was removed by this operation (needed for inverse of delete/replace).</param>
    public abstract EditOperation Invert(string deletedText);
}

/// <summary>
/// Insert text at a given offset.
/// </summary>
public sealed record InsertOperation(DocumentOffset Offset, string Text) : EditOperation
{
    public override EditOperation Invert(string deletedText)
        => new DeleteOperation(new DocumentRange(Offset, Offset + Text.Length));
}

/// <summary>
/// Delete text in a given range.
/// </summary>
public sealed record DeleteOperation(DocumentRange Range) : EditOperation
{
    public override EditOperation Invert(string deletedText)
        => new InsertOperation(Range.Start, deletedText);
}

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
