using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hex1b.McpServer.Tests;

// Alias to resolve namespace conflict with Hex1b.McpServer namespace
using MCP = ModelContextProtocol.Server.McpServer;

/// <summary>
/// Base class for MCP server integration tests using in-memory pipe transport.
/// Sets up a full MCP server with the Hex1b terminal session manager and tools.
/// </summary>
public abstract class McpServerTestBase : IAsyncDisposable
{
    private readonly Pipe _clientToServerPipe = new();
    private readonly Pipe _serverToClientPipe = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _serverTask = Task.CompletedTask;
    private MCP? _server;
    private ServiceProvider? _serviceProvider;
    private ILoggerFactory? _loggerFactory;

    protected McpServerTestBase()
    {
        ServiceCollection = new ServiceCollection();
        
        // Configure logging
        ServiceCollection.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        // Register the terminal session manager (singleton like in Program.cs)
        ServiceCollection.AddSingleton<TerminalSessionManager>();
        
        // Configure MCP server with in-memory stream transport
        // Use the assembly containing the tools (Hex1b.McpServer)
        var mcpServerAssembly = typeof(TerminalSessionManager).Assembly;
        McpServerBuilder = ServiceCollection
            .AddMcpServer()
            .WithStreamServerTransport(
                _clientToServerPipe.Reader.AsStream(), 
                _serverToClientPipe.Writer.AsStream())
            .WithToolsFromAssembly(mcpServerAssembly);

        // Allow subclasses to configure additional services
        ConfigureServices(ServiceCollection, McpServerBuilder);
    }

    protected ServiceCollection ServiceCollection { get; }
    protected IMcpServerBuilder McpServerBuilder { get; }

    protected MCP Server => _server 
        ?? throw new InvalidOperationException("Server not started. Call StartServerAsync first.");

    protected ServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("Server not started. Call StartServerAsync first.");

    protected TerminalSessionManager SessionManager => ServiceProvider.GetRequiredService<TerminalSessionManager>();

    /// <summary>
    /// Override to configure additional services before the server starts.
    /// </summary>
    protected virtual void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
    }

    /// <summary>
    /// Starts the MCP server and prepares it for testing.
    /// </summary>
    protected async Task StartServerAsync()
    {
        _serviceProvider = ServiceCollection.BuildServiceProvider(validateScopes: true);
        _loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
        _server = _serviceProvider.GetRequiredService<MCP>();
        _serverTask = _server.RunAsync(_cts.Token);
        
        // Give the server a moment to start
        await Task.Delay(50);
    }

    /// <summary>
    /// Creates an MCP client connected to the test server.
    /// </summary>
    protected async Task<McpClient> CreateClientAsync(McpClientOptions? clientOptions = null)
    {
        return await McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: _clientToServerPipe.Writer.AsStream(),
                serverOutput: _serverToClientPipe.Reader.AsStream(),
                _loggerFactory),
            clientOptions: clientOptions,
            loggerFactory: _loggerFactory,
            cancellationToken: _cts.Token);
    }

    /// <summary>
    /// Cancellation token that is cancelled when the test is disposing.
    /// </summary>
    protected CancellationToken TestCancellationToken => _cts.Token;

    public virtual async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _clientToServerPipe.Writer.Complete();
        _serverToClientPipe.Writer.Complete();

        try
        {
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Server didn't stop in time, continue cleanup
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        _cts.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
