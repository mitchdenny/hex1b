using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
{
    await Console.Error.WriteLineAsync("Hex1b.Analyzer requires Linux or macOS.");
    return 1;
}

// Parse command-line arguments
var (command, commandArgs, port) = ParseArgs(args);

if (string.IsNullOrEmpty(command))
{
    PrintUsage();
    return 1;
}

var width = Console.WindowWidth > 0 ? Console.WindowWidth : 120;
var height = Console.WindowHeight > 0 ? Console.WindowHeight : 40;

try
{
    // Launch the specified command with passthrough
    await using var process = new Hex1bTerminalChildProcess(
        command,
        commandArgs,
        workingDirectory: Environment.CurrentDirectory,
        inheritEnvironment: true,
        initialWidth: width,
        initialHeight: height
    );

    var presentation = new ConsolePresentationAdapter(enableMouse: false);

    var terminalOptions = new Hex1bTerminalOptions
    {
        Width = width,
        Height = height,
        PresentationAdapter = presentation,
        WorkloadAdapter = process
    };

    using var terminal = new Hex1bTerminal(terminalOptions);
    await process.StartAsync();

    // Start the web server in the background
    using var cts = new CancellationTokenSource();
    var webTask = StartWebServerAsync(terminal, port, cts.Token);

    // Wait for process to exit
    try
    {
        await process.WaitForExitAsync(CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        process.Kill();
    }

    // Stop the web server
    await cts.CancelAsync();

    try
    {
        await webTask;
    }
    catch (OperationCanceledException)
    {
        // Expected when cancelled
    }
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    return 1;
}

return 0;

static (string Command, string[] Args, int Port) ParseArgs(string[] args)
{
    var port = 5050; // Default port
    var command = "";
    var commandArgs = new List<string>();
    var parsingOptions = true;

    for (int i = 0; i < args.Length; i++)
    {
        if (parsingOptions && args[i] == "--port" && i + 1 < args.Length)
        {
            if (int.TryParse(args[i + 1], out var p) && p > 0 && p <= 65535)
            {
                port = p;
            }
            i++; // Skip the value
        }
        else if (parsingOptions && args[i] == "--")
        {
            parsingOptions = false;
        }
        else if (string.IsNullOrEmpty(command))
        {
            command = args[i];
            parsingOptions = false; // Stop parsing options after command
        }
        else
        {
            commandArgs.Add(args[i]);
        }
    }

    return (command, commandArgs.ToArray(), port);
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: hex1b-analyzer [--port PORT] <command> [args...]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  --port PORT  Port for the web server (default: 5050)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  hex1b-analyzer bash");
    Console.Error.WriteLine("  hex1b-analyzer --port 8080 /bin/bash --norc");
    Console.Error.WriteLine("  hex1b-analyzer htop");
}

static async Task StartWebServerAsync(Hex1bTerminal terminal, int port, CancellationToken ct)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = [] // Don't pass command line args to web app
    });

    // Disable all ASP.NET Core logging to prevent interference with terminal passthrough
    builder.Logging.ClearProviders();

    // Configure Kestrel to listen on the specified port
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(port);
    });

    var app = builder.Build();

    // Add the /getsvg endpoint
    app.MapGet("/getsvg", () =>
    {
        using var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg(new TerminalSvgOptions
        {
            ShowCellGrid = false
        });
        return Results.Content(svg, "image/svg+xml");
    });

    // Run the web application
    await app.RunAsync(ct);
}
