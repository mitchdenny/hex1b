using Hex1b.Theming;

namespace Hex1b.Markdown;

/// <summary>
/// Theme elements for the MarkdownWidget. Controls colors and characters
/// used by the default block renderers.
/// </summary>
public static class MarkdownTheme
{
    // --- Headings ---

    public static readonly Hex1bThemeElement<Hex1bColor> H1ForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(H1ForegroundColor)}", () => Hex1bColor.FromRgb(100, 200, 255));

    public static readonly Hex1bThemeElement<Hex1bColor> H2ForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(H2ForegroundColor)}", () => Hex1bColor.FromRgb(130, 210, 240));

    public static readonly Hex1bThemeElement<Hex1bColor> H3ForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(H3ForegroundColor)}", () => Hex1bColor.FromRgb(160, 200, 220));

    public static readonly Hex1bThemeElement<Hex1bColor> H4ForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(H4ForegroundColor)}", () => Hex1bColor.FromRgb(180, 190, 210));

    public static readonly Hex1bThemeElement<Hex1bColor> H5ForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(H5ForegroundColor)}", () => Hex1bColor.FromRgb(180, 180, 200));

    public static readonly Hex1bThemeElement<Hex1bColor> H6ForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(H6ForegroundColor)}", () => Hex1bColor.FromRgb(160, 160, 180));

    // --- Code blocks ---

    public static readonly Hex1bThemeElement<Hex1bColor> CodeBlockForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(CodeBlockForegroundColor)}", () => Hex1bColor.FromRgb(200, 200, 200));

    public static readonly Hex1bThemeElement<Hex1bColor> CodeBlockBackgroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(CodeBlockBackgroundColor)}", () => Hex1bColor.FromRgb(40, 40, 40));

    public static readonly Hex1bThemeElement<Hex1bColor> InlineCodeForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(InlineCodeForegroundColor)}", () => Hex1bColor.FromRgb(220, 170, 120));

    public static readonly Hex1bThemeElement<Hex1bColor> InlineCodeBackgroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(InlineCodeBackgroundColor)}", () => Hex1bColor.FromRgb(50, 50, 50));

    // --- Block quotes ---

    public static readonly Hex1bThemeElement<char> BlockQuoteBorderChar =
        new($"{nameof(MarkdownTheme)}.{nameof(BlockQuoteBorderChar)}", () => '│');

    public static readonly Hex1bThemeElement<Hex1bColor> BlockQuoteBorderColor =
        new($"{nameof(MarkdownTheme)}.{nameof(BlockQuoteBorderColor)}", () => Hex1bColor.FromRgb(100, 100, 120));

    public static readonly Hex1bThemeElement<Hex1bColor> BlockQuoteForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(BlockQuoteForegroundColor)}", () => Hex1bColor.FromRgb(160, 160, 170));

    // --- Lists ---

    public static readonly Hex1bThemeElement<char> UnorderedListBullet =
        new($"{nameof(MarkdownTheme)}.{nameof(UnorderedListBullet)}", () => '•');

    public static readonly Hex1bThemeElement<Hex1bColor> ListBulletColor =
        new($"{nameof(MarkdownTheme)}.{nameof(ListBulletColor)}", () => Hex1bColor.FromRgb(120, 160, 200));

    // --- Thematic break ---

    public static readonly Hex1bThemeElement<char> ThematicBreakChar =
        new($"{nameof(MarkdownTheme)}.{nameof(ThematicBreakChar)}", () => '─');

    public static readonly Hex1bThemeElement<Hex1bColor> ThematicBreakColor =
        new($"{nameof(MarkdownTheme)}.{nameof(ThematicBreakColor)}", () => Hex1bColor.FromRgb(80, 80, 100));

    // --- Links ---

    public static readonly Hex1bThemeElement<Hex1bColor> LinkForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(LinkForegroundColor)}", () => Hex1bColor.FromRgb(100, 160, 255));

    // --- Focus highlight ---

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedLinkForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(FocusedLinkForegroundColor)}", () => Hex1bColor.FromRgb(0, 0, 0));

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedLinkBackgroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(FocusedLinkBackgroundColor)}", () => Hex1bColor.FromRgb(100, 160, 255));

    // --- Emphasis ---

    public static readonly Hex1bThemeElement<Hex1bColor> BoldForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(BoldForegroundColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> ItalicForegroundColor =
        new($"{nameof(MarkdownTheme)}.{nameof(ItalicForegroundColor)}", () => Hex1bColor.Default);
}
