using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Hex1b;
using Hex1b.Kgp;

namespace WpfTerm;

public partial class MainWindow : Window
{
    private Hex1bTerminal? _terminal;
    private WpfTerminalAdapter? _adapter;
    private KgpPipeServer? _kgpPipeServer;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Remove the 1px white border that Windows draws on borderless windows
        if (PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource hwnd)
        {
            hwnd.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Black;
        }
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

        // ConPTY backend selection:
        // - Default: OS kernel32 ConPTY (most compatible)
        // - Set HEX1B_CONPTY=custom to use VS Code's conpty.dll (enables KGP passthrough but may break some apps)
        var useCustomConpty = Environment.GetEnvironmentVariable("HEX1B_CONPTY") == "custom";
        var conptyDll = useCustomConpty ? FindConptyDll() : null;

        // KGP side-channel pipe — always available as fallback for KGP when using kernel32 ConPTY
        _kgpPipeServer = new KgpPipeServer();
        _kgpPipeServer.Start();

        _terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(_adapter)
            .WithPtyProcess(options =>
            {
                options.FileName = pwshPath;
                options.Arguments = ["-NoLogo"];
                options.WindowsPtyMode = WindowsPtyMode.Direct;
                if (conptyDll != null)
                    options.ConptyDllPath = conptyDll;
                options.Environment ??= new Dictionary<string, string>();
                options.Environment["HEX1B_KGP_PIPE"] = _kgpPipeServer.PipeName;
            })
            .WithScrollback(10000)
            .WithDimensions(_adapter.Width, _adapter.Height)
            .Build();

        // Wire pipe server to terminal — KGP tokens from pipe get processed
        _kgpPipeServer.SetTokenHandler(token => _terminal.ProcessKgpFromSideChannel(token));

        Terminal.Attach(_adapter);

        // Sync title bar color with terminal's top row background
        _adapter.OutputReceived += () => Dispatcher.BeginInvoke(SyncTitleBarColor);

        var backend = conptyDll != null ? "conpty.dll" : "kernel32";
        TitleText.Text = $"WpfTerm [{backend}]";

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

        if (_kgpPipeServer != null)
        {
            await _kgpPipeServer.DisposeAsync();
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

    private static string? FindConptyDll()
    {
        // 1. Next to our exe
        var local = Path.Combine(AppContext.BaseDirectory, "conpty", "conpty.dll");
        if (File.Exists(local)) return local;

        // 2. Search VS Code installations for the bundled conpty.dll
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] vsCodePaths =
        [
            Path.Combine(localAppData, "Programs", "Microsoft VS Code Insiders"),
            Path.Combine(localAppData, "Programs", "Microsoft VS Code"),
        ];

        foreach (var vsCodeRoot in vsCodePaths)
        {
            if (!Directory.Exists(vsCodeRoot)) continue;
            try
            {
                var matches = Directory.GetFiles(vsCodeRoot, "conpty.dll", SearchOption.AllDirectories);
                // Find the one inside node-pty (not conpty.node)
                var nodePtyDll = matches.FirstOrDefault(p =>
                    p.Contains("node-pty", StringComparison.OrdinalIgnoreCase) &&
                    p.Contains("conpty\\conpty.dll", StringComparison.OrdinalIgnoreCase));
                if (nodePtyDll != null) return nodePtyDll;
            }
            catch { }
        }

        return null;
    }

    // === Custom title bar ===

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    /// <summary>
    /// Samples the dominant background color from the top row of the terminal
    /// buffer and applies it to the title bar.
    /// </summary>
    private void SyncTitleBarColor()
    {
        if (_adapter == null) return;

        _adapter.RenderUnderLock((buffer, width, height, _, _, _, _) =>
        {
            if (height == 0 || width == 0) return;

            // Count background colors in the top row
            var colorCounts = new Dictionary<uint, int>();
            for (int x = 0; x < width; x++)
            {
                var cell = buffer[0, x];
                var bg = cell.IsReverse ? cell.Foreground : cell.Background;
                uint key = bg.HasValue && !bg.Value.IsDefault
                    ? ((uint)bg.Value.R << 16) | ((uint)bg.Value.G << 8) | bg.Value.B
                    : 0x1E1E1E; // default background
                colorCounts[key] = colorCounts.GetValueOrDefault(key) + 1;
            }

            // Pick the most common color
            uint dominant = 0x1E1E1E;
            int maxCount = 0;
            foreach (var (color, count) in colorCounts)
            {
                if (count > maxCount) { dominant = color; maxCount = count; }
            }

            var r = (byte)((dominant >> 16) & 0xFF);
            var g = (byte)((dominant >> 8) & 0xFF);
            var b = (byte)(dominant & 0xFF);

            Dispatcher.BeginInvoke(() =>
            {
                TitleBar.Background = new SolidColorBrush(Color.FromRgb(r, g, b));

                // Adjust title text brightness for readability
                double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
                TitleText.Foreground = new SolidColorBrush(luminance > 0.5
                    ? Color.FromRgb(0x33, 0x33, 0x33)
                    : Color.FromRgb(0x99, 0x99, 0x99));
            });
        });
    }
}
