namespace Hex1b.Widgets;

/// <summary>
/// Displays function signature information with the active parameter highlighted.
/// Used for signature help triggered by typing trigger characters (e.g., '(' or ',').
/// </summary>
/// <param name="Signatures">Available function signatures.</param>
public record SignaturePanel(IReadOnlyList<SignaturePanelEntry> Signatures)
{
    /// <summary>Index of the active signature (0-based).</summary>
    public int ActiveSignature { get; init; }

    /// <summary>Index of the active parameter within the active signature (0-based).</summary>
    public int ActiveParameter { get; init; }
}

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

/// <summary>
/// Information about a single parameter in a signature.
/// </summary>
/// <param name="Label">The parameter's display text within the signature.</param>
public record SignatureParameterInfo(string Label)
{
    /// <summary>Optional documentation for this parameter.</summary>
    public string? Documentation { get; init; }
}
