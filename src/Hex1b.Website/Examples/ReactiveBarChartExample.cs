using System.ComponentModel;
using Hex1b;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Demonstrates reactive rendering with external events (timer-based updates).
/// Shows a bar chart that updates automatically every second without user input.
/// </summary>
public class ReactiveBarChartExample(ILogger<ReactiveBarChartExample> logger) : ReactiveExample
{
    private readonly ILogger<ReactiveBarChartExample> _logger = logger;

    public override string Id => "reactive-bar-chart";
    public override string Title => "Reactive Bar Chart";
    public override string Description => "Bar chart that updates automatically via timer - demonstrates Invalidate() and INotifyPropertyChanged.";

    /// <summary>
    /// Observable state that triggers re-renders when properties change.
    /// </summary>
    private class ChartState : INotifyPropertyChanged
    {
        private readonly Random _random = new();
        private int[] _values = [50, 30, 70, 45, 60, 35, 80];
        private int[] _directions = [1, -1, 1, -1, 1, -1, 1]; // 1 = up, -1 = down
        private int _updateCount;
        
        private const int MinValue = 5;
        private const int MaxValue = 100;
        private const int StepSize = 5;
        private const double MomentumChance = 0.75; // 75% chance to continue direction
        
        public event PropertyChangedEventHandler? PropertyChanged;

        public string[] Labels { get; } = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        
        public int[] Values
        {
            get => _values;
            private set
            {
                _values = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
            }
        }
        
        public int UpdateCount
        {
            get => _updateCount;
            private set
            {
                _updateCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateCount)));
            }
        }

        /// <summary>
        /// Updates values with momentum-based movement.
        /// Each bar has a 75% chance to continue its current direction.
        /// </summary>
        public void RandomizeValues()
        {
            var newValues = new int[Values.Length];
            for (int i = 0; i < newValues.Length; i++)
            {
                // Possibly flip direction (25% chance)
                if (_random.NextDouble() > MomentumChance)
                {
                    _directions[i] *= -1;
                }
                
                // Calculate new value with current direction
                var step = _random.Next(1, StepSize + 1) * _directions[i];
                var newValue = Values[i] + step;
                
                // Clamp to bounds and reverse direction if we hit a wall
                if (newValue >= MaxValue)
                {
                    newValue = MaxValue;
                    _directions[i] = -1; // Force downward
                }
                else if (newValue <= MinValue)
                {
                    newValue = MinValue;
                    _directions[i] = 1; // Force upward
                }
                
                newValues[i] = newValue;
            }
            Values = newValues;
            UpdateCount++;
        }
    }

    public override async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting reactive bar chart example");

        var state = new ChartState();

        // Create the app with our observable state
        using var app = new Hex1bApp(
            ctx => BuildChart(ctx, state),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workloadAdapter,
                Theme = Hex1bThemes.Ocean
            }
        );

        // Start the timer that updates state every 200ms
        // Since we removed INotifyPropertyChanged auto-subscription, we need to invalidate manually
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        var timerTask = RunTimerAsync(timer, state, app, cancellationToken);

        // Run the app (this blocks until cancelled)
        var appTask = app.RunAsync(cancellationToken);

        // Wait for either to complete
        await Task.WhenAny(appTask, timerTask);
    }

    private async Task RunTimerAsync(PeriodicTimer timer, ChartState state, Hex1bApp app, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                state.RandomizeValues();
                app.Invalidate(); // Trigger re-render
                _logger.LogDebug("Updated chart values (update #{Count})", state.UpdateCount);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private static Hex1bWidget BuildChart(RootContext ctx, ChartState state)
    {
        return ctx.VStack(v =>
        {
            var widgets = new List<Hex1bWidget>
            {
                v.Text("╔════════════════════════════════════════════════════╗"),
                v.Text("║         📊 REACTIVE BAR CHART DEMO                 ║"),
                v.Text("║    Demonstrates INotifyPropertyChanged + Timer     ║"),
                v.Text("╚════════════════════════════════════════════════════╝"),
                v.Text(""),
                v.Text($"  Updates: {state.UpdateCount}  |  Bars update every second automatically"),
                v.Text(""),
                v.Text("  ┌─────────────────────────────────────────────────┐")
            };

            // Build the bar chart rows
            var maxValue = 100;
            var barMaxWidth = 40;
            
            // Bar colors for each day
            var colors = new[] { "🟦", "🟩", "🟨", "🟧", "🟥", "🟪", "⬜" };
            
            for (int i = 0; i < state.Labels.Length; i++)
            {
                var label = state.Labels[i];
                var value = state.Values[i];
                var barWidth = (int)((double)value / maxValue * barMaxWidth);
                var bar = new string('█', barWidth);
                var padding = new string(' ', barMaxWidth - barWidth);
                
                // Use different block character based on index for visual variety
                var colorBlock = i switch
                {
                    0 => "\x1b[94m", // Bright Blue
                    1 => "\x1b[92m", // Bright Green
                    2 => "\x1b[93m", // Bright Yellow
                    3 => "\x1b[33m", // Orange/Yellow
                    4 => "\x1b[91m", // Bright Red
                    5 => "\x1b[95m", // Bright Magenta
                    6 => "\x1b[97m", // Bright White
                    _ => "\x1b[0m"
                };
                var reset = "\x1b[0m";
                
                widgets.Add(v.Text($"  │ {label} │{colorBlock}{bar}{reset}{padding}│ {value,3}%"));
            }

            widgets.Add(v.Text("  └─────────────────────────────────────────────────┘"));
            widgets.Add(v.Text(""));
            widgets.Add(v.Text("  Bars move with momentum (75% chance to continue direction)"));
            widgets.Add(v.Text(""));
            widgets.Add(v.Text("  ─────────────────────────────────────────────────────"));
            widgets.Add(v.Text("  HOW IT WORKS:"));
            widgets.Add(v.Text("  • Timer updates state.Values every 200ms"));
            widgets.Add(v.Text("  • Timer calls app.Invalidate() to trigger re-render"));
            widgets.Add(v.Text("  • Each bar has momentum - tends to keep moving up/down"));
            widgets.Add(v.Text("  • State is captured in closure, no generic TState needed"));
            widgets.Add(v.Text("  ─────────────────────────────────────────────────────"));

            return widgets.ToArray();
        });
    }
}
