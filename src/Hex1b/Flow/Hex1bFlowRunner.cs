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
    /// next tombstone appears immediately above the active step again.
    /// </summary>
    private int _cursorRow;

    // The currently active step, if any. Only one step may run at a time.
    private FlowStep? _activeStep;

    // CancellationToken from RunAsync, surfaced to flow callbacks via Hex1bFlowContext.
    private CancellationToken _cancellationToken;

    // Builders for every soft-wrap tombstone emitted by this runner, in
    // emission order. Used by the resize handler on the
    // UseSoftWrapTombstones path to re-render the entire tombstone history
    // at the new terminal width: the host terminal's reflow algorithm is
    // unpredictable across terminal emulators (xterm vs Windows Terminal vs
    // iTerm2 vs VTE), and the previous "clear only the active-step region
    // and trust the terminal" strategy left ghost copies of the old active
    // step above the new one and could clip wrapped tombstones. Owning the
    // re-render eliminates the dependence on terminal-specific behaviour at
    // the cost of one extra surface render per tombstone per resize event.
    // Resize is rare and tombstones are small, so the cost is negligible.
    // List is appended to from the soft-wrap branches of RenderStaticAsync
    // and RunStepLifecycleAsync's post-step block; never cleared during the
    // flow's lifetime.
    private readonly List<Func<RootContext, Hex1bWidget>> _tombstoneBuilders = new();

    // The terminal row at which this flow's content begins. Initialised
    // from Hex1bFlowOptions.InitialCursorRow at the start of RunAsync, then
    // decremented by overflow whenever the runner pre-scrolls the terminal
    // (in EmitSoftWrapTombstone, RenderStaticAsync, or
    // RunStepLifecycleAsync). The resize handler uses this to position the
    // cursor at the flow's origin and clear-from-cursor-to-end (ESC[J),
    // preserving any user content above the flow (typically a shell prompt)
    // that hasn't yet been scrolled into the host terminal's scrollback.
    private int _flowOriginRow;

    // DEC private mode 2026 (Synchronized Update Mode). Wrapping the
    // resize-time clear-and-re-render in BSU/ESU lets supporting terminals
    // present the whole repaint as a single atomic frame, eliminating the
    // brief blank flash between "clear viewport" and "tombstones drawn".
    // Terminals that don't recognise mode 2026 ignore both sequences.
    private const string SyncUpdateBegin = "\x1b[?2026h";
    private const string SyncUpdateEnd = "\x1b[?2026l";

    // Diagnostic trace gated on the HEX1B_FLOW_TRACE environment variable.
    // When set to a writable file path, every interesting state transition
    // (tombstone emission, pre-scroll, resize handling, re-render) is
    // appended to that file. Used to investigate visual artefacts like
    // duplicate tombstones on resize. The path is read once at process start.
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

        // Query the current cursor position using DSR (Device Status Report)
        _cursorRow = await QueryCursorRowAsync(ct);
        _flowOriginRow = _cursorRow;

        Trace($"RunAsync start: termSize={_parentAdapter.Width}x{_parentAdapter.Height} cursorRow={_cursorRow} flowOriginRow={_flowOriginRow} useSoftWrap={_options.UseSoftWrapTombstones}");

        var context = new Hex1bFlowContext(this);
        await _flowCallback(context);

        Trace($"RunAsync end: cursorRow={_cursorRow} flowOriginRow={_flowOriginRow} tombstones={_tombstoneBuilders.Count}");

        // After flow completes, position cursor below the last yield widget
        _parentAdapter.SetCursorPosition(0, _cursorRow);
        _parentAdapter.Write("\x1b[?25h"); // Ensure cursor is visible
    }

    /// <summary>
    /// Renders a static widget as frozen terminal output and advances the cursor.
    /// No interactive step is created — this is a fire-and-forget render.
    /// </summary>
    internal async Task RenderStaticAsync(Func<RootContext, Hex1bWidget> builder)
    {
        var terminalWidth = _parentAdapter.Width;
        var terminalHeight = _parentAdapter.Height;

        // Measure the content to determine how much space it needs
        var contentHeight = MeasureYieldHeight(builder, terminalWidth, terminalHeight);
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
            _flowOriginRow = Math.Max(0, _flowOriginRow - overflow);
        }

        // Clear and render
        ClearRegion(_cursorRow, contentHeight);
        if (_options.UseSoftWrapTombstones)
        {
            // Render the static content into a surface and emit it as
            // soft-wrap-friendly logical lines so the host terminal owns
            // the reflow/scroll behaviour.
            var surface = RenderToSurface(builder, terminalWidth, contentHeight);
            if (surface is not null)
            {
                EmitSoftWrapTombstone(surface);
                // Track the builder so a later resize can re-render this
                // static tombstone at the new width via the resize handler.
                _tombstoneBuilders.Add(builder);
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
        Func<FlowStepContext, Hex1bWidget> builder,
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
        Func<FlowStepContext, Hex1bWidget> builder,
        FlowStep step,
        int width,
        int maxHeight)
    {
        try
        {
            var stepCtx = new FlowStepContext(step);
            var widget = builder(stepCtx);
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
        Func<FlowStepContext, Hex1bWidget> builder,
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
                _flowOriginRow = Math.Max(0, _flowOriginRow - overflow);
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
            };

            if (_options.Theme != null)
            {
                appOptions.Theme = _options.Theme;
            }

            // Pump output from step adapter to parent adapter
            using var outputPumpCts = new CancellationTokenSource();
            var outputPumpTask = PumpStepOutputAsync(stepAdapter, outputPumpCts.Token);

            // Pump input from parent adapter to step adapter, with resize handling
            using var inputPumpCts = new CancellationTokenSource();
            var inputPumpTask = PumpStepInputAsync(stepAdapter, inputPumpCts.Token,
                onResize: (newWidth, newHeight) =>
                {
                    var newStepHeight = FlowResizeMath.ComputeStepHeight(options?.MaxHeight, newHeight);
                    Trace($"onResize: newSize={newWidth}x{newHeight} newStepH={newStepHeight} useSoftWrap={_options.UseSoftWrapTombstones}");

                    if (_options.UseSoftWrapTombstones)
                    {
                        // Owned re-render: clear the entire visible area and
                        // redraw the tombstone history from scratch at the new
                        // width, then re-anchor the active step. This avoids
                        // the unpredictable host-terminal reflow behaviour that
                        // would otherwise leave a ghost of the old active step
                        // above the new one and could clip wrapped tombstones
                        // on width shrink. See the comment on
                        // _tombstoneBuilders for the full rationale.
                        var newRowOrigin = ReRenderTombstonesAndAnchorStep(newWidth, newHeight, newStepHeight);

                        stepAdapter.RowOrigin = newRowOrigin;
                        rowOrigin = newRowOrigin;
                        _cursorRow = newRowOrigin;
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
                        // Track the builder so a later resize can re-render
                        // this completed-step tombstone at the new width
                        // via the resize handler.
                        _tombstoneBuilders.Add(completedBuilder);
                        Trace($"Step completed: tombstone tracked, total builders={_tombstoneBuilders.Count}");
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
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Hex1bWidget>> configure)
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
        Func<RootContext, Hex1bWidget>? widgetBuilder = null;
        bool configureInvoked = false;

        Func<RootContext, Hex1bWidget> wrappedBuilder = ctx =>
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
        Func<RootContext, Hex1bWidget> yieldBuilder,
        int width,
        int maxHeight)
    {
        // First, measure the yield widget to determine its natural height.
        var measuredHeight = MeasureYieldHeight(yieldBuilder, width, maxHeight * 10);
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
                _flowOriginRow = Math.Max(0, _flowOriginRow - overflow);
            }

            // Clear the page region
            ClearRegion(_cursorRow, pageHeight);

            // Render a step of the yield content at the current offset
            int offset = totalRendered;
            await RenderYieldPageAsync(ctx =>
            {
                // Build a wrapper that skips the first 'offset' rows and takes 'pageHeight'
                var fullWidget = yieldBuilder(ctx);
                return fullWidget;
            }, width, pageHeight, offset);

            _cursorRow += pageHeight;
            totalRendered += pageHeight;
            remainingLines -= pageHeight;
        }

        return totalRendered;
    }

    /// <summary>
    /// Measures the natural height of a yield widget tree.
    /// </summary>
    private int MeasureYieldHeight(Func<RootContext, Hex1bWidget> yieldBuilder, int width, int maxHeight)
    {
        try
        {
            var rootCtx = new RootContext();
            var widget = yieldBuilder(rootCtx);
            if (widget == null) return 1;

            var reconcileCtx = ReconcileContext.CreateRoot();
            var nodeTask = widget.ReconcileAsync(null, reconcileCtx);
            if (!nodeTask.IsCompleted) return 1;

            var node = nodeTask.Result;
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
        Func<RootContext, Hex1bWidget> yieldBuilder,
        int width,
        int height,
        int skipRows = 0)
    {
        Func<RootContext, Hex1bWidget> actualBuilder;
        if (skipRows > 0)
        {
            actualBuilder = ctx =>
            {
                var widget = yieldBuilder(ctx);
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
                var widget = actualBuilder(ctx);
                if (!rendered)
                {
                    rendered = true;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(50);
                        yieldApp?.RequestStop();
                    });
                }
                return widget;
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

    /// <summary>
    /// Renders a yield builder into a freshly-allocated <see cref="Surface"/>.
    /// Returns <c>null</c> if reconciliation, measurement, or rendering fails
    /// — callers should fall back to the legacy emission path in that case.
    /// </summary>
    /// <remarks>
    /// Used only on the soft-wrap tombstone path
    /// (<see cref="Hex1bFlowOptions.UseSoftWrapTombstones"/>). The surface is
    /// sized to <paramref name="width"/> by the widget's measured height
    /// (clamped to <paramref name="maxHeight"/> × 10 to bound page-by-page
    /// content). The surface is then arranged and rendered using the standard
    /// rendering pipeline so any widget that works on screen will work here.
    /// </remarks>
    private Surface? RenderToSurface(
        Func<RootContext, Hex1bWidget> builder,
        int width,
        int maxHeight)
    {
        try
        {
            var rootCtx = new RootContext();
            var widget = builder(rootCtx);
            if (widget == null) return null;

            var reconcileCtx = ReconcileContext.CreateRoot();
            var nodeTask = widget.ReconcileAsync(null, reconcileCtx);
            // Reconciliation should complete synchronously for the widgets
            // used in flow tombstones today; if it doesn't, defer to the
            // legacy renderer which has its own measurement fallback.
            if (!nodeTask.IsCompleted) return null;

            var node = nodeTask.Result;
            if (node == null) return null;

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
        catch
        {
            return null;
        }
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

        Trace($"EmitSoftWrapTombstone[#{emitId}] enter: surfaceSize={surface.Width}x{height} cursorRow={_cursorRow} flowOriginRow={_flowOriginRow} termH={terminalHeight}");

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
            _flowOriginRow = Math.Max(0, _flowOriginRow - overflow);
            Trace($"EmitSoftWrapTombstone[#{emitId}] pre-scroll: overflow={overflow} -> cursorRow={_cursorRow} flowOriginRow={_flowOriginRow}");
        }

        // Position the cursor at the row where the tombstone should land.
        _parentAdapter.SetCursorPosition(0, _cursorRow);

        SoftWrapEmitter.Emit(surface, _parentAdapter);

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
        Trace($"EmitSoftWrapTombstone[#{emitId}] exit: cursorRow={_cursorRow} flowOriginRow={_flowOriginRow}");
    }

    /// <summary>
    /// Resize-time owned re-render: clears the entire visible viewport,
    /// re-emits every tracked tombstone at the new terminal width starting
    /// at the top of the screen, and computes the row origin where the
    /// active step should be anchored. Called only on the soft-wrap path.
    /// Returns the row origin for the active step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The previous resize strategy ("clear from <see cref="_flowOriginRow"/>
    /// down and trust our row tracking to be in sync") had an unfixable
    /// problem on terminals that reflow logical lines on horizontal resize
    /// (Windows Terminal, iTerm2, modern xterm builds): tombstones are
    /// emitted as logical lines via <see cref="SoftWrapEmitter"/> precisely
    /// so that they reflow naturally, but reflow also moves them around
    /// vertically — what was at row N before the resize might now be at
    /// row M after the resize. <see cref="_flowOriginRow"/> tracked our
    /// own pre-scrolls but had no way to learn about reflow-induced row
    /// shifts. The result was that <c>SetCursorPosition(0, _flowOriginRow)
    /// + ESC[J</c> sometimes left terminal-reflowed copies of the
    /// tombstones above the cursor, and our re-emitted copies landed
    /// below them — duplicate tombstones on screen.
    /// </para>
    /// <para>
    /// To make the resize repaint deterministic regardless of host
    /// terminal reflow behaviour, this method now wipes the entire
    /// visible viewport (<c>ESC[H ESC[2J</c>) and redraws the tombstone
    /// history starting at row 0 of the visible area. Any user content
    /// that was visible above the flow before the resize (typically a
    /// shell prompt) is therefore lost from the visible area on the
    /// first resize. It is preserved in the host terminal's scrollback
    /// (the host has already reflowed it there as part of its own resize
    /// handling), so the user can still scroll up to see it. This is a
    /// deliberate trade-off: a one-time loss of the prompt from view is
    /// less disruptive than ghost copies of every tombstone stacking up
    /// each time the user resizes.
    /// </para>
    /// <para>
    /// The clear+redraw is bracketed in DEC private mode 2026
    /// (Synchronized Update Mode), so terminals that recognise it present
    /// the entire repaint as one atomic frame instead of a brief blank
    /// flash. Terminals that ignore mode 2026 see a slightly more visible
    /// repaint but no functional regression.
    /// </para>
    /// <para>
    /// <see cref="EmitSoftWrapTombstone"/>'s pre-scroll logic handles
    /// overflow into the host terminal's scrollback the same way it does
    /// during normal flow execution, so the user's mental model — "oldest
    /// tombstones go up into scrollback as new ones land at the bottom" —
    /// is preserved across resize.
    /// </para>
    /// </remarks>
    private int ReRenderTombstonesAndAnchorStep(int newWidth, int newHeight, int newStepHeight)
    {
        var resizeId = Interlocked.Increment(ref _resizeCounter);
        Trace($"ReRender[#{resizeId}] enter: newSize={newWidth}x{newHeight} newStepH={newStepHeight} cursorRow={_cursorRow} flowOriginRow={_flowOriginRow} builders={_tombstoneBuilders.Count}");

        // Begin synchronized update so supporting terminals present the whole
        // clear-and-redraw as a single atomic frame.
        _parentAdapter.Write(SyncUpdateBegin);

        try
        {
            // Wipe the entire visible viewport. We deliberately do NOT use
            // _flowOriginRow + ESC[J here: the host terminal may have
            // already reflowed our tombstones to a different row range
            // before this handler runs, and our row tracking has no way
            // to learn about that. ESC[H ESC[2J unconditionally homes the
            // cursor and clears the visible area, giving us a known-good
            // starting state. Anything previously visible above the flow
            // is preserved in scrollback (the host terminal puts it
            // there during its own resize handling).
            _parentAdapter.Write("\x1b[H\x1b[2J");
            _cursorRow = 0;
            _flowOriginRow = 0;
            Trace($"ReRender[#{resizeId}] cleared full viewport, reset to row 0");

            // Re-emit every tracked tombstone at the new width starting at
            // row 0. The emitter's pre-scroll logic naturally pushes
            // earlier tombstones into scrollback when the viewport can't
            // hold them all, matching the behaviour of normal flow
            // execution.
            for (int i = 0; i < _tombstoneBuilders.Count; i++)
            {
                var builder = _tombstoneBuilders[i];
                var surface = RenderToSurface(builder, newWidth, newHeight);
                if (surface is null)
                {
                    Trace($"ReRender[#{resizeId}] tombstone[{i}] surface=NULL, skipping");
                    continue;
                }
                Trace($"ReRender[#{resizeId}] tombstone[{i}] re-emit: size={surface.Width}x{surface.Height}");
                EmitSoftWrapTombstone(surface);
            }

            // Pre-scroll if the active step won't fit below the last
            // tombstone. After this, _cursorRow + newStepHeight <= newHeight
            // is guaranteed, so the step region we hand back is fully on
            // screen.
            var overflow = (_cursorRow + newStepHeight) - newHeight;
            if (overflow > 0)
            {
                _parentAdapter.SetCursorPosition(0, newHeight - 1);
                for (int i = 0; i < overflow; i++)
                {
                    _parentAdapter.Write("\n");
                }
                _cursorRow -= overflow;
                _flowOriginRow = Math.Max(0, _flowOriginRow - overflow);
                Trace($"ReRender[#{resizeId}] step pre-scroll: overflow={overflow} -> cursorRow={_cursorRow} flowOriginRow={_flowOriginRow}");
            }

            // Clear the active-step region so the step app's first
            // post-resize render lands on a clean canvas. Without this the
            // tombstone renderer's last emitted glyph would still be visible
            // in column 0 of the step's first row until overwritten.
            ClearRegion(_cursorRow, newStepHeight);

            Trace($"ReRender[#{resizeId}] exit: returning rowOrigin={_cursorRow} flowOriginRow={_flowOriginRow}");
            return _cursorRow;
        }
        finally
        {
            _parentAdapter.Write(SyncUpdateEnd);
        }
    }

    /// <summary>
    /// Pumps output from a step adapter to the parent adapter.
    /// </summary>
    private async Task PumpStepOutputAsync(InlineStepAdapter stepAdapter, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await stepAdapter.ReadOutputAsync(ct);
                if (data.IsEmpty) continue;
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
    /// Queries the current cursor row position using Device Status Report (DSR).
    /// Falls back to bottom of terminal if query fails.
    /// </summary>
    private Task<int> QueryCursorRowAsync(CancellationToken ct)
    {
        // Use the initial cursor row from options if provided
        return Task.FromResult(_options.InitialCursorRow ?? 0);
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
}
