namespace Hex1b;

/// <summary>
/// Describes how a KGP image is scaled to fill its allocated display area.
/// Follows the WPF/UWP <c>Stretch</c> naming convention.
/// </summary>
public enum KgpImageStretch
{
    /// <summary>
    /// The image is scaled to fill the allocated cell dimensions.
    /// Aspect ratio is not preserved — the image may appear distorted.
    /// This is the default, matching the behavior of <c>SizeHint.Fill</c>.
    /// </summary>
    Fill,

    /// <summary>
    /// The image is displayed at its natural pixel-to-cell dimensions
    /// (~10 px/column, ~20 px/row), ignoring the allocated display area.
    /// </summary>
    None,

    /// <summary>
    /// The image is scaled to fit within the allocated display area while
    /// preserving the aspect ratio. The resulting size may be smaller than
    /// the available space in one dimension. Wrap in <see cref="Widgets.AlignWidget"/>
    /// to control positioning of the remaining space.
    /// </summary>
    Uniform,

    /// <summary>
    /// The image is scaled to completely fill the allocated display area while
    /// preserving the aspect ratio. Excess portions of the source image are
    /// cropped using KGP source-rectangle clipping.
    /// </summary>
    UniformToFill,
}
