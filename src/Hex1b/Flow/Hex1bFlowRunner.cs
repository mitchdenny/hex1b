using System.Text;
using Hex1b.Input;
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

    // Current cursor row in the terminal buffer (0-based, relative to terminal top)
    private int _cursorRow;

    // The currently active step, if any. Only one step may run at a time.
    private FlowStep? _activeStep;

    // CancellationToken from RunAsync, surfaced to flow callbacks via Hex1bFlowContext.
    private CancellationToken _cancellationToken;

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

        var context = new Hex1bFlowContext(this);
        await _flowCallback(context);

        // After flow completes, position cursor below the last yield widget
        _parentAdapter.SetCursorPosition(0, _cursorRow);
        _parentAdapter.Write("\x1b[?25h"); // Ensure cursor is visible
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

        var desiredHeight = Math.Min(options?.MaxHeight ?? terminalHeight, terminalHeight);
        if (desiredHeight < 1) desiredHeight = 1;

        var step = new FlowStep(terminalWidth, terminalHeight, desiredHeight);
        _activeStep = step;

        // Start the step lifecycle on a background task
        _ = RunStepLifecycleAsync(step, builder, options, desiredHeight);

        return step;
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
                    var newStepHeight = Math.Min(options?.MaxHeight ?? newHeight, newHeight);
                    if (newStepHeight < 1) newStepHeight = 1;

                    var newRowOrigin = Math.Max(0, newHeight - newStepHeight);

                    ClearRegion(0, newHeight);

                    stepAdapter.RowOrigin = newRowOrigin;
                    rowOrigin = newRowOrigin;
                    _cursorRow = newRowOrigin;
                    desiredHeight = newStepHeight;
                    step.StepHeight = newStepHeight;

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
                var completedHeight = await RenderYieldWidgetAsync(completedBuilder, terminalWidth, desiredHeight);
                _cursorRow += completedHeight;
            }
            else
            {
                _cursorRow += desiredHeight;
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
        // Build the widget to count top-level VStack children (each is 1 row of text)
        var rootCtx = new RootContext();
        var widget = yieldBuilder(rootCtx);

        // If it's a VStack, count children
        if (widget is VStackWidget vstack)
            return vstack.Children.Count;

        // Single widget = 1 row
        return 1;
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
}
