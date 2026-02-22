using System.Text;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Orchestrates a flow — consuming sequential steps (slices and full-screen apps)
/// and managing the visual stack of yield widgets in the normal terminal buffer.
/// </summary>
internal sealed class Hex1bFlowRunner
{
    private readonly Func<Hex1bFlowContext, Task> _flowCallback;
    private readonly Hex1bFlowOptions _options;
    private readonly IHex1bAppTerminalWorkloadAdapter _parentAdapter;

    // Current cursor row in the terminal buffer (0-based, relative to terminal top)
    private int _cursorRow;

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
    /// Runs the entire flow from start to finish.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        // Query the current cursor position using DSR (Device Status Report)
        _cursorRow = await QueryCursorRowAsync(ct);

        var context = new Hex1bFlowContext(this);
        await _flowCallback(context);

        // After flow completes, position cursor below the last yield widget
        _parentAdapter.SetCursorPosition(0, _cursorRow);
        _parentAdapter.Write("\x1b[?25h"); // Ensure cursor is visible
    }

    /// <summary>
    /// Runs an inline slice — a micro-TUI in the normal buffer.
    /// </summary>
    internal async Task RunSliceAsync(
        Func<RootContext, Hex1bWidget> builder,
        Func<RootContext, Hex1bWidget>? yieldBuilder,
        Hex1bFlowSliceOptions? options)
    {
        await RunSliceInternalAsync(builder, null, yieldBuilder, options);
    }

    /// <summary>
    /// Runs an inline slice with app access for programmatic control.
    /// </summary>
    internal async Task RunSliceAsync(
        Func<Hex1bApp, Func<RootContext, Hex1bWidget>> configure,
        Func<RootContext, Hex1bWidget>? yieldBuilder,
        Hex1bFlowSliceOptions? options)
    {
        await RunSliceInternalAsync(null, configure, yieldBuilder, options);
    }

    private async Task RunSliceInternalAsync(
        Func<RootContext, Hex1bWidget>? builder,
        Func<Hex1bApp, Func<RootContext, Hex1bWidget>>? configure,
        Func<RootContext, Hex1bWidget>? yieldBuilder,
        Hex1bFlowSliceOptions? options)
    {
        var terminalWidth = _parentAdapter.Width;
        var terminalHeight = _parentAdapter.Height;

        // The slice wants MaxHeight rows (or full terminal height if unspecified),
        // capped to the terminal height since that's the max visible area.
        var desiredHeight = Math.Min(options?.MaxHeight ?? terminalHeight, terminalHeight);
        if (desiredHeight < 1) desiredHeight = 1;

        // Track the row origin for this slice (may be updated on resize)
        var rowOrigin = _cursorRow;

        // Scroll the terminal if the cursor is too far down to fit the slice
        var overflow = (rowOrigin + desiredHeight) - terminalHeight;
        if (overflow > 0)
        {
            _parentAdapter.SetCursorPosition(0, terminalHeight - 1);
            for (int i = 0; i < overflow; i++)
            {
                _parentAdapter.Write("\n");
            }
            // Adjust cursor row after scroll (frozen yields are already on screen)
            rowOrigin -= overflow;
            _cursorRow = rowOrigin;
        }

        // Clear the slice region so leftover characters from previous slices don't bleed through
        ClearRegion(rowOrigin, desiredHeight);

        // Create the inline adapter for this slice
        var sliceEnableMouse = options?.EnableMouse ?? false;
        var sliceCapabilities = _parentAdapter.Capabilities;
        if (sliceEnableMouse && !sliceCapabilities.SupportsMouse)
        {
            // Override capabilities to enable mouse for this slice even if the
            // parent terminal didn't request it globally.
            sliceCapabilities = sliceCapabilities with { SupportsMouse = true };
        }

        using var sliceAdapter = new InlineSliceAdapter(
            terminalWidth, desiredHeight, rowOrigin,
            sliceCapabilities);

        // Create the Hex1bApp with the inline adapter
        var appOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = sliceAdapter,
            EnableMouse = options?.EnableMouse ?? false,
            EnableDefaultCtrlCExit = true,
        };

        if (_options.Theme != null)
        {
            appOptions.Theme = _options.Theme;
        }

        // Pump output from slice adapter to parent adapter
        using var outputPumpCts = new CancellationTokenSource();
        var outputPumpTask = PumpSliceOutputAsync(sliceAdapter, outputPumpCts.Token);

        // Pump input from parent adapter to slice adapter, with resize handling
        using var inputPumpCts = new CancellationTokenSource();
        var inputPumpTask = PumpSliceInputAsync(sliceAdapter, inputPumpCts.Token,
            onResize: (newWidth, newHeight) =>
            {
                // Recalculate slice dimensions after terminal resize
                var newSliceHeight = Math.Min(options?.MaxHeight ?? newHeight, newHeight);
                if (newSliceHeight < 1) newSliceHeight = 1;

                // The slice anchors to the bottom of the terminal.
                // After reflow, assume content above was reflowed and the slice
                // should be repositioned at the bottom.
                var newRowOrigin = Math.Max(0, newHeight - newSliceHeight);

                // Clear the entire visible area below where we think we are —
                // reflow may have left artifacts anywhere.
                ClearRegion(0, newHeight);

                // Update the adapter's row origin so ANSI rewrites use the new position
                sliceAdapter.RowOrigin = newRowOrigin;
                rowOrigin = newRowOrigin;
                _cursorRow = newRowOrigin;
                desiredHeight = newSliceHeight;

                // Forward the resize with the slice height to the adapter
                _ = sliceAdapter.ResizeAsync(newWidth, newSliceHeight);
            });

        try
        {
            if (configure != null)
            {
                // Configure pattern: pass app reference to the callback
                Hex1bApp? app = null;
                Func<RootContext, Hex1bWidget>? widgetBuilder = null;
                bool configureInvoked = false;

                Func<RootContext, Hex1bWidget> wrappedBuilder = ctx =>
                {
                    if (!configureInvoked)
                    {
                        configureInvoked = true;
                        widgetBuilder = configure(app!);
                    }
                    return widgetBuilder!(ctx);
                };

                app = new Hex1bApp(wrappedBuilder, appOptions);
                await using (app)
                {
                    await app.RunAsync(default);
                }
            }
            else
            {
                await using var app = new Hex1bApp(builder!, appOptions);
                await app.RunAsync(default);
            }
        }
        finally
        {
            // Stop the I/O pumps
            outputPumpCts.Cancel();
            inputPumpCts.Cancel();

            try { await outputPumpTask; } catch (OperationCanceledException) { }
            try { await inputPumpTask; } catch (OperationCanceledException) { }
        }

        // Clear the slice region so remnants of the interactive widget don't show
        // through the (typically much smaller) yield widget.
        ClearRegion(_cursorRow, desiredHeight);

        // After slice completes, render the yield widget as frozen output (if provided)
        if (yieldBuilder != null)
        {
            var yieldHeight = await RenderYieldWidgetAsync(yieldBuilder, terminalWidth, desiredHeight);
            _cursorRow += yieldHeight;
        }
        else
        {
            // No yield widget — just advance cursor past the slice region
            _cursorRow += desiredHeight;
        }
    }

    /// <summary>
    /// Runs a full-screen TUI application in the alternate screen buffer.
    /// </summary>
    internal async Task RunFullScreenAsync(
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

            // Render a slice of the yield content at the current offset
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

        using var yieldAdapter = new InlineSliceAdapter(
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
        var pumpTask = PumpSliceOutputAsync(yieldAdapter, pumpCts.Token);

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
    /// Pumps output from a slice adapter to the parent adapter.
    /// </summary>
    private async Task PumpSliceOutputAsync(InlineSliceAdapter sliceAdapter, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await sliceAdapter.ReadOutputAsync(ct);
                if (data.IsEmpty) continue;
                _parentAdapter.Write(Encoding.UTF8.GetString(data.Span));
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Pumps input events from the parent adapter to a slice adapter.
    /// Intercepts resize events to recalculate the slice position.
    /// </summary>
    private async Task PumpSliceInputAsync(
        InlineSliceAdapter sliceAdapter,
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
                        await sliceAdapter.WriteInputEventAsync(evt, ct);
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
    /// Theme for all slices and full-screen apps in the flow.
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
