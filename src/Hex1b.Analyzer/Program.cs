using System.CommandLine;
using Hex1b.Analyzer;
using Hex1b.Analyzer.Components;
using Hex1b.Terminal.Automation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Create the root command
var rootCommand = new RootCommand("Hex1b Analyzer - Terminal passthrough with web-based monitoring");

// Create the 'run' command
var runCommand = new Command("run", "Run a command with terminal passthrough and web monitoring");

// Add options
var portOption = new Option<int>(
    name: "--port",
    getDefaultValue: () => 5050,
    description: "Port for the web server");
portOption.AddValidator(result =>
{
    var port = result.GetValueForOption(portOption);
    if (port <= 0 || port > 65535)
    {
        result.ErrorMessage = "Port must be between 1 and 65535";
    }
});

var commandArgument = new Argument<string[]>(
    name: "command",
    description: "The command and arguments to run (after --)");
commandArgument.Arity = ArgumentArity.OneOrMore;

runCommand.AddOption(portOption);
runCommand.AddArgument(commandArgument);

runCommand.SetHandler(async (int port, string[] commandParts) =>
{
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
    {
        await Console.Error.WriteLineAsync("Hex1b.Analyzer requires Linux or macOS.");
        Environment.ExitCode = 1;
        return;
    }

    if (commandParts.Length == 0)
    {
        await Console.Error.WriteLineAsync("Error: A command to run is required.");
        Environment.ExitCode = 1;
        return;
    }

    var command = commandParts[0];
    var commandArgs = commandParts.Length > 1 ? commandParts[1..] : [];

    // Create the RunCommand instance
    var runCmd = new RunCommand(port, command, commandArgs);

    // Build the web application with DI
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

    // Register services
    builder.Services.AddSingleton(runCmd);
    builder.Services.AddSingleton<TerminalHolder>();
    
    // Add Blazor services
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseStaticFiles();
    app.UseAntiforgery();

    // Add the /getsvg endpoint
    app.MapGet("/getsvg", (TerminalHolder holder) =>
    {
        var terminal = holder.Terminal;
        if (terminal == null)
        {
            return Results.Problem("Terminal not initialized");
        }
        
        using var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg(new TerminalSvgOptions
        {
            ShowCellGrid = false
        });
        return Results.Content(svg, "image/svg+xml");
    });

    // Map Blazor components
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Execute the run command
    Environment.ExitCode = await runCmd.ExecuteAsync(app);

}, portOption, commandArgument);

rootCommand.AddCommand(runCommand);

// Execute the command
return await rootCommand.InvokeAsync(args);

