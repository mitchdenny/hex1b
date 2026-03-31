namespace Hex1b.Widgets;

/// <summary>
/// Describes an adornment to display next to a form field.
/// The adornment's visibility is controlled by an async predicate evaluated against the field value.
/// When the predicate resolves to true, the widget returned by the builder is rendered.
/// </summary>
/// <param name="Predicate">
/// Async function that receives the current field value and a cancellation token.
/// Returns true if the adornment should be visible.
/// </param>
/// <param name="Builder">
/// Factory function that creates the widget to display when the adornment is visible.
/// Called during reconciliation when the predicate has resolved to true.
/// </param>
public sealed record FieldAdornment(
    Func<string, CancellationToken, Task<bool>> Predicate,
    Func<Hex1bWidget> Builder)
{
    /// <summary>
    /// Creates an adornment with a predicate that doesn't need cancellation support.
    /// </summary>
    public static FieldAdornment Create(
        Func<string, Task<bool>> predicate,
        Func<Hex1bWidget> builder)
        => new((value, _) => predicate(value), builder);

    /// <summary>
    /// Creates an adornment with a synchronous predicate (always resolves immediately).
    /// Used internally for validation indicators.
    /// </summary>
    internal static FieldAdornment CreateSync(
        Func<string, bool> predicate,
        Func<Hex1bWidget> builder)
        => new((value, _) => Task.FromResult(predicate(value)), builder);
}
