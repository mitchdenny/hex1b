using System.Text;
using Hex1b;
using Spectre.Console;
using Spectre.Tui;
using Spectre.Tui.Ansi;

namespace Hex1b.Integrations.Spectre.SpectreTui;

/// <summary>
/// A <see cref="global::Spectre.Tui.ITerminal"/> implementation that flushes its
/// rendered ANSI buffer into an <see cref="IHex1bAppTerminalWorkloadAdapter"/>.
/// </summary>
/// <remarks>
/// <para>
/// Spectre.Tui's <see cref="AnsiTerminal"/> already accumulates every
/// rendered cell into an internal <see cref="StringBuilder"/> and only asks
/// its concrete subclasses to <see cref="Flush(string)"/> the resulting ANSI
/// somewhere. That seam is where we insert Hex1b — every frame Spectre.Tui
/// produces is forwarded as a single contiguous chunk into the workload
/// pipeline, ready to be parsed, recorded, presented, or embedded.
/// </para>
/// <para>
/// <see cref="GetSize"/> reads live dimensions from the adapter so a remote
/// or muxed presentation that resizes the terminal will be reflected on the
/// next render frame. <see cref="HideCursor"/> is overridden as a no-op so
/// the adapter's TUI-mode sequences own cursor visibility.
/// </para>
/// <para>
/// The adapter is stored via a primary constructor parameter capture so it
/// is accessible from <see cref="Flush(string)"/> even during base-class
/// construction — Spectre.Tui's <see cref="AnsiTerminal"/> base type calls
/// <c>Flush</c> from inside its own constructor (to push the mode's attach
/// sequence), and a regular field assignment in our constructor body would
/// not yet have run at that point.
/// </para>
/// </remarks>
/// <param name="adapter">The workload adapter receiving Spectre.Tui's rendered ANSI.</param>
/// <param name="mode">The terminal mode (fullscreen vs inline). Defaults to <see cref="FullscreenMode"/>.</param>
/// <param name="colors">The color system to advertise to Spectre.Tui. Defaults to <see cref="ColorSystem.TrueColor"/>.</param>
public sealed class Hex1bSpectreTuiTerminal(
    IHex1bAppTerminalWorkloadAdapter adapter,
    ITerminalMode? mode = null,
    ColorSystem colors = ColorSystem.TrueColor)
    : AnsiTerminal(
        BuildCapabilities(adapter, colors),
        mode ?? new FullscreenMode())
{
    /// <inheritdoc />
    public override global::Spectre.Tui.Size GetSize()
    {
        var width = adapter.Width > 0 ? adapter.Width : 80;
        var height = adapter.Height > 0 ? adapter.Height : 24;
        return Mode.GetSize(width, height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cursor visibility is driven by the workload adapter's TUI-mode entry
    /// and exit sequences. Suppressing the per-frame hide here prevents the
    /// adapter from receiving a stream of "\e[?25l" sequences that would
    /// pollute recordings and confuse downstream parsers.
    /// </remarks>
    public override void HideCursor()
    {
        // intentional no-op
    }

    /// <inheritdoc />
    protected override void Flush(string buffer)
    {
        if (string.IsNullOrEmpty(buffer))
        {
            return;
        }

        adapter.Write(buffer);
    }

    private static AnsiCapabilities BuildCapabilities(
        IHex1bAppTerminalWorkloadAdapter adapter,
        ColorSystem colors)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        // Hex1b's virtual terminal accepts the full Spectre.Tui feature
        // surface — alt buffer, OSC 8 hyperlinks, true colour. Honouring the
        // caller's colour preference lets advanced consumers downshift for
        // recordings targeted at older terminal emulators.
        return new AnsiCapabilities
        {
            Ansi = true,
            ColorSystem = colors,
            Links = true,
            AlternateBuffer = true,
        };
    }
}
