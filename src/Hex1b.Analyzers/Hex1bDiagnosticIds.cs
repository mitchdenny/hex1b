namespace Hex1b.Analyzers;

/// <summary>
/// Diagnostic IDs and shared metadata for Hex1b analyzer rules.
/// </summary>
internal static class Hex1bDiagnosticIds
{
    public const string Category = "Hex1b.ApiDesign";

    public const string WidgetMethodNameStartsWithWith = "HEX1B0001";
    public const string WidgetTypeNameMissingSuffix = "HEX1B0002";
    public const string NodeTypeNameMissingSuffix = "HEX1B0003";
    public const string WidgetMustBeRecord = "HEX1B0004";
    public const string NodeMustBeClass = "HEX1B0005";

    /// <summary>
    /// Help link template. Diagnostic-specific suffix is appended.
    /// </summary>
    /// <remarks>
    /// The target docs file does not yet exist. Once docs are written under
    /// docs/analyzers/, the link will resolve. Keeping the URL stable now so
    /// that suppression metadata in downstream PRs does not need to change.
    /// </remarks>
    public const string HelpLinkBase = "https://github.com/mitchdenny/hex1b/blob/main/docs/analyzers/";

    public static string HelpLink(string ruleId) => HelpLinkBase + ruleId + ".md";
}
