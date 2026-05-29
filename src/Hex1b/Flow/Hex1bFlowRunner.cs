using System.Text;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Orchestrates a flow — consuming sequential steps (steps and full-screen apps)
/// and managing the visual stack of yield widgets in the normal terminal buffer.
/// </summary>
internal sealed class Hex1bFlowRunner
{
    private readonly Func<Hex1bFlowContext, Task> _flowCallback;
    private readonly Hex1bFlowOptions _options;
    private readonly IHex1bAppTerminalWorkloadAdapter _parentAdapter;

    /// <summary>
    /// Current cursor row in the terminal buffer (0-based, relative to terminal top).
    /// Tracks where the next yield widget or step should be rendered. After a
    /// soft-wrap tombstone is emitted, this becomes the row immediately past
    /// the last emitted line (capped to the bottom of the viewport when the
    /// terminal had to scroll). After a resize on the soft-wrap path, this is
    /// re-anchored to the top of the bottom-aligned active-step region so the
    /// next tombstone (when the active step completes) lands directly in the
    /// step's place.
    /// </summary>
    private int _cursorRow;

    /// <summary>
    /// The row at which the very first tombstone (or the active step, if no
    /// tombstones have been emitted yet) is anchored. Captured at flow start
    /// from <see cref="_cursorRow"/> and decremented whenever a tombstone
    /// emission triggers a pre-scroll (so the anchor tracks "where the top
    /// of the flow's content currently lives in the viewport"). Combined
    /// with the per-paragraph widths in <see cref="_emittedTombstones"/>,
    /// this lets the resize handler compute where the active step should
    /// land at any new width without round-tripping a CPR query.
    /// </summary>
    private int _initialRowOrigin;

    /// <summary>
    /// Per-tombstone records of paragraph widths (one inner list per
    /// emitted tombstone, one int per CR+LF-terminated paragraph it
    /// contained). The host terminal guarantees hard-newline-terminated
    /// paragraphs are never reflowed across paragraph boundaries, so the
    /// only thing we need to track to recompute on-screen layout at any
    /// width is the widths themselves. Used by the soft-wrap settle-mode
    /// resize handler.
    /// </summary>
    private readonly List<IReadOnlyList<int>> _emittedTombstones = new();

    // The currently active step, if any. Only one step may run at a time.
    private FlowStep? _activeStep;

    // CancellationToken from RunAsync, surfaced to flow callbacks via Hex1bFlowContext.
    private CancellationToken _cancellationToken;

    // DEC private mode 2026 (Synchronized Update Mode). Wrapping the
    // resize-time clear-and-redraw in BSU/ESU lets supporting terminals
    // present the whole repaint as a single atomic frame, eliminating any
    // brief blank flash between clearing the old step region and the step
    // app re-rendering into the new one. Terminals that don't recognise
    // mode 2026 ignore both sequences.
    private const string SyncUpdateBegin = "\x1b[?2026h";
    private const string SyncUpdateEnd = "\x1b[?2026l";

    // Diagnostic trace gated on the HEX1B_FLOW_TRACE environment variable.
    // When set to a writable file path, every interesting state transition
    // (tombstone emission, pre-scroll, resize handling) is appended to
    // that file. Used to investigate visual artefacts like duplicate
    // tombstones on resize. The path is read once at process start.
    private static readonly string? TraceLogPath = Environment.GetEnvironmentVariable("HEX1B_FLOW_TRACE");
    private static readonly object TraceLock = new();
    private static int _resizeCounter;
    private static int _emitCounter;

    // Always callable; becomes a near-zero-cost no-op when the env var is
    // unset (one nullable field read, one early return). Not gated on
    // [Conditional("DEBUG")] so users can capture traces from a Release-mode
    // build of FlowDemo without a special rebuild.
    private static void Trace(string message)
    {
        var path = TraceLogPath;
        if (string.IsNullOrEmpty(path)) return;
        lock (TraceLock)
        {
            try
            {
                File.AppendAllText(path, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostic logging is best-effort; never let a trace write
                // disrupt the flow.
            }
        }
    }

    public Hex1bFlowRunner(
        Func<Hex1bFlowContext, Task> flowCallback,
        Hex1bFlowOptions options,
        IHex1bAppTerminalWorkloadAdapter parentAdapter)
    {
        _flowCallback = flowCallback;
        _options = options;
        _parentAdapter = parentAdapter;
    }

    /// <summary>
    /// Gets the cancellation token from the outer flow runner.
    /// </summary>
    internal CancellationToken CancellationToken => _cancellationToken;

    /// <summary>
    /// Gets the terminal width in columns.
    /// </summary>
    internal int TerminalWidth => _parentAdapter.Width;

    /// <summary>
    /// Gets the terminal height in rows.
    /// </summary>
    internal int TerminalHeight => _parentAdapter.Height;

    /// <summary>
    /// Gets the number of rows available from the current cursor position
    /// to the bottom of the terminal (before any scrolling would occur).
    /// </summary>
    internal int AvailableHeight => Math.Max(0, _parentAdapter.Height - _cursorRow);

    /// <summary>
    /// Runs the entire flow from start to finish.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _cancellationToken = ct;

        // Query the current cursor position using the host terminal's
        // synchronous cursor API (when available). Falls back to
        // InitialCursorRow or 0 when no live query is wired.
        _cursorRow = await QueryCursorRowAsync(ct) ?? _options.InitialCursorRow ?? 0;
        _initialRowOrigin = _cursorRow;
        _emittedTombstones.Clear();

        Trace($"RunAsync start: termSize={_parentAdapter.Width}x{_parentAdapter.Height} cursorRow={_cursorRow} useSoftWrap={_options.UseSoftWrapTombstones}");

        var context = new Hex1bFlowContext(this);
        await _flowCallback(context);

        Trace($"RunAsync end: cursorRow={_cursorRow}");

        // After flow completes, position cursor below the last yield widget
        _parentAdapter.SetCursorPosition(0, _cursorRow);
        _parentAdapter.Write("\x1b[?25h"); // Ensure cursor is visible
    }

    /// <summary>
    /// Renders a static widget as frozen terminal output and advances the cursor.
    /// No interactive step is created — this is a fire-and-forget render.
    /// </summary>
    internal async Task RenderStaticAsync(Func<RootContext, Task<Hex1bWidget>> builder)
    {
        var terminalWidth = _parentAdapter.Width;
        var terminalHeight = _parentAdapter.Height;

        // Measure the content to determine how much space it needs
        var contentHeight = await MeasureYieldHeightAsync(builder, terminalWidth, terminalHeight);
        if (contentHeight < 1) contentHeight = 1;

        // Scroll if needed to make room
        var overflow = (_cursorRow + contentHeight) - terminalHeight;
        if (overflow > 0)
        {
            _parentAdapter.SetCursorPosition(0, terminalHeight - 1);
            for (int i = 0; i < overflow; i++)
            {
                _parentAdapter.Write("\n");
            }
            _cursorRow -= overflow;
        }

        // Clear and render
        ClearRegion(_cursorRow, contentHeight);
        if (_options.UseSoftWrapTombstones)
        {
            // Render the static content into a surface and emit it as
            // soft-wrap-friendly logical lines so the host terminal owns
            // the reflow/scroll behaviour for the lifetime of the flow.
            var surface = await RenderToSurfaceAsync(builder, terminalWidth, contentHeight);
            if (surface is not null)
            {
                EmitSoftWrapTombstone(surface);
                return;
            }
            // Fall through to legacy path if surface rendering failed.
        }

        var renderedHeight = await RenderYieldWidgetAsync(builder, terminalWidth, contentHeight);
        _cursorRow += renderedHeight;
    }

