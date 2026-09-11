namespace Hex1b.Automation;

/// <summary>Identifies a token's lexical category without converting its value.</summary>
public enum TapeTokenKind
{
    /// <summary>The end of input.</summary>
    EndOfFile,
    /// <summary>An unrecognized character.</summary>
    Illegal,
    /// <summary>A quoted string or non-reserved bare word.</summary>
    String,
    /// <summary>A sequence of digits and decimal points, not necessarily a valid number.</summary>
    Number,
    /// <summary>A reserved command keyword.</summary>
    Keyword,
    /// <summary>A reserved setting name.</summary>
    Setting,
    /// <summary>A reserved unit: ms, s, m, px, or em.</summary>
    Unit,
    /// <summary>A bare true or false value.</summary>
    Boolean,
    /// <summary>A slash-delimited regular expression.</summary>
    Regex,
    /// <summary>A brace-delimited theme value; JSON validity is not checked.</summary>
    Json,
    /// <summary>A hash-prefixed comment.</summary>
    Comment,
    /// <summary>An at sign.</summary>
    At,
    /// <summary>A plus sign.</summary>
    Plus,
    /// <summary>A minus sign.</summary>
    Minus,
    /// <summary>An equals sign.</summary>
    Equal,
    /// <summary>A percent sign.</summary>
    Percent,
    /// <summary>A left square bracket.</summary>
    LeftBracket,
    /// <summary>A right square bracket.</summary>
    RightBracket,
    /// <summary>A caret.</summary>
    Caret,
    /// <summary>A backslash.</summary>
    Backslash
}
