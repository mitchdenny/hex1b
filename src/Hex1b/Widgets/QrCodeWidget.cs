namespace Hex1b.Widgets;

/// <summary>
/// Displays a QR code in the terminal using block characters.
/// </summary>
/// <param name="Data">The data to encode in the QR code (typically a URL).</param>
/// <param name="QuietZone">
/// The number of module widths to use as a border around the QR code.
/// Defaults to 1. Set to 0 to disable the quiet zone.
/// </param>
/// <remarks>
/// <para>
/// QrCodeWidget renders QR codes using Unicode block characters (█) to create
/// scannable QR codes in the terminal. The primary use case is encoding URLs.
/// </para>
/// <para>
/// The QR code is automatically sized based on the data length and error correction level.
/// QR codes work best when displayed with a monospace font where character width equals height.
/// </para>
/// <para>
/// Error correction level is fixed at Q (Quartile - 25% recovery capability) which provides
/// a good balance between data capacity and error recovery.
/// </para>
/// </remarks>
/// <example>
/// <para>Basic URL QR code:</para>
/// <code>
/// ctx.QrCode("https://github.com/mitchdenny/hex1b")
/// </code>
/// <para>QR code without quiet zone:</para>
/// <code>
/// ctx.QrCode("https://example.com", quietZone: 0)
/// </code>
/// </example>
public sealed record QrCodeWidget(string Data, int QuietZone = 1) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as QrCodeNode ?? new QrCodeNode();
        
        // Mark dirty if properties changed
        if (node.Data != Data || node.QuietZone != QuietZone)
        {
            node.MarkDirty();
        }
        
        node.Data = Data;
        node.QuietZone = QuietZone;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(QrCodeNode);
}