    /// <summary>
    /// Starts an inline step and returns a <see cref="FlowStep"/> handle for
    /// controlling it. The step runs on a background task; use the handle to
    /// invalidate, complete, and await the step.
    /// </summary>
    internal FlowStep StartStep(
        Func<FlowStepContext, Task<Hex1bWidget>> builder,
        Hex1bFlowStepOptions? options)
    {
        if (_activeStep != null)
            throw new InvalidOperationException(
                "A step is already active. Call Complete() and await the current step before starting a new one.");

        var terminalWidth = _parentAdapter.Width;
        var terminalHeight = _parentAdapter.Height;

        var maxHeight = Math.Min(options?.MaxHeight ?? terminalHeight, terminalHeight);
        if (maxHeight < 1) maxHeight = 1;

        // Pre-measure the widget to determine actual content height
        var step = new FlowStep(terminalWidth, terminalHeight, maxHeight);
        var contentHeight = MeasureStepContent(builder, step, terminalWidth, maxHeight);
        var desiredHeight = Math.Min(contentHeight, maxHeight);
        if (desiredHeight < 1) desiredHeight = 1;
        step.StepHeight = desiredHeight;

        _activeStep = step;

        // Start the step lifecycle on a background task
        _ = RunStepLifecycleAsync(step, builder, options, desiredHeight);

        return step;
    }

    /// <summary>
    /// Measures the content height of a step's widget tree by building and measuring
    /// the widget without rendering it.
    /// </summary>
    private int MeasureStepContent(
        Func<FlowStepContext, Task<Hex1bWidget>> builder,
        FlowStep step,
        int width,
        int maxHeight)
    {
        try
        {
            var stepCtx = new FlowStepContext(step);
            var widgetTask = builder(stepCtx);
            // Synchronous fallback for the measurement pass — see the
            // RenderToSurface invariant for the rationale.
            if (!widgetTask.IsCompletedSuccessfully) return maxHeight;
            var widget = widgetTask.Result;
            if (widget == null) return maxHeight;

            // Reconcile the widget into a node tree and measure it
            var reconcileCtx = ReconcileContext.CreateRoot();
            var nodeTask = widget.ReconcileAsync(null, reconcileCtx);
            // ReconcileAsync should complete synchronously for simple widgets
            if (!nodeTask.IsCompleted)
                return maxHeight; // Can't measure async widgets, use max

            var node = nodeTask.Result;
            if (node == null) return maxHeight;

            var constraints = new Layout.Constraints(0, width, 0, maxHeight);
            var measured = node.Measure(constraints);
            return Math.Max(1, measured.Height);
        }
        catch
        {
            // If measurement fails, fall back to maxHeight
            return maxHeight;
        }
    }

