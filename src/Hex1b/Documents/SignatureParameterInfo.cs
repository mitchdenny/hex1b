namespace Hex1b.Widgets;

/// <summary>
/// Information about a single parameter in a signature.
/// </summary>
/// <param name="Label">The parameter's display text within the signature.</param>
public record SignatureParameterInfo(string Label)
{
    /// <summary>Optional documentation for this parameter.</summary>
    public string? Documentation { get; init; }
}
