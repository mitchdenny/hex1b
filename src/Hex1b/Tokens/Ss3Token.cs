namespace Hex1b.Tokens;

/// <summary>
/// Represents an SS3 (Single Shift 3) escape sequence (ESC O followed by a character).
/// SS3 sequences are used for function keys F1-F4 and arrow keys in application keypad mode.
/// </summary>
/// <remarks>
/// Common SS3 sequences:
/// <list type="bullet">
///   <item>ESC O P = F1</item>
///   <item>ESC O Q = F2</item>
///   <item>ESC O R = F3</item>
///   <item>ESC O S = F4</item>
///   <item>ESC O A = Up arrow (application mode)</item>
///   <item>ESC O B = Down arrow (application mode)</item>
///   <item>ESC O C = Right arrow (application mode)</item>
///   <item>ESC O D = Left arrow (application mode)</item>
///   <item>ESC O H = Home (application mode)</item>
///   <item>ESC O F = End (application mode)</item>
/// </list>
/// </remarks>
/// <param name="Character">The character following ESC O.</param>
public sealed record Ss3Token(char Character) : AnsiToken;