    private async Task RunStepLifecycleAsync(
        FlowStep step,
        Func<FlowStepContext, Task<Hex1bWidget>> builder,
        Hex1bFlowStepOptions? options,
        int desiredHeight)
    {
        try
        {
            var terminalWidth = _parentAdapter.Width;
            var terminalHeight = _parentAdapter.Height;

            // Track the row origin for this step (may be updated on resize)
            var rowOrigin = _cursorRow;

            // Scroll the terminal if the cursor is too far down to fit the step
            var overflow = (rowOrigin + desiredHeight) - terminalHeight;
            if (overflow > 0)
            {
                _parentAdapter.SetCursorPosition(0, terminalHeight - 1);
                for (int i = 0; i < overflow; i++)
                {
                    _parentAdapter.Write("\n");
                }
                rowOrigin -= overflow;
                _cursorRow = rowOrigin;
            }

            // Clear the step region
            ClearRegion(rowOrigin, desiredHeight);

            // Create the inline adapter for this step
            var stepEnableMouse = options?.EnableMouse ?? false;
            var stepCapabilities = _parentAdapter.Capabilities;
            if (stepEnableMouse && !stepCapabilities.SupportsMouse)
            {
                stepCapabilities = stepCapabilities with { SupportsMouse = true };
            }

            using var stepAdapter = new InlineStepAdapter(
                terminalWidth, desiredHeight, rowOrigin,
                stepCapabilities);

            var appOptions = new Hex1bAppOptions
            {
                WorkloadAdapter = stepAdapter,
                EnableMouse = options?.EnableMouse ?? false,
                EnableDefaultCtrlCExit = true,
                // On the soft-wrap path the active step is rendered as
                // logical lines (text + ESC[K + CR+LF) instead of as a
                // CUP-positioned cell diff. This makes the step content
                // reflowable by the host terminal alongside any
                // tombstones above it, so a horizontal resize doesn't
                // leave wrap-spillover ghost cells around the new step
                // region.
                UseSoftWrapEmission = _options.UseSoftWrapTombstones,
            };

            if (_options.Theme != null)
            {
                appOptions.Theme = _options.Theme;
            }

            // Pump output from step adapter to parent adapter. The pump is
            // muted for the duration of a resize burst (see resize handler
            // below) — without it, the inner Hex1bApp's continuous frame
            // emission (glow animations, focus blink, etc.) lands at the
            // stale rowOrigin/oldHeight and scrolls the buffer up via the
            // CR+LF row terminators inside each frame.
            using var outputPumpCts = new CancellationTokenSource();
            var outputMuteGate = new System.Runtime.CompilerServices.StrongBox<bool>(false);
            var outputPumpTask = PumpStepOutputAsync(
                stepAdapter,
                outputPumpCts.Token,
                isMuted: () => System.Threading.Volatile.Read(ref outputMuteGate.Value));

            // Pump input from parent adapter to step adapter, with resize handling
            using var inputPumpCts = new CancellationTokenSource();

            // Settle state for the new debounced resize path. Captured by
            // both the per-event handler and the timer callback. The lock
            // protects every write the resize machinery makes to the parent
            // adapter so a settle timer firing on the threadpool cannot
            // interleave with a track-and-clear pass running on the pump
            // loop.
            var settleSync = new object();
            CancellationTokenSource? settleTimerCts = null;
            (int Width, int Height)? settleOriginalDims = null;
            // Snapshot of where the active step's render region *was* when
            // the current burst started. Used at settle time to clear the
            // pre-burst rectangle in addition to the post-settle one — so
            // a stale frame the inner Hex1bApp emitted just before the
            // mute gate closed cannot leave artifacts above or below the
            // new render.
            (int RowOrigin, int Height)? settleOriginalRect = null;
            (int Width, int Height) settleLatestDims = default;
            var lastKnownWidth = _parentAdapter.Width;
            var lastKnownHeight = _parentAdapter.Height;

            var inputPumpTask = PumpStepInputAsync(stepAdapter, inputPumpCts.Token,
                onResize: (newWidth, newHeight) =>
                {
                    var newStepHeight = FlowResizeMath.ComputeStepHeight(options?.MaxHeight, newHeight);
                    Trace($"onResize: newSize={newWidth}x{newHeight} newStepH={newStepHeight} useSoftWrap={_options.UseSoftWrapTombstones} settleDelay={_options.ResizeSettleDelay}");

                    var useSettle = _options.UseSoftWrapTombstones
                        && _options.ResizeSettleDelay is { } _;

                    if (useSettle)
                    {
                        // "Track cursor on every event, repaint on settle".
                        //
                        // Per-event we do NOT touch the screen at all — no
                        // ESC[J, no placeholder draw, no bottom-overflow
                        // LFs. The host terminal already owns reflow of
                        // every byte we've emitted, so its own scrolling
                        // (if any) keeps the tombstones above naturally
                        // anchored. We only update internal bookkeeping
                        // so the settle-time repaint knows where to land.
                        //
                        // When events go quiet for ResizeSettleDelay, we
                        // do any necessary bottom-overflow scroll, clear
                        // ONLY the active-step rectangle (per-row ESC[2K,
                        // never ESC[J), optionally drop a resize-marker
                        // tombstone above it, then ask the inner Hex1bApp
                        // to repaint into the cleared region.
                        CancellationToken settleToken;
                        lock (settleSync)
                        {
                            // First event of a burst: snapshot the pre-burst
                            // rect so we can clear it at settle (the inner
                            // Hex1bApp may have left a frame painted at the
                            // old origin/height before the mute gate closed).
                            if (settleOriginalDims is null)
                            {
                                settleOriginalDims = (lastKnownWidth, lastKnownHeight);
                                settleOriginalRect = (rowOrigin, step.StepHeight);
                            }
                            settleLatestDims = (newWidth, newHeight);

                            // Hide the cursor for the duration of the drag
                            // so it doesn't visibly chase the reflow.
                            _parentAdapter.Write("\x1b[?25l");

                            // Disable line wrap (DECAWM) for the duration
                            // of the drag. The inner Hex1bApp keeps
                            // rendering at the OLD width via the output
                            // pump (glow animations, focus blink, etc.);
                            // when the terminal is shrunk, that stale
                            // wide content would wrap at the new right
                            // edge and — if any wrap lands on the bottom
                            // row — scroll the buffer up, pushing the
                            // tombstones above off-screen. With DECAWM
                            // off the host terminal truncates instead of
                            // wrapping, so the stale content lands on
                            // the active region but never scrolls. The
                            // settle pass re-enables DECAWM as its last
                            // act before showing the cursor.
                            _parentAdapter.Write("\x1b[?7l");

                            // Mute the inner-app output pump for the
                            // duration of the drag. DECAWM-off protects
                            // against right-edge wrap, but the inner
                            // app's frames also contain explicit CR+LF
                            // row separators (UseSoftWrapEmission =
                            // true). Those CR+LFs advance the cursor
                            // unconditionally — if the cursor lands at
                            // the bottom row of the now-shrunken
                            // terminal, the buffer scrolls and the
                            // tombstones above slide off-screen. Muting
                            // the pump lets the inner app keep rendering
                            // into the channel without those frames ever
                            // reaching the parent terminal.
                            System.Threading.Volatile.Write(ref outputMuteGate.Value, true);

                            // Park the cursor at the home position
                            // (1,1). The host terminal scrolls the
                            // primary buffer on shrink to keep the
                            // cursor visible — and the cursor is
                            // wherever the inner Hex1bApp last left it
                            // (typically the textbox row near the
                            // bottom of the active step). If we don't
                            // intervene, every subsequent shrink event
                            // in the burst pushes another row off the
                            // top. With the cursor parked at the home
                            // position, the host has no reason to
                            // scroll on subsequent shrink events. The
                            // very first event of a burst still loses
                            // a row or two (the host has already
                            // scrolled by the time our event handler
                            // runs), but all later events in the same
                            // drag are anchored. The settle pass
                            // restores the cursor to its proper place.
                            _parentAdapter.Write("\x1b[H");

                            // Render the placeholder (if any) into the
                            // active rect. With the inner-app output
                            // pump muted, the prompt would otherwise
                            // stay frozen showing the pre-resize frame
                            // (which is laid out for the OLD width and
                            // would visibly clip/wrap on shrink). The
                            // placeholder is a deliberately tiny widget
                            // (typically a single short line) so it
                            // fits inside even an aggressively shrunken
                            // viewport.
                            if (_options.ResizePlaceholder is { } placeholderBuilder)
                            {
                                var placeholderRowOrigin = settleOriginalRect?.RowOrigin
                                    ?? rowOrigin;
                                var placeholderHeight = settleOriginalRect?.Height
                                    ?? step.StepHeight;

                                // Use the most up-to-date width we have
                                // (latest event in the burst). DECAWM
                                // is off so any over-wide content
                                // simply truncates at the right edge.
                                var phSurface = RenderToSurface(
                                    placeholderBuilder,
                                    Math.Max(1, newWidth),
                                    Math.Max(1, placeholderHeight));
                                if (phSurface is not null)
                                {
                                    _parentAdapter.Write(SyncUpdateBegin);
                                    try
                                    {
                                        // Wipe the rect first so we don't
                                        // composite the placeholder onto
                                        // the old prompt's leftovers.
                                        for (var i = 0; i < placeholderHeight; i++)
                                        {
                                            var row = placeholderRowOrigin + i;
                                            if (row < 0 || row >= newHeight) continue;
                                            _parentAdapter.SetCursorPosition(0, row);
                                            _parentAdapter.Write("\x1b[2K");
                                        }
                                        if (placeholderRowOrigin >= 0
                                            && placeholderRowOrigin < newHeight)
                                        {
                                            _parentAdapter.SetCursorPosition(0, placeholderRowOrigin);
                                            SoftWrapEmitter.Emit(phSurface, _parentAdapter);
                                        }
                                    }
                                    finally
                                    {
                                        _parentAdapter.Write(SyncUpdateEnd);
                                    }
                                    // Park the cursor at home again so
                                    // the placeholder emission (which
                                    // leaves the cursor at the end of
                                    // the last paragraph) cannot anchor
                                    // a subsequent shrink-scroll.
                                    _parentAdapter.Write("\x1b[H");
                                }
                            }

                            // Update internal state for the eventual
                            // settle. Note: we deliberately don't write
                            // anything to the parent here — the settle
                            // pass below recomputes the origin against
                            // whatever the LATEST dimensions ended up
                            // being and does the scroll/clear/repaint as
                            // a single atomic pass.
                            desiredHeight = newStepHeight;
                            step.StepHeight = newStepHeight;

                            settleTimerCts?.Cancel();
                            settleTimerCts = CancellationTokenSource.CreateLinkedTokenSource(inputPumpCts.Token);
                            settleToken = settleTimerCts.Token;
                            lastKnownWidth = newWidth;
                            lastKnownHeight = newHeight;
                        }

                        var delay = _options.ResizeSettleDelay!.Value;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(delay, settleToken);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }

                            lock (settleSync)
                            {
                                if (settleToken.IsCancellationRequested) return;

                                var original = settleOriginalDims ?? settleLatestDims;
                                var dimsChanged = original != settleLatestDims;

                                var settledWidth = settleLatestDims.Width;
                                var settledHeight = settleLatestDims.Height;
                                var settledStepHeight = FlowResizeMath.ComputeStepHeight(
                                    options?.MaxHeight, settledHeight);

                                // Compute where the active step lands at
                                // the FINAL settled width.
                                var settledRowOrigin = FlowResizeMath.ComputeRowOriginAtWidth(
                                    _initialRowOrigin, _emittedTombstones, settledWidth);

                                // Bottom-overflow scroll — applied ONCE,
                                // at settle time, against the final
                                // dimensions. Without this the active
                                // region would render off the bottom of
                                // a shrunken terminal.
                                var bottomOverflow = (settledRowOrigin + settledStepHeight) - settledHeight;
                                if (bottomOverflow > 0)
                                {
                                    _parentAdapter.SetCursorPosition(0, settledHeight - 1);
                                    for (var i = 0; i < bottomOverflow; i++)
                                    {
                                        _parentAdapter.Write("\n");
                                    }
                                    _initialRowOrigin -= bottomOverflow;
                                    settledRowOrigin -= bottomOverflow;
                                }

                                rowOrigin = settledRowOrigin;
                                stepAdapter.RowOrigin = settledRowOrigin;
                                _cursorRow = settledRowOrigin;
                                desiredHeight = settledStepHeight;
                                step.StepHeight = settledStepHeight;

                                // Optional one-off marker tombstone above
                                // the active step. EmitSoftWrapTombstone
                                // advances _cursorRow past the marker so
                                // the step lands cleanly below it.
                                if (dimsChanged && _options.ResizeMarker is { } markerBuilder)
                                {
                                    var markerSurface = RenderToSurface(
                                        markerBuilder,
                                        settledWidth,
                                        Math.Max(1, settledHeight - settledStepHeight - 1));
                                    if (markerSurface is not null)
                                    {
                                        _parentAdapter.SetCursorPosition(0, rowOrigin);
                                        EmitSoftWrapTombstone(markerSurface);
                                        rowOrigin = _cursorRow;
                                        stepAdapter.RowOrigin = rowOrigin;
                                    }
                                }

                                // Clear the union of the pre-burst rect
                                // and the post-settle rect. Per-event we
                                // mute the inner Hex1bApp's output pump,
                                // but the very first frame of a burst may
                                // already have been written to the parent
                                // before the mute gate flipped — leaving a
                                // partial render at the OLD origin/height.
                                // If settledRowOrigin or settledStepHeight
                                // moved, that stale fragment can sit above
                                // or below the new render unless we wipe
                                // the old rect too. Row-by-row ESC[2K
                                // (never ESC[J) so a wrong computation
                                // can't erase tombstones above.
                                _parentAdapter.Write(SyncUpdateBegin);
                                try
                                {
                                    _parentAdapter.Write("\x1b[?7l");

                                    var clearTop = rowOrigin;
                                    var clearBottom = rowOrigin + settledStepHeight - 1;
                                    if (settleOriginalRect is { } origRect)
                                    {
                                        clearTop = Math.Min(clearTop, origRect.RowOrigin);
                                        clearBottom = Math.Max(
                                            clearBottom, origRect.RowOrigin + origRect.Height - 1);
                                    }
                                    for (var row = clearTop; row <= clearBottom; row++)
                                    {
                                        if (row < 0 || row >= settledHeight) continue;
                                        _parentAdapter.SetCursorPosition(0, row);
                                        _parentAdapter.Write("\x1b[2K");
                                    }
                                    _parentAdapter.SetCursorPosition(0, rowOrigin);
                                    _parentAdapter.Write("\x1b[?7h");
                                    _parentAdapter.Write("\x1b[?25h"); // show cursor
                                }
                                finally
                                {
                                    _parentAdapter.Write(SyncUpdateEnd);
                                }

                                _ = stepAdapter.ResizeAsync(settledWidth, settledStepHeight);

                                // Unmute the output pump. From this point
                                // the inner Hex1bApp's frames flow back
                                // through to the parent — its very next
                                // frame is rendered at the settled
                                // dimensions so it lands in the cleared
                                // active rectangle cleanly.
                                System.Threading.Volatile.Write(ref outputMuteGate.Value, false);

                                settleOriginalDims = null;
                                settleOriginalRect = null;
                                settleTimerCts = null;
                            }
                        });
                        return;
                    }

                    if (_options.UseSoftWrapTombstones)
                    {
                        // Eager soft-wrap path (no settle delay): preserve
                        // the existing scroll-to-scrollback behaviour so
                        // callers who haven't opted into settle keep the
                        // same semantics they've always had.
                        ScrollViewportToScrollback(newHeight);

                        stepAdapter.RowOrigin = 0;
                        rowOrigin = 0;
                        _cursorRow = 0;
                        _initialRowOrigin = 0;
                        _emittedTombstones.Clear();
                        desiredHeight = newStepHeight;
                        step.StepHeight = newStepHeight;
                    }
                    else
                    {
                        // Legacy path: cell-positioned tombstones can't be
                        // preserved across reflow, so we wipe the whole visible
                        // area and bottom-anchor the new step.
                        var (clearOrigin, clearHeight) = FlowResizeMath.ComputeClearRegion(
                            useSoftWrapTombstones: false, newHeight, newStepHeight);
                        var newRowOrigin = Math.Max(0, newHeight - newStepHeight);

                        ClearRegion(clearOrigin, clearHeight);

                        stepAdapter.RowOrigin = newRowOrigin;
                        rowOrigin = newRowOrigin;
                        _cursorRow = newRowOrigin;
                        desiredHeight = newStepHeight;
                        step.StepHeight = newStepHeight;
                    }

                    lastKnownWidth = newWidth;
                    lastKnownHeight = newHeight;

                    _ = stepAdapter.ResizeAsync(newWidth, newStepHeight);
                });

            try
            {
                // Wrap the user's builder to inject the FlowStepContext
                var stepCtx = new FlowStepContext(step);
                await using var app = new Hex1bApp(rootCtx =>
                {
                    stepCtx.CancellationToken = rootCtx.CancellationToken;
                    return builder(stepCtx);
                }, appOptions);
                step.SetApp(app);
                await app.RunAsync(default);
            }
            finally
            {
                outputPumpCts.Cancel();
                inputPumpCts.Cancel();

                try { await outputPumpTask; } catch (OperationCanceledException) { }
                try { await inputPumpTask; } catch (OperationCanceledException) { }
            }

            // Clear the step region so remnants don't show through the yield widget
            ClearRegion(_cursorRow, desiredHeight);

            // Render the completed widget as frozen output
            var completedBuilder = step.CompletedBuilder;
            if (completedBuilder != null)
            {
                if (_options.UseSoftWrapTombstones)
                {
                    var surface = RenderToSurface(completedBuilder, terminalWidth, desiredHeight);
                    if (surface is not null)
                    {
                        EmitSoftWrapTombstone(surface);
                        Trace("Step completed: tombstone emitted (append-only, terminal owns reflow)");
                    }
                    else
                    {
                        // Fall back to the legacy path if surface rendering failed.
                        var completedHeight = await RenderYieldWidgetAsync(completedBuilder, terminalWidth, desiredHeight);
                        _cursorRow += completedHeight;
                    }
                }
                else
                {
                    var completedHeight = await RenderYieldWidgetAsync(completedBuilder, terminalWidth, desiredHeight);
                    _cursorRow += completedHeight;
                }
            }

            _activeStep = null;
            step.SetCompleted();
        }
        catch (Exception ex)
        {
            _activeStep = null;
            step.SetFaulted(ex);
        }
    }

    /// <summary>
    /// Runs a full-screen TUI application in the alternate screen buffer.
    /// </summary>
    internal async Task RunFullScreenStepAsync(
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Task<Hex1bWidget>>> configure)
    {
        // The parent adapter handles alt-buffer transitions naturally
        // We create a standard Hex1bApp with the parent adapter
        var appOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = _parentAdapter,
            EnableMouse = _options.EnableMouse,
        };

        if (_options.Theme != null)
        {
            appOptions.Theme = _options.Theme;
        }

        Hex1bApp? app = null;
        Func<RootContext, Task<Hex1bWidget>>? widgetBuilder = null;
        bool configureInvoked = false;

        Func<RootContext, Task<Hex1bWidget>> wrappedBuilder = ctx =>
        {
            if (!configureInvoked)
            {
                configureInvoked = true;
                widgetBuilder = configure(app!, appOptions);
            }
            return widgetBuilder!(ctx);
        };

        app = new Hex1bApp(wrappedBuilder, appOptions);
        await using (app)
        {
            await app.RunAsync(default);
        }

        // After returning from full-screen, the terminal restores the normal buffer
        // which already contains the frozen yield output. No re-rendering needed.
    }

    /// <summary>
    /// Renders a yield widget and returns its height.
    /// If the content exceeds the available screen space, it is rendered in
    /// pages with the terminal scrolling between each page so no content is lost.
    /// </summary>
    private async Task<int> RenderYieldWidgetAsync(
        Func<RootContext, Task<Hex1bWidget>> yieldBuilder,
        int width,
        int maxHeight)
    {
        // First, measure the yield widget to determine its natural height.
        var measuredHeight = await MeasureYieldHeightAsync(yieldBuilder, width, maxHeight * 10);
        if (measuredHeight < 1) measuredHeight = 1;

        // If it fits in one screen, render in place
        if (measuredHeight <= maxHeight)
        {
            await RenderYieldPageAsync(yieldBuilder, width, measuredHeight);
            return measuredHeight;
        }

        // Content overflows the screen — render in pages.
        // We render the full content into a tall adapter, then write it page by page
        // to the terminal, scrolling between pages.
        var totalRendered = 0;
        var terminalHeight = _parentAdapter.Height;
        var remainingLines = measuredHeight;

        while (remainingLines > 0)
        {
            var pageHeight = Math.Min(remainingLines, terminalHeight);

            // Scroll to make room for this page
            var overflow = (_cursorRow + pageHeight) - terminalHeight;
            if (overflow > 0)
            {
                _parentAdapter.SetCursorPosition(0, terminalHeight - 1);
                for (int i = 0; i < overflow; i++)
                    _parentAdapter.Write("\n");
                _cursorRow -= overflow;
            }

            // Clear the page region
            ClearRegion(_cursorRow, pageHeight);

            // Render a step of the yield content at the current offset
            int offset = totalRendered;
            await RenderYieldPageAsync(async ctx =>
            {
                // Build a wrapper that skips the first 'offset' rows and takes 'pageHeight'
                var fullWidget = await yieldBuilder(ctx);
                return fullWidget;
            }, width, pageHeight, offset);

            _cursorRow += pageHeight;
            totalRendered += pageHeight;
            remainingLines -= pageHeight;
        }

        return totalRendered;
    }

    /// <summary>
    /// Measures the natural height of a yield widget tree. Async sibling that awaits the builder
    /// properly; falls back to height 1 if the builder or reconciliation isn't synchronous.
    /// </summary>
    private async Task<int> MeasureYieldHeightAsync(Func<RootContext, Task<Hex1bWidget>> yieldBuilder, int width, int maxHeight)
    {
        try
        {
            var rootCtx = new RootContext();
            var widget = await yieldBuilder(rootCtx);
            if (widget == null) return 1;

            var reconcileCtx = ReconcileContext.CreateRoot();
            var node = await widget.ReconcileAsync(null, reconcileCtx);
            if (node == null) return 1;

            var constraints = new Constraints(0, width, 0, maxHeight);
            var measured = node.Measure(constraints);
            return Math.Max(1, measured.Height);
        }
        catch
        {
            return 1;
        }
    }

    /// <summary>
    /// Renders a yield widget page at the current cursor position.
    /// </summary>
    private async Task RenderYieldPageAsync(
        Func<RootContext, Task<Hex1bWidget>> yieldBuilder,
        int width,
        int height,
        int skipRows = 0)
    {
        Func<RootContext, Task<Hex1bWidget>> actualBuilder;
        if (skipRows > 0)
        {
            actualBuilder = async ctx =>
            {
                var widget = await yieldBuilder(ctx);
                if (widget is VStackWidget vstack && skipRows < vstack.Children.Count)
                {
                    var remaining = vstack.Children.Skip(skipRows).Take(height).ToArray();
                    return new VStackWidget(remaining);
                }
                return widget;
            };
        }
        else
        {
            actualBuilder = yieldBuilder;
        }

        using var yieldAdapter = new InlineStepAdapter(
            width, height, _cursorRow,
            _parentAdapter.Capabilities);

        var yieldOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = yieldAdapter,
            EnableMouse = false,
            EnableDefaultCtrlCExit = false,
        };

        if (_options.Theme != null)
            yieldOptions.Theme = _options.Theme;

        var pumpCts = new CancellationTokenSource();
        var pumpTask = PumpStepOutputAsync(yieldAdapter, pumpCts.Token);

        try
        {
            Hex1bApp? yieldApp = null;
            bool rendered = false;

            yieldApp = new Hex1bApp(ctx =>
            {
                var widgetTask = actualBuilder(ctx);
                if (!rendered)
                {
                    rendered = true;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(50);
                        yieldApp?.RequestStop();
                    });
                }
                return widgetTask;
            }, yieldOptions);

            await using (yieldApp)
            {
                await yieldApp.RunAsync(default);
            }
        }
        finally
        {
            pumpCts.Cancel();
            try { await pumpTask; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Clears a region of the terminal at the given row origin.
    /// </summary>
    private void ClearRegion(int rowOrigin, int height)
    {
        var sb = new StringBuilder();
        for (int row = 0; row < height; row++)
        {
            sb.Append($"\x1b[{rowOrigin + row + 1};1H");
            sb.Append("\x1b[2K");
        }
        _parentAdapter.Write(sb.ToString());
    }

    private Surface? RenderToSurface(
        Func<RootContext, Hex1bWidget> builder,
        int width,
        int maxHeight)
        => RenderToSurface(ctx => Task.FromResult(builder(ctx)), width, maxHeight);

    /// <summary>
    /// Renders an async widget builder into a freshly-allocated <see cref="Surface"/>
    /// synchronously. Returns <c>null</c> if the builder Task hasn't already completed,
    /// if reconciliation needs to go async, or if anything throws — callers should
    /// fall back to the legacy emission path in that case.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by call sites that can't await (the per-event resize-burst handler and
    /// the settle-task body, both of which run inside <c>lock(settleSync)</c>).
    /// The "must already be completed" invariant mirrors the existing constraint
    /// on <see cref="Hex1bWidget.ReconcileAsync"/>: flow tombstones build widgets
    /// from pre-resolved state and wrap them with <see cref="Task.FromResult{TResult}(TResult)"/>,
    /// so the Task is completed inline on the caller's thread. Truly-async builders
    /// (network IO etc.) simply skip a resize frame and the next event re-triggers.
    /// </para>
    /// <para>
    /// Used only on the soft-wrap tombstone path
    /// (<see cref="Hex1bFlowOptions.UseSoftWrapTombstones"/>). The surface is sized
    /// to <paramref name="width"/> by the widget's measured height (clamped to
    /// <paramref name="maxHeight"/> × 10 to bound page-by-page content). The surface
    /// is then arranged and rendered using the standard rendering pipeline so any
    /// widget that works on screen will work here.
    /// </para>
    /// </remarks>
    private Surface? RenderToSurface(
        Func<RootContext, Task<Hex1bWidget>> builder,
        int width,
        int maxHeight)
    {
        try
        {
            var rootCtx = new RootContext();
            var widgetTask = builder(rootCtx);
            if (!widgetTask.IsCompletedSuccessfully) return null;
            var widget = widgetTask.Result;
            if (widget == null) return null;

            var reconcileCtx = ReconcileContext.CreateRoot();
            var nodeTask = widget.ReconcileAsync(null, reconcileCtx);
            // Reconciliation should complete synchronously for the widgets
            // used in flow tombstones today; if it doesn't, defer to the
            // legacy renderer which has its own measurement fallback.
            if (!nodeTask.IsCompleted) return null;

            var node = nodeTask.Result;
            if (node == null) return null;

            return MaterializeSurface(node, width, maxHeight);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Async sibling of <see cref="RenderToSurface(Func{RootContext, Task{Hex1bWidget}}, int, int)"/>
    /// for callers that can <c>await</c> the builder (and reconciliation) properly.
    /// Used by the static/yield/completed-tombstone paths that already run in
    /// <c>async Task</c> methods.
    /// </summary>
    private async Task<Surface?> RenderToSurfaceAsync(
        Func<RootContext, Task<Hex1bWidget>> builder,
        int width,
        int maxHeight)
    {
        try
        {
            var rootCtx = new RootContext();
            var widget = await builder(rootCtx);
            if (widget == null) return null;

            var reconcileCtx = ReconcileContext.CreateRoot();
            var node = await widget.ReconcileAsync(null, reconcileCtx);
            if (node == null) return null;

            return MaterializeSurface(node, width, maxHeight);
        }
        catch
        {
            return null;
        }
    }

    private Surface? MaterializeSurface(Hex1bNode node, int width, int maxHeight)
    {
        // Measure with a generous height bound so multi-line tombstones
        // get their full height, then clamp to a safety ceiling so a
        // misbehaving widget can't allocate an unbounded surface.
        var measureMax = Math.Max(maxHeight * 10, maxHeight);
        var constraints = new Constraints(0, width, 0, measureMax);
        var measured = node.Measure(constraints);
        var height = Math.Max(1, Math.Min(measured.Height, measureMax));

        var surface = new Surface(width, height);
        node.Arrange(new Rect(0, 0, width, height));

        var renderCtx = new SurfaceRenderContext(surface, _options.Theme);
        renderCtx.SetCapabilities(_parentAdapter.Capabilities);
        node.Render(renderCtx);

        return surface;
    }

    /// <summary>
    /// Emits the contents of <paramref name="surface"/> as a tombstone via
    /// <see cref="SoftWrapEmitter"/>, pre-scrolling the terminal as needed so
    /// the emission fits in the visible area, and updating <see cref="_cursorRow"/>
    /// to point at the row directly below the tombstone.
    /// </summary>
    private void EmitSoftWrapTombstone(Surface surface)
    {
        var emitId = Interlocked.Increment(ref _emitCounter);
        var height = surface.Height;
        var terminalHeight = _parentAdapter.Height;

        Trace($"EmitSoftWrapTombstone[#{emitId}] enter: surfaceSize={surface.Width}x{height} cursorRow={_cursorRow} termH={terminalHeight}");

        // Pre-scroll the viewport if there isn't enough room below the cursor
        // for the tombstone. We use the same trick as the legacy paths
        // (writing newlines at the bottom row) which causes the terminal to
        // scroll the existing content up — including any older tombstones,
        // which is the desired behaviour.
        var overflow = (_cursorRow + height) - terminalHeight;
        if (overflow > 0)
        {
            _parentAdapter.SetCursorPosition(0, terminalHeight - 1);
            for (int i = 0; i < overflow; i++)
            {
                _parentAdapter.Write("\n");
            }
            _cursorRow -= overflow;
            // The viewport scrolled up by `overflow` rows, so every
            // previously-emitted tombstone (and the initial anchor) moved up
            // by the same amount on screen. Track that shift so
            // ComputeRowOriginAtWidth keeps returning the correct on-screen
            // row for the active step after a future resize.
            _initialRowOrigin -= overflow;
            Trace($"EmitSoftWrapTombstone[#{emitId}] pre-scroll: overflow={overflow} -> cursorRow={_cursorRow} initialRowOrigin={_initialRowOrigin}");
        }

        // Position the cursor at the row where the tombstone should land.
        _parentAdapter.SetCursorPosition(0, _cursorRow);

        SoftWrapEmitter.Emit(surface, _parentAdapter);

        // Capture this tombstone's per-paragraph widths so the soft-wrap
        // settle-mode resize handler can recompute the active step's
        // row origin at any width. Each surface row corresponds to one
        // CR+LF-terminated paragraph (the SoftWrapEmitter contract); the
        // logical width is the column index of the last non-blank cell
        // plus one. Empty rows count as zero-width paragraphs and still
        // occupy one display row on reflow (the Math.Max(1, ...) in
        // ComputeRowOriginAtWidth handles that).
        var paragraphWidths = new int[height];
        for (var row = 0; row < height; row++)
        {
            paragraphWidths[row] = MeasureSurfaceRowWidth(surface, row);
        }
        _emittedTombstones.Add(paragraphWidths);

        // The emitter terminates rows 0 .. height-2 with CR + LF (each
        // advances the cursor down one row, with no scrolling because we
        // pre-scrolled above to guarantee the last row lands at or above the
        // bottom of the viewport). The final row deliberately has no trailing
        // newline so emitting a tombstone at the very bottom does not scroll
        // the content one row up — visually the tombstone freezes in place
        // exactly where the step was. The terminal cursor therefore ends up
        // at (last-row-content-column, _cursorRow + height - 1); for our
        // bookkeeping we want _cursorRow to point at the row immediately
        // *below* the last visible tombstone row, so the next render lands
        // there cleanly.
        _cursorRow += height;

        Trace($"EmitSoftWrapTombstone[#{emitId}] exit: cursorRow={_cursorRow}");
    }

    /// <summary>
    /// Returns the logical paragraph width of the given surface row — the
    /// column index of the last non-blank cell plus one. Mirrors the
    /// trailing-blank trimming that <see cref="SoftWrapEmitter"/> performs
    /// when it emits the row, so the recorded paragraph width matches the
    /// number of cells the host terminal will actually have to reflow.
    /// </summary>
    private static int MeasureSurfaceRowWidth(Surface surface, int row)
    {
        var width = surface.Width;
        for (var x = width - 1; x >= 0; x--)
        {
            var cell = surface.GetCell(x, row);
            if (cell.IsContinuation)
            {
                // A continuation cell means the wide glyph occupies (x-1, x);
                // treat (x) as content because removing only the continuation
                // would mis-report the wide character's footprint.
                return x + 1;
            }
            if (cell.Character != " "
                && cell.Character != string.Empty
                && cell.Character != SurfaceCells.UnwrittenMarker)
            {
                return x + 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// Pushes the entire current viewport contents up off the top of the
    /// screen and into the host terminal's scrollback buffer, then clears
    /// the viewport. Used by the soft-wrap resize handler so the active
    /// step (which uses absolute cursor positioning) can be cleanly
    /// re-anchored at row 0 without competing with reflowed tombstone
    /// content from the previous viewport.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tombstones above the active step were emitted as soft-wrap-friendly
    /// logical lines, so once they are scrolled into scrollback the host
    /// terminal renders them with proper word wrap at the new width.
    /// Cell-positioned step content cannot reflow that way, so we just
    /// scroll it away and let the step app repaint at the new size.
    /// </para>
    /// <para>
    /// Mechanism: park cursor at the bottom row, write <paramref name="newHeight"/>
    /// linefeeds. Each LF at the bottom row scrolls the viewport up by one
    /// row (pushing the top row into scrollback), so writing a full
    /// viewport-height worth of LFs guarantees every previously-visible
    /// row ends up in scrollback. Then position cursor at home and clear
    /// from there to end of screen — this leaves a fully blank viewport
    /// with cursor at (0,0), ready for the step to re-render.
    /// </para>
    /// <para>
    /// Bracketed in DEC private mode 2026 (Synchronized Update Mode) so
    /// supporting terminals present the scroll+clear as one atomic frame.
    /// Terminals that ignore mode 2026 see a brief flash but no
    /// functional regression.
    /// </para>
    /// </remarks>
    private void ScrollViewportToScrollback(int newHeight)
    {
        var resizeId = Interlocked.Increment(ref _resizeCounter);
        Trace($"ScrollViewportToScrollback[#{resizeId}] enter: newHeight={newHeight}");

        _parentAdapter.Write(SyncUpdateBegin);
        try
        {
            // Park at bottom-left and emit one LF per viewport row. Each
            // LF at the bottom scrolls the viewport up by one row, moving
            // the top line into scrollback. After newHeight LFs every
            // previously-visible row has been pushed into scrollback.
            _parentAdapter.SetCursorPosition(0, newHeight - 1);
            var sb = new StringBuilder(newHeight);
            for (int i = 0; i < newHeight; i++)
            {
                sb.Append('\n');
            }
            _parentAdapter.Write(sb.ToString());

            // Home + clear-to-end-of-screen. After scrolling, the cursor
            // may be anywhere; reset to top-left and wipe so the step can
            // render from a known-blank canvas.
            _parentAdapter.Write("\x1b[1;1H\x1b[J");
        }
        finally
        {
            _parentAdapter.Write(SyncUpdateEnd);
        }

        Trace($"ScrollViewportToScrollback[#{resizeId}] exit");
    }

    /// <summary>
    /// Pumps output from a step adapter to the parent adapter.
    /// </summary>
    private async Task PumpStepOutputAsync(
        InlineStepAdapter stepAdapter,
        CancellationToken ct,
        Func<bool>? isMuted = null)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await stepAdapter.ReadOutputAsync(ct);
                if (data.IsEmpty) continue;
                // Drop frames while the runner has the pump muted (e.g.
                // during a resize burst). Forwarding them would replay
                // stale-sized content at the stale rowOrigin, which is
                // the dominant cause of buffer-scroll during a drag.
                if (isMuted?.Invoke() == true) continue;
                _parentAdapter.Write(Encoding.UTF8.GetString(data.Span));
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Pumps input events from the parent adapter to a step adapter.
    /// Intercepts resize events to recalculate the step position.
    /// </summary>
    private async Task PumpStepInputAsync(
        InlineStepAdapter stepAdapter,
        CancellationToken ct,
        Action<int, int>? onResize = null)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await _parentAdapter.InputEvents.WaitToReadAsync(ct))
                {
                    while (_parentAdapter.InputEvents.TryRead(out var evt))
                    {
                        if (evt is Hex1bResizeEvent resize && onResize != null)
                        {
                            // Let the runner handle repositioning before forwarding
                            onResize(resize.Width, resize.Height);
                            continue; // ResizeAsync already called in onResize
                        }
                        await stepAdapter.WriteInputEventAsync(evt, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Queries the host terminal's current cursor row (0-based). Uses the
    /// <see cref="Hex1bFlowOptions.CursorRowProvider"/> delegate when set so
    /// the runner can read the post-reflow position of the parked cursor
    /// after a horizontal resize. Falls back to
    /// <see cref="Hex1bFlowOptions.InitialCursorRow"/> when no provider is
    /// available (headless/test scenarios).
    /// </summary>
    private Task<int?> QueryCursorRowAsync(CancellationToken ct)
    {
        if (_options.CursorRowProvider is { } provider)
        {
            try
            {
                return Task.FromResult(provider());
            }
            catch
            {
                // Provider failures collapse to "unavailable" so callers
                // can fall back to bottom-anchor behaviour.
                return Task.FromResult<int?>(null);
            }
        }

        return Task.FromResult<int?>(_options.InitialCursorRow);
    }
}

/// <summary>
/// Options for the Hex1b Flow system.
/// </summary>
public sealed class Hex1bFlowOptions
{
    /// <summary>
    /// Theme for all steps and full-screen apps in the flow.
    /// </summary>
    public Hex1bTheme? Theme { get; set; }

    /// <summary>
    /// Whether to enable mouse input for full-screen steps.
    /// </summary>
    public bool EnableMouse { get; set; }

    /// <summary>
    /// Initial cursor row (0-based) where the flow starts rendering.
    /// Set this to <c>Console.GetCursorPosition().Top</c> before calling RunAsync.
    /// If null, defaults to 0.
    /// </summary>
    public int? InitialCursorRow { get; set; }

    /// <summary>
    /// Optional delegate that returns the host terminal's current cursor row
    /// (0-based). When set, the flow runner uses this to read the initial
    /// cursor position at startup, before the input pump is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implemented on top of <c>Console.GetCursorPosition()</c> by
    /// <c>Hex1bTerminalBuilder.WithHex1bFlow</c> when a presentation adapter
    /// is available. On Windows this resolves through Win32
    /// <c>GetConsoleScreenBufferInfo</c> (no DSR roundtrip); on Unix the BCL
    /// uses a raw DSR query that reads the response from stdin.
    /// </para>
    /// <para>
    /// <strong>Must not be called from within a resize handler.</strong>
    /// On Unix, calling this while Hex1b's input pump is running causes a
    /// deadlock: the DSR response arrives on the same stdin file descriptor
    /// that the input pump owns, so <c>Console.GetCursorPosition()</c>
    /// blocks forever waiting for bytes it will never receive.
    /// </para>
    /// <para>
    /// Returns <c>null</c> to indicate the cursor row could not be
    /// determined.
    /// </para>
    /// </remarks>
    public Func<int?>? CursorRowProvider { get; set; }

    /// <summary>
    /// When <c>true</c>, tombstoned (yield) widget output is emitted as
    /// soft-wrap-friendly logical lines (text + <c>ESC[K</c> + LF) so the host
    /// terminal can reflow tombstones during a resize and scroll older
    /// tombstones into the scrollback buffer naturally. When <c>false</c>
    /// (the default), tombstones are emitted with absolute cursor positioning
    /// and the entire visible area is cleared on every resize, which causes
    /// tombstones to disappear when the terminal is resized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This option is experimental and gates two related behaviours together:
    /// the tombstone emission path and the resize handler. The new resize
    /// handler only clears the active step region and trusts the host
    /// terminal to have reflowed tombstones above it — that is only a valid
    /// assumption when tombstones were emitted as proper logical lines, so
    /// both behaviours move together under this single flag.
    /// </para>
    /// <para>
    /// Once validated across the supported terminal matrix this will become
    /// the default and the legacy cell-positioning path will be removed.
    /// </para>
    /// </remarks>
    public bool UseSoftWrapTombstones { get; set; }

    /// <summary>
    /// Quiet window required after the last <c>Hex1bResizeEvent</c> before
    /// the runner re-renders the active step at the new size. When
    /// <c>null</c> (the default), the runner repaints eagerly on every
    /// resize event — today's behaviour. When set, the runner enters a
    /// two-phase resize mode: every resize event performs a cheap
    /// "track-and-clear" pass (recompute where the active step should
    /// land at the new width, move the cursor there, and erase the region
    /// below), and only after the terminal has been idle for the settle
    /// delay does the inner step app re-render.
    /// </summary>
    /// <remarks>
    /// Only takes effect when <see cref="UseSoftWrapTombstones"/> is also
    /// <c>true</c>: the track-and-clear pass relies on tombstones above
    /// the active step being hard-newline-terminated paragraphs that the
    /// host terminal will not reflow across paragraph boundaries.
    /// Recommended value: 50–100 ms.
    /// </remarks>
    public TimeSpan? ResizeSettleDelay { get; set; }

    /// <summary>
    /// Optional widget builder emitted as a one-off hard-newline tombstone
    /// <em>above</em> the repainted step after the resize has settled, but
    /// only when the final dimensions differ from the dimensions at the
    /// start of the settle window. Intended for a faint
    /// "─── terminal resized ───" breadcrumb. When <c>null</c> (the
    /// default), no marker is emitted.
    /// </summary>
    /// <remarks>
    /// <para>Has no effect when <see cref="ResizeSettleDelay"/> is <c>null</c>.</para>
    /// <para>
    /// Sync-only by design: the runner invokes this builder from a render-time
    /// critical section that cannot <c>await</c>. Build the widget from
    /// already-resolved state and rely on the rest of the flow API's async
    /// surface for IO-bearing work.
    /// </para>
    /// </remarks>
    public Func<RootContext, Hex1bWidget>? ResizeMarker { get; set; }

    /// <summary>
    /// Optional widget builder rendered in place of the active step's
    /// content during a drag-resize burst. The placeholder is drawn at
    /// each per-event tick into the active rectangle and is then replaced
    /// by the actual repainted step at settle. Use a deliberately tiny
    /// widget (a single short line is ideal) so it fits inside even an
    /// aggressively shrunken viewport. When <c>null</c> (the default),
    /// the active rectangle simply stays cleared during the drag.
    /// </summary>
    /// <remarks>
    /// <para>Has no effect when <see cref="ResizeSettleDelay"/> is <c>null</c>.</para>
    /// <para>
    /// Sync-only by design: the runner invokes this builder per resize event
    /// from a critical section that cannot <c>await</c>. Build the widget
    /// from already-resolved state and rely on the rest of the flow API's
    /// async surface for IO-bearing work.
    /// </para>
    /// </remarks>
    public Func<RootContext, Hex1bWidget>? ResizePlaceholder { get; set; }
}
