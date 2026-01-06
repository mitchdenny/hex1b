using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace Hex1b.Analyzer;

/// <summary>
/// The run command that executes terminal passthrough with web-based monitoring.
/// This class is registered in DI and handles starting the web application and terminal process.
/// </summary>
public class RunCommand
{
    private readonly int _port;
    private readonly string _command;
    private readonly string[] _commandArgs;

    /// <summary>
    /// Creates a new RunCommand instance.
    /// </summary>
    /// <param name="port">The port for the web server.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="commandArgs">Arguments for the command.</param>
    public RunCommand(int port, string command, string[] commandArgs)
    {
        _port = port;
        _command = command;
        _commandArgs = commandArgs;
    }

    /// <summary>
    /// Gets the port number for the web server.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets the command to execute.
    /// </summary>
    public string Command => _command;

    /// <summary>
    /// Gets the command arguments.
    /// </summary>
    public string[] CommandArgs => _commandArgs;

    /// <summary>
    /// Executes the run command: starts the web server, outputs the URL with OSC 8, 
    /// then runs the terminal passthrough.
    /// </summary>
    /// <param name="app">The built WebApplication instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(WebApplication app, CancellationToken ct = default)
    {
        var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
        var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

        // Output the URL with OSC 8 hyperlink escape codes BEFORE starting terminal
        var url = $"http://localhost:{_port}";
        OutputHyperlinkUrl(url);

        try
        {
            // Launch the specified command with passthrough
            await using var process = new Hex1bTerminalChildProcess(
                _command,
                _commandArgs,
                workingDirectory: Environment.CurrentDirectory,
                inheritEnvironment: true,
                initialWidth: width,
                initialHeight: height
            );

            // Create console presentation adapter for passthrough
            var consoleAdapter = new ConsolePresentationAdapter(enableMouse: false);
            
            // Create Blazor presentation adapter for web streaming
            var blazorAdapter = new BlazorPresentationAdapter(width, height);
            
            // Store Blazor adapter for SignalR hub access
            app.Services.GetRequiredService<BlazorPresentationAdapterHolder>().Adapter = blazorAdapter;
            
            // Create multiheaded adapter that broadcasts to both
            var multiheadedAdapter = new MultiheadedPresentationAdapter(consoleAdapter, blazorAdapter);

            var terminalOptions = new Hex1bTerminalOptions
            {
                Width = width,
                Height = height,
                PresentationAdapter = multiheadedAdapter,
                WorkloadAdapter = process
            };

            // Register the terminal in DI for the web endpoints to access
            var terminal = new Hex1bTerminal(terminalOptions);
            
            // Store terminal reference for the /getsvg endpoint
            app.Services.GetRequiredService<TerminalHolder>().Terminal = terminal;

            await process.StartAsync();

            // Start the web server in the background
            using var webCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var webTask = app.RunAsync(webCts.Token);

            // Wait for process to exit
            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
            }

            // Stop the web server
            await webCts.CancelAsync();

            try
            {
                await webTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }

            terminal.Dispose();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Outputs the URL to stderr with OSC 8 hyperlink escape codes.
    /// </summary>
    private static void OutputHyperlinkUrl(string url)
    {
        // OSC 8 format: ESC ] 8 ; params ; URI ST  text  ESC ] 8 ; ; ST
        // params can be empty, ST is BEL (\x07) or ESC \
        var startLink = $"\x1b]8;;{url}\x07";
        var endLink = "\x1b]8;;\x07";
        
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Terminal analyzer running at: {startLink}{url}{endLink}");
        Console.Error.WriteLine();
    }
}

/// <summary>
/// Holder class to share the terminal instance between the command and web endpoints.
/// Thread-safe using volatile field access.
/// </summary>
public class TerminalHolder
{
    private volatile Hex1bTerminal? _terminal;

    /// <summary>
    /// The active terminal instance. Thread-safe read/write.
    /// </summary>
    public Hex1bTerminal? Terminal
    {
        get => _terminal;
        set => _terminal = value;
    }
}
