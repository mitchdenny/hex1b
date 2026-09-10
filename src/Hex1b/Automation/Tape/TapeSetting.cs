namespace Hex1b.Automation;

/// <summary>Identifies a setting recognized by the VHS v0.11.0 grammar.</summary>
public enum TapeSetting
{
    /// <summary>The shell program.</summary>
    Shell,
    /// <summary>The font family.</summary>
    FontFamily,
    /// <summary>The font size.</summary>
    FontSize,
    /// <summary>The spacing between letters.</summary>
    LetterSpacing,
    /// <summary>The line height.</summary>
    LineHeight,
    /// <summary>The output frame rate.</summary>
    Framerate,
    /// <summary>The default typing delay.</summary>
    TypingSpeed,
    /// <summary>The named or inline theme.</summary>
    Theme,
    /// <summary>The output playback speed.</summary>
    PlaybackSpeed,
    /// <summary>The output height.</summary>
    Height,
    /// <summary>The output width.</summary>
    Width,
    /// <summary>The output padding.</summary>
    Padding,
    /// <summary>The animation loop offset.</summary>
    LoopOffset,
    /// <summary>The margin color or image.</summary>
    MarginFill,
    /// <summary>The output margin.</summary>
    Margin,
    /// <summary>The window decoration style.</summary>
    WindowBar,
    /// <summary>The window decoration size.</summary>
    WindowBarSize,
    /// <summary>The border radius.</summary>
    BorderRadius,
    /// <summary>Whether the cursor blinks.</summary>
    CursorBlink,
    /// <summary>The default wait timeout.</summary>
    WaitTimeout,
    /// <summary>The default wait regular expression.</summary>
    WaitPattern
}
