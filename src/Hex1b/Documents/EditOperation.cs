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
