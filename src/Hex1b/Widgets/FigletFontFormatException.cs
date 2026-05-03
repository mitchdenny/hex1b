namespace Hex1b.Widgets;

/// <summary>
/// The exception that is thrown when a FIGfont (.flf) file fails to parse, either because
/// the header is malformed or because the FIGcharacter data does not conform to the
/// FIGfont 2.0 specification.
/// </summary>
/// <remarks>
/// <para>
/// This exception is raised by <see cref="FigletFont.LoadAsync(System.IO.Stream, System.Threading.CancellationToken)"/>,
/// <see cref="FigletFont.LoadFileAsync(string, System.Threading.CancellationToken)"/>,
/// <see cref="FigletFont.LoadBundledAsync(string, System.Threading.CancellationToken)"/>,
/// <see cref="FigletFont.LoadBundled(string)"/>, and
/// <see cref="FigletFont.Parse(string)"/> when input is not a recognizable FIGfont.
/// </para>
/// <para>
/// The exception message identifies the offending portion of the font (for example, the
/// header line, a comment line count, or the FIGcharacter at a specific code point) so that
/// font authors can locate problems quickly.
/// </para>
/// </remarks>
public sealed class FigletFontFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FigletFontFormatException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public FigletFontFormatException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FigletFontFormatException"/> class
    /// with a specified error message and a reference to the inner exception that is the
    /// cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FigletFontFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
