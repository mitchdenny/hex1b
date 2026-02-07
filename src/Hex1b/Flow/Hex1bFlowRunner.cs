using System.Text;
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

    // Visual stack of completed yield widgets
    private readonly List<YieldEntry> _yieldStack = new();

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
        var terminalWidth = _parentAdapter.Width;
        var terminalHeight = _parentAdapter.Height;
        var maxHeight = options?.MaxHeight ?? terminalHeight;

        // Calculate available height from cursor position
        var availableHeight = Math.Min(maxHeight, terminalHeight - _cursorRow);
        if (availableHeight < 1) availableHeight = 1;

        // Reserve space by emitting newlines if needed
        var neededNewlines = (_cursorRow + availableHeight) - terminalHeight;
        if (neededNewlines > 0)
        {
            // We need to scroll the terminal to make room
            _parentAdapter.SetCursorPosition(0, terminalHeight - 1);
            for (int i = 0; i < neededNewlines; i++)
            {
                _parentAdapter.Write("\n");
            }
            // Adjust cursor row and yield stack after scroll
            _cursorRow -= neededNewlines;
            foreach (var entry in _yieldStack)
            {
                entry.TopRow -= neededNewlines;
            }
        }

        // Create the inline adapter for this slice
        using var sliceAdapter = new InlineSliceAdapter(
            terminalWidth, availableHeight, _cursorRow,
            _parentAdapter.Capabilities);

        // Create the Hex1bApp with the inline adapter
        var appOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = sliceAdapter,
            EnableMouse = false,
            EnableDefaultCtrlCExit = true,
        };

        if (_options.Theme != null)
        {
            appOptions.Theme = _options.Theme;
        }

        // Pump output from slice adapter to parent adapter
        using var outputPumpCts = new CancellationTokenSource();
        var outputPumpTask = PumpSliceOutputAsync(sliceAdapter, outputPumpCts.Token);

        // Pump input from parent adapter to slice adapter
        using var inputPumpCts = new CancellationTokenSource();
        var inputPumpTask = PumpSliceInputAsync(sliceAdapter, inputPumpCts.Token);

        try
        {
            await using var app = new Hex1bApp(builder, appOptions);
            await app.RunAsync(default);
        }
        finally
        {
            // Stop the I/O pumps
            outputPumpCts.Cancel();
            inputPumpCts.Cancel();

            try { await outputPumpTask; } catch (OperationCanceledException) { }
            try { await inputPumpTask; } catch (OperationCanceledException) { }
        }

        // After slice completes, render the yield widget (if provided)
        if (yieldBuilder != null)
        {
            var yieldHeight = await RenderYieldWidgetAsync(yieldBuilder, terminalWidth, availableHeight);
            _yieldStack.Add(new YieldEntry
            {
                Builder = yieldBuilder,
                TopRow = _cursorRow,
                Height = yieldHeight,
            });
            _cursorRow += yieldHeight;
        }
        else
        {
            // No yield widget — just advance cursor past the slice region
            _cursorRow += availableHeight;
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

        // After returning from full-screen, re-render visible yield widgets
        await ReRenderYieldStackAsync();
    }

    /// <summary>
    /// Renders a yield widget and returns its height.
    /// </summary>
    private async Task<int> RenderYieldWidgetAsync(
        Func<RootContext, Hex1bWidget> yieldBuilder,
        int width,
        int maxHeight)
    {
        // For the spike, render the yield widget using a minimal Hex1bApp
        // that runs for exactly one frame, then exits.
        // We render it at the current cursor position.
        var yieldHeight = Math.Min(3, maxHeight); // Simple heuristic for spike

        // Clear the slice region and render yield content
        var sb = new StringBuilder();
        for (int row = 0; row < yieldHeight; row++)
        {
            sb.Append($"\x1b[{_cursorRow + row + 1};1H");
            sb.Append("\x1b[2K");
        }
        _parentAdapter.Write(sb.ToString());

        // For the spike: use a one-shot inline adapter to render the yield widget
        using var yieldAdapter = new InlineSliceAdapter(
            width, yieldHeight, _cursorRow,
            _parentAdapter.Capabilities);

        var yieldOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = yieldAdapter,
            EnableMouse = false,
            EnableDefaultCtrlCExit = false,
        };

        if (_options.Theme != null)
        {
            yieldOptions.Theme = _options.Theme;
        }

        // Pump output for the single render frame
        var pumpCts = new CancellationTokenSource();
        var pumpTask = PumpSliceOutputAsync(yieldAdapter, pumpCts.Token);

        try
        {
            // Create app that renders once and stops
            Hex1bApp? yieldApp = null;
            bool rendered = false;

            yieldApp = new Hex1bApp(ctx =>
            {
                var widget = yieldBuilder(ctx);
                if (!rendered)
                {
                    rendered = true;
                    // Schedule stop after first render
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(50); // Allow frame to flush
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

        return yieldHeight;
    }

    /// <summary>
    /// Re-renders the visible yield widget stack after returning from full-screen mode.
    /// </summary>
    private Task ReRenderYieldStackAsync()
    {
        // After exiting alternate screen, the terminal restores the normal buffer
        // which already contains the yield widgets. No re-rendering needed for now.
        // TODO: Re-render on resize or if content was corrupted
        return Task.CompletedTask;
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
    /// </summary>
    private async Task PumpSliceInputAsync(InlineSliceAdapter sliceAdapter, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await _parentAdapter.InputEvents.WaitToReadAsync(ct))
                {
                    while (_parentAdapter.InputEvents.TryRead(out var evt))
                    {
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

    private sealed class YieldEntry
    {
        public required Func<RootContext, Hex1bWidget> Builder { get; init; }
        public int TopRow { get; set; }
        public required int Height { get; init; }
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
