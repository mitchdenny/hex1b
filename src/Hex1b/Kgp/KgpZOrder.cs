namespace Hex1b;

/// <summary>
/// Specifies the z-ordering of a KGP image relative to text content.
/// </summary>
public enum KgpZOrder
{
    /// <summary>
    /// Image renders behind text (z &lt; 0). This is the default.
    /// </summary>
    BelowText,
    
    /// <summary>
    /// Image renders on top of text (z &gt; 0).
    /// </summary>
    AboveText
}
