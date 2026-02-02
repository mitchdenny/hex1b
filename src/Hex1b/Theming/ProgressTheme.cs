namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Progress widgets.
/// </summary>
/// <remarks>
/// <para>
/// ProgressTheme provides customization for both determinate and indeterminate progress bars:
/// </para>
/// <list type="bullet">
/// <item><description>Filled section (determinate mode): The completed portion of the progress bar</description></item>
/// <item><description>Empty section: The uncompleted portion of the progress bar</description></item>
/// <item><description>Indeterminate section: The animated segment in indeterminate mode</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Customize progress bar appearance:</para>
/// <code>
/// var theme = new Hex1bThemeBuilder()
///     .Set(ProgressTheme.FilledCharacter, '▓')
///     .Set(ProgressTheme.EmptyCharacter, '░')
///     .Set(ProgressTheme.FilledForegroundColor, Hex1bColor.Green)
///     .Build();
/// </code>
/// </example>
/// <seealso cref="Widgets.ProgressWidget"/>
public static class ProgressTheme
{
    /// <summary>
    /// The character used to render the filled (completed) portion of a determinate progress bar.
    /// </summary>
    /// <remarks>
    /// Default is '█' (full block). Other common choices include '▓', '▒', '■', or '='.
    /// </remarks>
    public static readonly Hex1bThemeElement<char> FilledCharacter = 
        new($"{nameof(ProgressTheme)}.{nameof(FilledCharacter)}", () => '█');
    
    /// <summary>
    /// The character used for a left-half filled cell.
    /// </summary>
    /// <remarks>
    /// Default is '▌' (left half block). For braille style, use '⡀' (U+2840).
    /// Used at the trailing edge when progress is decreasing (e.g., countdown timer).
    /// </remarks>
    public static readonly Hex1bThemeElement<char> FilledLeftHalfCharacter = 
        new($"{nameof(ProgressTheme)}.{nameof(FilledLeftHalfCharacter)}", () => '▌');
    
    /// <summary>
    /// The character used for a right-half filled cell.
    /// </summary>
    /// <remarks>
    /// Default is '▐' (right half block). For braille style, use '⢀' (U+2880).
    /// Used at the trailing edge when progress is increasing (e.g., download progress).
    /// </remarks>
    public static readonly Hex1bThemeElement<char> FilledRightHalfCharacter = 
        new($"{nameof(ProgressTheme)}.{nameof(FilledRightHalfCharacter)}", () => '▐');
    
    /// <summary>
    /// The character used to render the empty (uncompleted) portion of the progress bar.
    /// </summary>
    /// <remarks>
    /// Default is '░' (light shade). Other common choices include '·', '-', ' ', or '▒'.
    /// </remarks>
    public static readonly Hex1bThemeElement<char> EmptyCharacter = 
        new($"{nameof(ProgressTheme)}.{nameof(EmptyCharacter)}", () => '░');
    
    /// <summary>
    /// Whether to use half-cell precision for smoother progress display.
    /// </summary>
    /// <remarks>
    /// When true, the progress bar uses half-cell characters for sub-cell precision.
    /// Default is false for classic block-style rendering.
    /// </remarks>
    public static readonly Hex1bThemeElement<bool> UseHalfCellPrecision = 
        new($"{nameof(ProgressTheme)}.{nameof(UseHalfCellPrecision)}", () => false);
    
    /// <summary>
    /// The character used for the animated segment in indeterminate mode.
    /// </summary>
    /// <remarks>
    /// Default is '█' (full block). This character moves back and forth across the progress bar.
    /// </remarks>
    public static readonly Hex1bThemeElement<char> IndeterminateCharacter = 
        new($"{nameof(ProgressTheme)}.{nameof(IndeterminateCharacter)}", () => '█');
    
    /// <summary>
    /// The foreground color for the filled portion of a determinate progress bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FilledForegroundColor = 
        new($"{nameof(ProgressTheme)}.{nameof(FilledForegroundColor)}", () => Hex1bColor.Green);
    
    /// <summary>
    /// The background color for the filled portion of a determinate progress bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FilledBackgroundColor = 
        new($"{nameof(ProgressTheme)}.{nameof(FilledBackgroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// The foreground color for the empty (uncompleted) portion of the progress bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> EmptyForegroundColor = 
        new($"{nameof(ProgressTheme)}.{nameof(EmptyForegroundColor)}", () => Hex1bColor.DarkGray);
    
    /// <summary>
    /// The background color for the empty (uncompleted) portion of the progress bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> EmptyBackgroundColor = 
        new($"{nameof(ProgressTheme)}.{nameof(EmptyBackgroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// The foreground color for the animated segment in indeterminate mode.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> IndeterminateForegroundColor = 
        new($"{nameof(ProgressTheme)}.{nameof(IndeterminateForegroundColor)}", () => Hex1bColor.Cyan);
    
    /// <summary>
    /// The background color for the animated segment in indeterminate mode.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> IndeterminateBackgroundColor = 
        new($"{nameof(ProgressTheme)}.{nameof(IndeterminateBackgroundColor)}", () => Hex1bColor.Default);
}
