using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Integrations.Spectre.SpectreTui;

/// <summary>
/// Drives a <see cref="Hex1bWidget"/> sub-tree as discrete frame steps so it
/// can be hosted inside another rendering system — most notably a Spectre.Tui
/// <see cref="global::Spectre.Tui.IWidget"/>. Hex1bApp owns its own render loop
/// and terminal; this type lifts the same pipeline (Build → Reconcile →
/// Measure → Arrange → Render) out of that loop and exposes it as plain
/// methods callable from any host.
/// </summary>
/// <remarks>
/// <para>
/// State that Hex1b normally keeps on <see cref="Hex1b.Hex1bApp"/> — the
/// reconciled node tree, the focus ring, the chord/capture state of the
/// input router — is held on this instance and preserved across calls so
/// that focus, cursor positions, and animation tickers survive every
/// repaint of the outer host.
/// </para>
/// <para>
/// The host is async-aware on the input side because Hex1b binding actions
/// are <see cref="Task"/>-returning by contract, but
/// <see cref="RenderToSurface"/> is synchronous: Hex1b widget reconciliation
/// completes synchronously when no awaitable work is pending (the common
/// case for declarative widget builders) so the embed widget's
/// <c>Render(RenderContext)</c> can run inline on Spectre.Tui's frame
/// thread.
/// </para>
/// </remarks>
public sealed class Hex1bEmbeddedHost
{
    private readonly Hex1bTheme? _theme;
    private readonly FocusRing _focusRing = new();
    private readonly InputRouterState _inputRouterState = new();
    private Hex1bNode? _rootNode;
    private int _lastWidth;
    private int _lastHeight;

    /// <summary>
    /// Initializes a new <see cref="Hex1bEmbeddedHost"/>.
    /// </summary>
    /// <param name="theme">Optional theme passed into the rendered surface.
    /// When <c>null</c> the Hex1b default theme is used.</param>
    public Hex1bEmbeddedHost(Hex1bTheme? theme = null)
    {
        _theme = theme;
    }

    /// <summary>
    /// Gets the current root node, or <c>null</c> if no frame has been rendered yet.
    /// Exposed for advanced scenarios such as forcing focus or asserting state in tests.
    /// </summary>
    public Hex1bNode? RootNode => _rootNode;

    /// <summary>
    /// Gets the focus ring used to track focusable nodes inside the embedded tree.
    /// </summary>
    public FocusRing FocusRing => _focusRing;

    /// <summary>
    /// Builds the supplied widget tree, reconciles it against the persisted
    /// node tree, lays it out into the requested viewport, and renders it
    /// into a freshly-allocated <see cref="Surface"/>.
    /// </summary>
    /// <param name="widget">The Hex1b widget tree to render this frame.</param>
    /// <param name="width">The viewport width in cells. Must be greater than zero.</param>
    /// <param name="height">The viewport height in cells. Must be greater than zero.</param>
    /// <returns>A surface containing the rendered cells. Caller does not need to dispose.</returns>
    public Surface RenderToSurface(Hex1bWidget widget, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var reconcileTask = ReconcileAsync(widget, CancellationToken.None);
        if (!reconcileTask.IsCompleted)
        {
            // Hex1b's declarative widgets reconcile synchronously when no
            // awaits are pending. If a builder introduces real async work
            // we still need to wait for it to keep the frame consistent —
            // tolerable here because RenderToSurface is called from the
            // Spectre.Tui frame thread which is allowed to block briefly.
            reconcileTask.GetAwaiter().GetResult();
        }
        else
        {
            // Surface any synchronous exception.
            reconcileTask.GetAwaiter().GetResult();
        }

        _lastWidth = width;
        _lastHeight = height;

        var surface = new Surface(width, height);
        if (_rootNode is null)
        {
            return surface;
        }

        var size = new global::Hex1b.Layout.Size(width, height);
        _rootNode.Measure(Constraints.Tight(size));
        _rootNode.Arrange(Rect.FromSize(size));

        var renderContext = new SurfaceRenderContext(surface, _theme);
        _rootNode.Render(renderContext);

        return surface;
    }

    /// <summary>
    /// Routes a key event through the embedded Hex1b tree's input router.
    /// </summary>
    /// <param name="keyEvent">The Hex1b key event to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to async binding actions.</param>
    /// <returns><c>true</c> when an input binding or focused node consumed the event; otherwise <c>false</c>.</returns>
    public async Task<bool> HandleKeyAsync(Hex1bKeyEvent keyEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyEvent);

        if (_rootNode is null)
        {
            return false;
        }

        var result = await InputRouter.RouteInputAsync(
            _rootNode,
            keyEvent,
            _focusRing,
            _inputRouterState,
            requestStop: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result == InputResult.Handled;
    }

    /// <summary>
    /// Convenience overload that converts a <see cref="ConsoleKeyInfo"/> to a
    /// <see cref="Hex1bKeyEvent"/> using the standard
    /// <see cref="KeyMapper.ToHex1bKeyEvent(ConsoleKey, char, bool, bool, bool)"/>
    /// mapping and dispatches it.
    /// </summary>
    public Task<bool> HandleKeyAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        var keyEvent = KeyMapper.ToHex1bKeyEvent(
            keyInfo.Key,
            keyInfo.KeyChar,
            shift: (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
            alt: (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
            control: (keyInfo.Modifiers & ConsoleModifiers.Control) != 0);

        return HandleKeyAsync(keyEvent, cancellationToken);
    }

    /// <summary>
    /// Gets the width of the last viewport rendered, or zero if no frame has been rendered yet.
    /// </summary>
    public int LastWidth => _lastWidth;

    /// <summary>
    /// Gets the height of the last viewport rendered, or zero if no frame has been rendered yet.
    /// </summary>
    public int LastHeight => _lastHeight;

    private async Task ReconcileAsync(Hex1bWidget widget, CancellationToken cancellationToken)
    {
        var context = ReconcileContext.CreateRoot(
            focusRing: _focusRing,
            cancellationToken: cancellationToken);

        _rootNode = await widget.ReconcileAsync(_rootNode, context).ConfigureAwait(false);
    }
}
