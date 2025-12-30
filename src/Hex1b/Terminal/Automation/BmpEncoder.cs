namespace Hex1b.Terminal.Automation;

/// <summary>
/// Encodes raw pixel data to BMP format for embedding in SVG as data URIs.
/// </summary>
/// <remarks>
/// <para>
/// BMP is used because it's universally supported by browsers and requires
/// no compression libraries to generate. The format is simple: a fixed header
/// followed by raw pixel data.
/// </para>
/// <para>
/// This encoder produces 24-bit RGB BMP files (no alpha channel in output,
/// but transparent pixels are rendered as a background color).
/// </para>
/// </remarks>
public static class BmpEncoder
{
    /// <summary>
    /// The background color used for transparent pixels.
    /// </summary>
    private static readonly (byte R, byte G, byte B) TransparentBackground = (0x1e, 0x1e, 0x1e);

    /// <summary>
    /// Encodes RGBA pixel data to a base64 BMP data URI.
    /// </summary>
    /// <param name="image">The decoded sixel image.</param>
    /// <returns>A data URI containing the BMP image.</returns>
    public static string ToDataUri(SixelImage image)
    {
        var bmpData = EncodeToBmp(image.Width, image.Height, image.Pixels);
        var base64 = Convert.ToBase64String(bmpData);
        return $"data:image/bmp;base64,{base64}";
    }

    /// <summary>
    /// Encodes RGBA pixel data to raw BMP bytes.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="rgbaPixels">RGBA pixel data (4 bytes per pixel).</param>
    /// <returns>Complete BMP file as byte array.</returns>
    public static byte[] EncodeToBmp(int width, int height, byte[] rgbaPixels)
    {
        // BMP row stride must be a multiple of 4 bytes
        var rowStride = (width * 3 + 3) & ~3;
        var pixelDataSize = rowStride * height;
        
        // Total file size: 14 (file header) + 40 (DIB header) + pixel data
        var fileSize = 14 + 40 + pixelDataSize;
        var bmp = new byte[fileSize];
        
        // BMP File Header (14 bytes)
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        WriteInt32LE(bmp, 2, fileSize);      // File size
        WriteInt32LE(bmp, 6, 0);             // Reserved
        WriteInt32LE(bmp, 10, 54);           // Pixel data offset (14 + 40)
        
        // DIB Header - BITMAPINFOHEADER (40 bytes)
        WriteInt32LE(bmp, 14, 40);           // DIB header size
        WriteInt32LE(bmp, 18, width);        // Width
        WriteInt32LE(bmp, 22, height);       // Height (positive = bottom-up)
        WriteInt16LE(bmp, 26, 1);            // Planes (must be 1)
        WriteInt16LE(bmp, 28, 24);           // Bits per pixel (24-bit RGB)
        WriteInt32LE(bmp, 30, 0);            // Compression (0 = none)
        WriteInt32LE(bmp, 34, pixelDataSize); // Image size
        WriteInt32LE(bmp, 38, 2835);         // Horizontal resolution (72 DPI)
        WriteInt32LE(bmp, 42, 2835);         // Vertical resolution (72 DPI)
        WriteInt32LE(bmp, 46, 0);            // Colors in palette
        WriteInt32LE(bmp, 50, 0);            // Important colors
        
        // Pixel data (bottom-up, BGR order)
        var pixelOffset = 54;
        for (int y = height - 1; y >= 0; y--)
        {
            var rowOffset = pixelOffset + (height - 1 - y) * rowStride;
            for (int x = 0; x < width; x++)
            {
                var srcIndex = (y * width + x) * 4;
                var dstIndex = rowOffset + x * 3;
                
                // Read RGBA
                var r = rgbaPixels[srcIndex];
                var g = rgbaPixels[srcIndex + 1];
                var b = rgbaPixels[srcIndex + 2];
                var a = rgbaPixels[srcIndex + 3];
                
                // Alpha blend with background if partially transparent
                if (a < 255)
                {
                    var alpha = a / 255.0;
                    r = (byte)(r * alpha + TransparentBackground.R * (1 - alpha));
                    g = (byte)(g * alpha + TransparentBackground.G * (1 - alpha));
                    b = (byte)(b * alpha + TransparentBackground.B * (1 - alpha));
                }
                
                // Write BGR (BMP uses BGR order)
                bmp[dstIndex] = b;
                bmp[dstIndex + 1] = g;
                bmp[dstIndex + 2] = r;
            }
            
            // Padding bytes are already zero from array initialization
        }
        
        return bmp;
    }

    private static void WriteInt16LE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteInt32LE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
