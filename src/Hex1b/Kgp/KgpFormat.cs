namespace Hex1b;

/// <summary>
/// The pixel data format for KGP image transmission.
/// Specified by the 'f' key in the control data.
/// </summary>
public enum KgpFormat
{
    /// <summary>24-bit RGB data, 3 bytes per pixel (f=24).</summary>
    Rgb24 = 24,

    /// <summary>32-bit RGBA data, 4 bytes per pixel (f=32, default).</summary>
    Rgba32 = 32,

    /// <summary>PNG image data (f=100).</summary>
    Png = 100,
}
