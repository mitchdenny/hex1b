namespace Hex1b.Theming;

/// <summary>
/// Theme elements for syntax highlighting. These map to common semantic token types
/// used by language servers and syntax highlighters.
/// </summary>
public static class SyntaxTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> KeywordColor =
        new($"{nameof(SyntaxTheme)}.{nameof(KeywordColor)}", () => Hex1bColor.FromRgb(86, 156, 214));

    public static readonly Hex1bThemeElement<Hex1bColor> StringColor =
        new($"{nameof(SyntaxTheme)}.{nameof(StringColor)}", () => Hex1bColor.FromRgb(206, 145, 120));

    public static readonly Hex1bThemeElement<Hex1bColor> CommentColor =
        new($"{nameof(SyntaxTheme)}.{nameof(CommentColor)}", () => Hex1bColor.FromRgb(106, 153, 85));

    public static readonly Hex1bThemeElement<Hex1bColor> NumberColor =
        new($"{nameof(SyntaxTheme)}.{nameof(NumberColor)}", () => Hex1bColor.FromRgb(181, 206, 168));

    public static readonly Hex1bThemeElement<Hex1bColor> TypeColor =
        new($"{nameof(SyntaxTheme)}.{nameof(TypeColor)}", () => Hex1bColor.FromRgb(78, 201, 176));

    public static readonly Hex1bThemeElement<Hex1bColor> FunctionColor =
        new($"{nameof(SyntaxTheme)}.{nameof(FunctionColor)}", () => Hex1bColor.FromRgb(220, 220, 170));

    public static readonly Hex1bThemeElement<Hex1bColor> VariableColor =
        new($"{nameof(SyntaxTheme)}.{nameof(VariableColor)}", () => Hex1bColor.FromRgb(156, 220, 254));

    public static readonly Hex1bThemeElement<Hex1bColor> OperatorColor =
        new($"{nameof(SyntaxTheme)}.{nameof(OperatorColor)}", () => Hex1bColor.Default);

    public static readonly Hex1bThemeElement<Hex1bColor> PropertyColor =
        new($"{nameof(SyntaxTheme)}.{nameof(PropertyColor)}", () => Hex1bColor.FromRgb(156, 220, 254));

    public static readonly Hex1bThemeElement<Hex1bColor> NamespaceColor =
        new($"{nameof(SyntaxTheme)}.{nameof(NamespaceColor)}", () => Hex1bColor.FromRgb(78, 201, 176));

    public static readonly Hex1bThemeElement<Hex1bColor> EnumMemberColor =
        new($"{nameof(SyntaxTheme)}.{nameof(EnumMemberColor)}", () => Hex1bColor.FromRgb(79, 193, 255));

    public static readonly Hex1bThemeElement<Hex1bColor> ParameterColor =
        new($"{nameof(SyntaxTheme)}.{nameof(ParameterColor)}", () => Hex1bColor.FromRgb(156, 220, 254));

    public static readonly Hex1bThemeElement<Hex1bColor> PreprocessorColor =
        new($"{nameof(SyntaxTheme)}.{nameof(PreprocessorColor)}", () => Hex1bColor.FromRgb(155, 155, 155));
}
