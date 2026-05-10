namespace Hex1b.Theming;

/// <summary>
/// Theme elements for TextBox widgets.
/// </summary>
/// <remarks>
/// As of the prediction-input refresh, the TextBox no longer renders the
/// classic <c>[…]</c> bookends. The fill/background-based rendering that
/// used to be opt-in via <c>UseFillMode</c> is now the only style. If you
/// need a bracketed look, render brackets in surrounding widgets.
/// </remarks>
public static class TextBoxTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(CursorForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(CursorBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(SelectionForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(SelectionBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Background color used to delineate the text area when the textbox is unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FillBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(FillBackgroundColor)}", () => Hex1bColor.FromRgb(40, 40, 40));

    /// <summary>
    /// Background color used when the text box has focus.
    /// Slightly lighter than <see cref="FillBackgroundColor"/> to indicate active input.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedFillBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(FocusedFillBackgroundColor)}", () => Hex1bColor.FromRgb(55, 55, 55));

    /// <summary>
    /// Foreground color of the inline prediction (suggestion) text rendered to
    /// the right of the cursor. Defaults to <see cref="Hex1bColor.Gray"/> — a
    /// monochrome mid-gray that stays clearly visible against typical field
    /// backgrounds while remaining distinct from regular typed text.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> PredictionForegroundColor =
        new($"{nameof(TextBoxTheme)}.{nameof(PredictionForegroundColor)}", () => Hex1bColor.Gray);

    /// <summary>
    /// Background color of the inline prediction text. Defaults to
    /// <see cref="Hex1bColor.Default"/>, which the renderer treats as
    /// "follow the textbox field fill background" so the suggestion blends
    /// into the input surface. Set to any concrete color to draw the
    /// prediction on a contrasting band instead.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> PredictionBackgroundColor =
        new($"{nameof(TextBoxTheme)}.{nameof(PredictionBackgroundColor)}", () => Hex1bColor.Default);
}
