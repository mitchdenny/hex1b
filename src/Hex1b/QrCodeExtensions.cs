namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating and configuring <see cref="QrCodeWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable concise QR code widget creation within widget builder callbacks.
/// The primary use case is encoding URLs for display in terminal applications.
/// </para>
/// </remarks>
/// <example>
/// <para>Using QrCode within a VStack:</para>
/// <code>
/// ctx.VStack(v => [
///     v.Text("Scan to visit:"),
///     v.QrCode("https://github.com/mitchdenny/hex1b"),
///     v.Text("GitHub Repository")
/// ])
/// </code>
/// </example>
/// <seealso cref="QrCodeWidget"/>
public static class QrCodeExtensions
{
    /// <summary>
    /// Creates a <see cref="QrCodeWidget"/> with the specified data to encode.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The data to encode in the QR code (typically a URL).</param>
    /// <returns>A new <see cref="QrCodeWidget"/> with a default quiet zone of 1.</returns>
    /// <example>
    /// <code>
    /// ctx.QrCode("https://example.com")
    /// </code>
    /// </example>
    public static QrCodeWidget QrCode<TParent>(
        this WidgetContext<TParent> ctx,
        string data)
        where TParent : Hex1bWidget
        => new(data);

    /// <summary>
    /// Sets the quiet zone (border) size for the QR code.
    /// </summary>
    /// <param name="widget">The QR code widget to configure.</param>
    /// <param name="quietZone">The number of module widths to use as a border. Use 0 to disable.</param>
    /// <returns>A new <see cref="QrCodeWidget"/> with the specified quiet zone.</returns>
    /// <remarks>
    /// The quiet zone is the white space border around the QR code. QR code standards
    /// recommend a quiet zone of at least 4 modules, but smaller values work in controlled
    /// environments like terminal displays.
    /// </remarks>
    /// <example>
    /// <code>
    /// v.QrCode("https://example.com").WithQuietZone(0)
    /// </code>
    /// </example>
    public static QrCodeWidget WithQuietZone(this QrCodeWidget widget, int quietZone)
        => widget with { QuietZone = quietZone };
}
