using System.Text;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Spectre.Tui;
using Color = Spectre.Console.Color;
using Decoration = Spectre.Console.Decoration;

namespace Hex1b.Integrations.Spectre.SpectreTui;

/// <summary>
/// Copies cells from a Hex1b <see cref="Surface"/> into a Spectre.Tui
/// <see cref="RenderContext"/>, translating colors and styling attributes
/// between the two type systems.
/// </summary>
/// <remarks>
/// <para>
/// Hex1b stores per-cell foreground / background as <see cref="Hex1bColor"/>
/// (kind + RGB resolution + ANSI index hint) while Spectre.Tui's
/// <see cref="Cell"/> uses <see cref="global::Spectre.Console.Color"/> (RGB triples
/// or named palette entries). The mapping always round-trips via RGB
/// because <see cref="Hex1bColor"/> stores resolved RGB even for indexed
/// values, which keeps the bridge simple at the cost of dropping any
/// downstream "this was originally indexed" hints.
/// </para>
/// <para>
/// Wide-character continuation cells (display width zero) are skipped so
/// the leading wide cell's symbol covers both columns. Multi-codepoint
/// graphemes (emoji, combining marks) are written via the publicly-writable
/// <see cref="Cell.Symbol"/> property; single-rune ASCII / BMP characters
/// take the fast path through <see cref="Cell.SetSymbol(System.Text.Rune)"/>.
/// </para>
/// </remarks>
internal static class SpectreTuiCellWriter
{
    /// <summary>
    /// Copies <paramref name="surface"/> into the supplied
    /// <paramref name="context"/>'s viewport, clipping to the smaller of
    /// the two dimensions.
    /// </summary>
    public static void Copy(Surface surface, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(context);

        var viewport = context.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var copyWidth = Math.Min(surface.Width, viewport.Width);
        var copyHeight = Math.Min(surface.Height, viewport.Height);

        for (var y = 0; y < copyHeight; y++)
        {
            for (var x = 0; x < copyWidth; x++)
            {
                var src = surface[x, y];
                if (src.IsContinuation)
                {
                    continue;
                }

                var cell = context.GetCell(viewport.X + x, viewport.Y + y);
                if (cell is null)
                {
                    continue;
                }

                WriteCell(cell, src);
            }
        }
    }

    private static void WriteCell(Cell cell, SurfaceCell src)
    {
        WriteSymbol(cell, src.Character);

        if (src.Foreground is { } fg)
        {
            cell.SetForeground(ToSpectreColor(fg));
        }

        if (src.Background is { } bg)
        {
            cell.SetBackground(ToSpectreColor(bg));
        }

        var decoration = ToDecoration(src.Attributes);
        if (decoration != Decoration.None)
        {
            cell.SetDecoration(decoration);
        }
    }

    private static void WriteSymbol(Cell cell, string character)
    {
        if (string.IsNullOrEmpty(character))
        {
            cell.SetSymbol(new Rune(' '));
            return;
        }

        // SurfaceCell.UnwrittenMarker is a private-use codepoint Hex1b uses
        // internally to mark cells that haven't been touched by a writer.
        // Render it as a regular space so the embed surface looks blank.
        if (character == "\uE000")
        {
            cell.SetSymbol(new Rune(' '));
            return;
        }

        var enumerator = character.EnumerateRunes();
        if (!enumerator.MoveNext())
        {
            cell.SetSymbol(new Rune(' '));
            return;
        }

        // Use the first rune. For multi-codepoint graphemes (e.g. heart +
        // VS-16) Spectre.Tui's only public SetSymbol takes a single Rune,
        // so we drop the trailing variation selector / combining marks.
        // Acceptable lossiness for an embedding prototype — base codepoints
        // alone render reasonably in modern terminals.
        cell.SetSymbol(enumerator.Current);
    }

    private static Color ToSpectreColor(Hex1bColor color)
    {
        if (color.IsDefault)
        {
            return Color.Default;
        }

        return new Color(color.R, color.G, color.B);
    }

    private static Decoration ToDecoration(CellAttributes attributes)
    {
        if (attributes == CellAttributes.None)
        {
            return Decoration.None;
        }

        var result = Decoration.None;
        if ((attributes & CellAttributes.Bold) != 0) result |= Decoration.Bold;
        if ((attributes & CellAttributes.Dim) != 0) result |= Decoration.Dim;
        if ((attributes & CellAttributes.Italic) != 0) result |= Decoration.Italic;
        if ((attributes & CellAttributes.Underline) != 0) result |= Decoration.Underline;
        if ((attributes & CellAttributes.Blink) != 0) result |= Decoration.SlowBlink;
        if ((attributes & CellAttributes.Reverse) != 0) result |= Decoration.Invert;
        if ((attributes & CellAttributes.Hidden) != 0) result |= Decoration.Conceal;
        if ((attributes & CellAttributes.Strikethrough) != 0) result |= Decoration.Strikethrough;
        return result;
    }
}
