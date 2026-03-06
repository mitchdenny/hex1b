using System.Text;

namespace Hex1b;

/// <summary>
/// Helper for constructing KGP escape sequences in tests.
/// </summary>
public static class KgpTestHelper
{
    /// <summary>
    /// Builds a complete KGP escape sequence with base64-encoded payload.
    /// </summary>
    /// <param name="controlData">Comma-separated key=value control data.</param>
    /// <param name="rawPayload">Raw bytes to base64-encode as payload, or null for no payload.</param>
    /// <returns>The complete escape sequence string.</returns>
    public static string BuildCommand(string controlData, byte[]? rawPayload = null)
    {
        var sb = new StringBuilder();
        sb.Append("\x1b_G");
        sb.Append(controlData);
        if (rawPayload is not null && rawPayload.Length > 0)
        {
            sb.Append(';');
            sb.Append(Convert.ToBase64String(rawPayload));
        }
        sb.Append("\x1b\\");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a KGP transmit command for a simple RGB or RGBA image.
    /// </summary>
    public static string BuildTransmitCommand(
        uint imageId,
        uint width,
        uint height,
        KgpFormat format = KgpFormat.Rgba32,
        KgpAction action = KgpAction.Transmit,
        int quiet = 0,
        byte fillByte = 0xFF)
    {
        var bytesPerPixel = format == KgpFormat.Rgb24 ? 3 : 4;
        var data = new byte[width * height * bytesPerPixel];
        Array.Fill(data, fillByte);

        var actionChar = action switch
        {
            KgpAction.Transmit => 't',
            KgpAction.TransmitAndDisplay => 'T',
            KgpAction.Query => 'q',
            _ => 't',
        };

        var ctrl = $"a={actionChar},f={(int)format},s={width},v={height},i={imageId}";
        if (quiet > 0)
            ctrl += $",q={quiet}";
        return BuildCommand(ctrl, data);
    }

    /// <summary>
    /// Builds a KGP transmit+display command.
    /// </summary>
    public static string BuildTransmitAndDisplayCommand(
        uint imageId,
        uint width,
        uint height,
        KgpFormat format = KgpFormat.Rgba32,
        uint displayColumns = 0,
        uint displayRows = 0,
        int zIndex = 0,
        int cursorMovement = 0,
        int quiet = 0,
        byte fillByte = 0xFF)
    {
        var bytesPerPixel = format == KgpFormat.Rgb24 ? 3 : 4;
        var data = new byte[width * height * bytesPerPixel];
        Array.Fill(data, fillByte);

        var ctrl = $"a=T,f={(int)format},s={width},v={height},i={imageId}";
        if (displayColumns > 0) ctrl += $",c={displayColumns}";
        if (displayRows > 0) ctrl += $",r={displayRows}";
        if (zIndex != 0) ctrl += $",z={zIndex}";
        if (cursorMovement != 0) ctrl += $",C={cursorMovement}";
        if (quiet > 0) ctrl += $",q={quiet}";
        return BuildCommand(ctrl, data);
    }

    /// <summary>
    /// Builds a KGP put (display) command for a previously transmitted image.
    /// </summary>
    public static string BuildPutCommand(
        uint imageId,
        uint placementId = 0,
        uint displayColumns = 0,
        uint displayRows = 0,
        int zIndex = 0,
        int cursorMovement = 0)
    {
        var ctrl = $"a=p,i={imageId}";
        if (placementId > 0) ctrl += $",p={placementId}";
        if (displayColumns > 0) ctrl += $",c={displayColumns}";
        if (displayRows > 0) ctrl += $",r={displayRows}";
        if (zIndex != 0) ctrl += $",z={zIndex}";
        if (cursorMovement != 0) ctrl += $",C={cursorMovement}";
        return BuildCommand(ctrl);
    }

    /// <summary>
    /// Builds a KGP delete command.
    /// </summary>
    public static string BuildDeleteCommand(char deleteTarget = 'a', uint imageId = 0, uint placementId = 0)
    {
        var ctrl = $"a=d,d={deleteTarget}";
        if (imageId > 0) ctrl += $",i={imageId}";
        if (placementId > 0) ctrl += $",p={placementId}";
        return BuildCommand(ctrl);
    }

    /// <summary>
    /// Builds a KGP query command.
    /// </summary>
    public static string BuildQueryCommand(
        uint imageId = 0,
        uint width = 1,
        uint height = 1,
        KgpFormat format = KgpFormat.Rgb24)
    {
        var bytesPerPixel = format == KgpFormat.Rgb24 ? 3 : 4;
        var data = new byte[width * height * bytesPerPixel];

        var ctrl = $"a=q,f={(int)format},s={width},v={height},t=d";
        if (imageId > 0) ctrl += $",i={imageId}";
        return BuildCommand(ctrl, data);
    }

    /// <summary>
    /// Builds chunked KGP transmit commands.
    /// </summary>
    /// <param name="imageId">Image ID.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="chunkSize">Max raw bytes per chunk before base64 encoding.</param>
    /// <param name="format">Pixel format.</param>
    /// <param name="fillByte">Byte value to fill pixel data with.</param>
    /// <returns>List of escape sequences (first has full control data, rest have only m key).</returns>
    public static List<string> BuildChunkedTransmitCommands(
        uint imageId,
        uint width,
        uint height,
        int chunkSize = 3072,
        KgpFormat format = KgpFormat.Rgba32,
        byte fillByte = 0xFF)
    {
        var bytesPerPixel = format == KgpFormat.Rgb24 ? 3 : 4;
        var totalData = new byte[width * height * bytesPerPixel];
        Array.Fill(totalData, fillByte);

        var commands = new List<string>();
        var offset = 0;

        while (offset < totalData.Length)
        {
            var remaining = totalData.Length - offset;
            var thisChunk = Math.Min(chunkSize, remaining);
            var chunk = new byte[thisChunk];
            Array.Copy(totalData, offset, chunk, 0, thisChunk);

            var isLast = offset + thisChunk >= totalData.Length;

            if (offset == 0)
            {
                var ctrl = $"a=t,f={(int)format},s={width},v={height},i={imageId},m={(isLast ? 0 : 1)}";
                commands.Add(BuildCommand(ctrl, chunk));
            }
            else
            {
                var ctrl = $"m={(isLast ? 0 : 1)}";
                commands.Add(BuildCommand(ctrl, chunk));
            }

            offset += thisChunk;
        }

        return commands;
    }

    /// <summary>
    /// Creates test pixel data of the specified size.
    /// </summary>
    public static byte[] CreatePixelData(uint width, uint height, KgpFormat format = KgpFormat.Rgba32, byte fillByte = 0xFF)
    {
        var bytesPerPixel = format == KgpFormat.Rgb24 ? 3 : 4;
        var data = new byte[width * height * bytesPerPixel];
        Array.Fill(data, fillByte);
        return data;
    }
}
