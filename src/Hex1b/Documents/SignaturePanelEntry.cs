namespace Hex1b.Widgets;

/// <summary>
/// A single function signature in the <see cref="SignaturePanel"/>.
/// </summary>
/// <param name="Label">The full signature text (e.g., "void Foo(int x, string y)").</param>
/// <param name="Parameters">Parameter information for highlighting.</param>
public record SignaturePanelEntry(
    string Label,
    IReadOnlyList<SignatureParameterInfo> Parameters)
{
    /// <summary>Optional documentation for this signature.</summary>
    public string? Documentation { get; init; }
}
