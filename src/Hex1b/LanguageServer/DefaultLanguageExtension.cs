namespace Hex1b.LanguageServer;

/// <summary>
/// Default language extension that enables all LSP features with no customization.
/// </summary>
internal sealed class DefaultLanguageExtension : ILanguageExtension
{
    /// <summary>Shared singleton instance.</summary>
    public static DefaultLanguageExtension Instance { get; } = new("plaintext");

    /// <inheritdoc />
    public string LanguageId { get; }

    /// <inheritdoc />
    public LspFeatureSet EnabledFeatures => LspFeatureSet.All;

    /// <summary>
    /// Creates a default extension for the specified language.
    /// </summary>
    public DefaultLanguageExtension(string languageId)
    {
        LanguageId = languageId;
    }
}
