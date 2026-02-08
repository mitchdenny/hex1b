using System.CommandLine;
using Hex1b.Tool.Commands;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootCommand = Hex1b.Tool.Commands.RootCommand;

namespace Hex1b.Tool;

public class Program
{
    internal static async Task<IHost> BuildApplication(string[] args)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        // Logging — only to stderr, never stdout (TUI apps use stdout for rendering)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Infrastructure services
        builder.Services.AddSingleton<TerminalClient>();
        builder.Services.AddSingleton<TerminalDiscovery>();
        builder.Services.AddSingleton<TerminalIdResolver>();
        builder.Services.AddSingleton<OutputFormatter>();

        // Terminal commands
        builder.Services.AddTransient<Commands.Terminal.TerminalListCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalStartCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalStopCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalInfoCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalResizeCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalCleanCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalAttachCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalHostCommand>();
        builder.Services.AddTransient<Commands.Terminal.TerminalCommand>();

        // App commands
        builder.Services.AddTransient<Commands.App.AppTreeCommand>();
        builder.Services.AddTransient<Commands.App.AppCommand>();

        // Top-level commands
        builder.Services.AddTransient<Commands.KeysCommand>();
        builder.Services.AddTransient<Commands.AssertCommand>();

        // Capture commands
        builder.Services.AddTransient<Commands.Capture.CaptureScreenshotCommand>();
        builder.Services.AddTransient<Commands.Capture.CaptureRecordingStartCommand>();
        builder.Services.AddTransient<Commands.Capture.CaptureRecordingStopCommand>();
        builder.Services.AddTransient<Commands.Capture.CaptureRecordingStatusCommand>();
        builder.Services.AddTransient<Commands.Capture.CaptureRecordingPlaybackCommand>();
        builder.Services.AddTransient<Commands.Capture.CaptureRecordingCommand>();
        builder.Services.AddTransient<Commands.Capture.CaptureCommand>();

        // Mouse commands
        builder.Services.AddTransient<Commands.Mouse.MouseClickCommand>();
        builder.Services.AddTransient<Commands.Mouse.MouseDragCommand>();
        builder.Services.AddTransient<Commands.Mouse.MouseCommand>();

        // Agent commands
        builder.Services.AddTransient<Commands.Agent.AgentMcpCommand>();
        builder.Services.AddTransient<Commands.Agent.AgentInitCommand>();
        builder.Services.AddTransient<Commands.Agent.AgentCommand>();

        // Root command
        builder.Services.AddTransient<RootCommand>();

        return builder.Build();
    }

    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };

        using var app = await BuildApplication(args);
        await app.StartAsync(cts.Token);

        var rootCommand = app.Services.GetRequiredService<RootCommand>();
        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync(cancellationToken: cts.Token);
    }
}
