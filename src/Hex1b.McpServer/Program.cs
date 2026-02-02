using Hex1b.McpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (MCP uses stdout for communication)
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register the terminal session manager as a singleton
builder.Services.AddSingleton<TerminalSessionManager>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

#if DEBUG
// Register host for debug shutdown tool
DebugTools.Host = host;
#endif

await host.RunAsync();

/// <summary>
/// Simple ping tool to verify the MCP server is working.
/// </summary>
[McpServerToolType]
public static class PingTool
{
    /// <summary>
    /// Returns a pong response to verify the server is working.
    /// </summary>
    /// <returns>A pong message with the current timestamp.</returns>
    [McpServerTool, Description("Verify the Hex1b MCP server is running and responsive.")]
    public static string Ping()
    {
        return $"pong! Hex1b.McpServer is running. Server time: {DateTime.UtcNow:O}";
    }
}

#if DEBUG
/// <summary>
/// Debug-only tools for development.
/// </summary>
[McpServerToolType]
public static class DebugTools
{
    internal static IHost? Host { get; set; }

    /// <summary>
    /// Shuts down the MCP server. Only available in DEBUG builds.
    /// </summary>
    [McpServerTool, Description("Shuts down the MCP server (DEBUG builds only). Use this to restart the server after code changes.")]
    public static string Shutdown()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(100); // Give time for response to be sent
            Host?.StopAsync();
        });
        return "Shutting down MCP server...";
    }
}
#endif
