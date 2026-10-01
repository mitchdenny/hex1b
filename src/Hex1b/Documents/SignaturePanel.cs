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
