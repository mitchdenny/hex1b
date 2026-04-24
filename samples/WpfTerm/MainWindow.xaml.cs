using System.IO;
using System.Windows;
using Hex1b;

namespace WpfTerm;

public partial class MainWindow : Window
{
    private Hex1bTerminal? _terminal;
    private WpfTerminalAdapter? _adapter;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();

        // Find PowerShell
        var pwshPath = FindPwsh();
        if (pwshPath == null)
        {
            MessageBox.Show("PowerShell 7 (pwsh.exe) not found on PATH.", "WpfTerm", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        _adapter = new WpfTerminalAdapter(120, 30);

        _terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(_adapter)
            .WithPtyProcess(options =>
            {
                options.FileName = pwshPath;
                options.Arguments = ["-NoLogo", "-NoProfile"];
                options.WindowsPtyMode = WindowsPtyMode.Direct;
            })
            .WithDimensions(_adapter.Width, _adapter.Height)
            .Build();

        Terminal.Attach(_adapter);

        // Diagnostic: show KGP stats in title bar
        var diagTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        diagTimer.Tick += (_, _) =>
        {
            var placements = _adapter!.GetKgpPlacements();
            Title = $"WpfTerm — KGP tokens:{_adapter.KgpTokensReceived} placements:{placements.Count}";
        };
        diagTimer.Start();

        // Run the terminal on a background task
        _runTask = Task.Run(async () =>
        {
            try
            {
                await _terminal.RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Title = $"WpfTerm — Error: {ex.Message}";
                });
            }
        });
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        if (_terminal != null)
        {
            await _terminal.DisposeAsync();
        }

        if (_runTask != null)
        {
            try
            {
                await _runTask.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Timeout on shutdown is fine
            }
        }
    }

    private static string? FindPwsh()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(entry, "pwsh.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }
}
