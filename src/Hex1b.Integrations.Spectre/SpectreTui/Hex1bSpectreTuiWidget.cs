using Hex1b.Theming;
using Hex1b.Widgets;
using Spectre.Tui;
using HexWidget = Hex1b.Widgets.Hex1bWidget;

namespace Hex1b.Integrations.Spectre.SpectreTui;

/// <summary>
/// A Spectre.Tui <see cref="IWidget"/> that hosts a Hex1b widget sub-tree
/// inside a Spectre.Tui application. Each Spectre.Tui frame triggers a
/// fresh build of the supplied callback's widget tree, which is then
/// reconciled against the persisted Hex1b node tree, laid out into the
/// viewport, and rendered cell-by-cell into Spectre.Tui's
/// <see cref="RenderContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// State (focus, cursor positions, scroll offsets, animation tickers)
/// survives reconciliation so the embedded Hex1b widgets behave the same
/// way they would inside a normal <see cref="global::Hex1b.Hex1bApp"/>.
/// </para>
/// <para>
/// Spectre.Tui dispatches <see cref="global::Spectre.Tui.App.KeyMessage"/>
/// to whichever <see cref="global::Spectre.Tui.App.Screen"/> currently
/// owns input focus. To deliver those keys to the embedded Hex1b widgets,
/// the hosting screen calls <see cref="HandleKey(ConsoleKeyInfo)"/> from
/// its <c>OnMessage</c> override; if the call returns <c>true</c> the
/// screen treats the key as consumed.
/// </para>
/// </remarks>
public sealed class Hex1bSpectreTuiWidget : IWidget
{
    private readonly Func<HexWidget> _builder;

    /// <summary>
    /// Initializes a new <see cref="Hex1bSpectreTuiWidget"/>.
    /// </summary>
    /// <param name="builder">Callback invoked once per render frame to build the
    /// embedded Hex1b widget tree. Returning the same widget instance is
    /// acceptable and triggers a normal reconcile pass.</param>
    /// <param name="theme">Optional theme passed to the embedded surface.</param>
    public Hex1bSpectreTuiWidget(Func<HexWidget> builder, Hex1bTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Host = new Hex1bEmbeddedHost(theme);
    }

    /// <summary>
    /// Gets the underlying <see cref="Hex1bEmbeddedHost"/>. Useful when the
    /// hosting code needs to dispatch keys from an async context (use
    /// <see cref="Hex1bEmbeddedHost.HandleKeyAsync(global::Hex1b.Input.Hex1bKeyEvent, CancellationToken)"/>
    /// instead of the sync <see cref="HandleKey(ConsoleKeyInfo)"/> wrapper).
    /// </summary>
    public Hex1bEmbeddedHost Host { get; }

    /// <inheritdoc />
    public void Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var viewport = context.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var widget = _builder();
        if (widget is null)
        {
            return;
        }

        var surface = Host.RenderToSurface(widget, viewport.Width, viewport.Height);
        SpectreTuiCellWriter.Copy(surface, context);
    }

    /// <summary>
    /// Forwards a key event from Spectre.Tui's input loop into the embedded
    /// Hex1b widget tree. Intended to be called from a screen's
    /// <see cref="global::Spectre.Tui.App.Screen.OnMessage"/> override.
    /// </summary>
    /// <param name="keyInfo">The key info from a <see cref="global::Spectre.Tui.App.KeyMessage"/>.</param>
    /// <returns><c>true</c> if the embedded Hex1b tree consumed the event.</returns>
    /// <remarks>
    /// This is a synchronous wrapper over
    /// <see cref="Hex1bEmbeddedHost.HandleKeyAsync(ConsoleKeyInfo, CancellationToken)"/>
    /// for convenience. Hex1b binding actions return <see cref="Task"/> by
    /// contract but the vast majority complete synchronously, making the
    /// blocking wait safe in practice. Async callers should prefer the
    /// <see cref="Host"/>'s async API.
    /// </remarks>
    public bool HandleKey(ConsoleKeyInfo keyInfo)
    {
        return Host.HandleKeyAsync(keyInfo).GetAwaiter().GetResult();
    }
}
