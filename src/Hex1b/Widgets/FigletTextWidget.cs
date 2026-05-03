using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Displays large ASCII-art ("FIGlet") text rendered from a <see cref="FigletFont"/>.
/// </summary>
/// <param name="Text">The text to render. May contain newline characters which force vertical breaks.</param>
/// <remarks>
/// <para>
/// FIGlet text is composed of glyphs whose height is determined by the font. The widget honors
/// the font's preferred horizontal and vertical layout modes by default but the caller can
/// override either axis via <see cref="FigletTextExtensions.Horizontal(FigletTextWidget, FigletLayoutMode)"/>,
/// <see cref="FigletTextExtensions.Vertical(FigletTextWidget, FigletLayoutMode)"/>, or
/// <see cref="FigletTextExtensions.Layout(FigletTextWidget, FigletLayoutMode)"/>.
/// </para>
/// <para>
/// To pick a font, use one of the bundled instances on <see cref="FigletFonts"/> (for example
/// <c>FigletFonts.Slant</c>) or load a custom <c>.flf</c> file via
/// <see cref="FigletFont.LoadFileAsync(string, System.Threading.CancellationToken)"/>.
/// </para>
/// <para>
/// FIGlet text is rendered monochrome — apply colors and animations by wrapping the widget in an
/// <c>EffectPanel</c>, which colorizes surface cells without affecting layout.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Hex1b;
/// using Hex1b.Widgets;
///
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithHex1bApp((app, options) =&gt; ctx =&gt;
///         ctx.FigletText("Hello").Font(FigletFonts.Slant))
///     .Build();
///
/// await terminal.RunAsync();
/// </code>
/// </example>
public sealed record FigletTextWidget(string Text) : Hex1bWidget
{
    /// <summary>The font used to render <see cref="Text"/>. Defaults to <see cref="FigletFonts.Standard"/>.</summary>
    public FigletFont Font { get; init; } = FigletFonts.Standard;

    /// <summary>Horizontal layout mode. <see cref="FigletLayoutMode.Default"/> defers to the font's preference.</summary>
    public FigletLayoutMode HorizontalLayout { get; init; } = FigletLayoutMode.Default;

    /// <summary>Vertical layout mode. <see cref="FigletLayoutMode.Default"/> defers to the font's preference.</summary>
    public FigletLayoutMode VerticalLayout { get; init; } = FigletLayoutMode.Default;

    /// <summary>How to handle horizontal overflow. Defaults to <see cref="FigletHorizontalOverflow.Clip"/>.</summary>
    public FigletHorizontalOverflow HorizontalOverflow { get; init; } = FigletHorizontalOverflow.Clip;

    /// <summary>How to handle vertical overflow. Defaults to <see cref="FigletVerticalOverflow.Clip"/>.</summary>
    public FigletVerticalOverflow VerticalOverflow { get; init; } = FigletVerticalOverflow.Clip;

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as FigletTextNode ?? new FigletTextNode();

        if (node.Text != Text
            || !ReferenceEquals(node.Font, Font)
            || node.HorizontalLayout != HorizontalLayout
            || node.VerticalLayout != VerticalLayout
            || node.HorizontalOverflow != HorizontalOverflow
            || node.VerticalOverflow != VerticalOverflow)
        {
            node.MarkDirty();
        }

        node.Text = Text;
        node.Font = Font;
        node.HorizontalLayout = HorizontalLayout;
        node.VerticalLayout = VerticalLayout;
        node.HorizontalOverflow = HorizontalOverflow;
        node.VerticalOverflow = VerticalOverflow;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(FigletTextNode);
}
