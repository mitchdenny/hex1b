# Workload Adapters

Workload adapters define what work the terminal performs. They implement the `IHex1bTerminalWorkloadAdapter` interface and handle input processing, state management, and the main execution loop.

## Built-in Adapters

### Hex1bAppWorkloadAdapter

Runs a Hex1b TUI application:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithConsolePresentation()
    .WithAppWorkload(BuildUI)  // Your widget builder function
    .Build();

Hex1bWidget BuildUI(Hex1bAppContext context)
{
    return new VStackWidget()
        .Children(
            new TextBlockWidget("Hello, World!"),
            new ButtonWidget("Click Me")
        );
}
```

### Hex1bTerminalChildProcess

Hosts an external process (shell, CLI tool, etc.):

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithConsolePresentation()
    .WithChildProcessWorkload("bash", "-i")
    .Build();
```

This enables:
- Running shells inside your application
- Hosting other TUI programs
- Building terminal multiplexers

## The IHex1bTerminalWorkloadAdapter Interface

```csharp
public interface IHex1bTerminalWorkloadAdapter
{
    /// <summary>
    /// Initialize the adapter with the parent terminal.
    /// </summary>
    void Initialize(Hex1bTerminal terminal);
    
    /// <summary>
    /// Run the main workload loop.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Handle keyboard input from the user.
    /// </summary>
    void HandleInput(Hex1bKeyEvent keyEvent);
    
    /// <summary>
    /// Handle terminal resize events.
    /// </summary>
    void HandleResize(int width, int height);
    
    /// <summary>
    /// Clean up resources.
    /// </summary>
    void Dispose();
}
```

## The IHex1bAppTerminalWorkloadAdapter Interface

For TUI applications, there's an extended interface:

```csharp
public interface IHex1bAppTerminalWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    /// <summary>
    /// Get the current widget tree for inspection.
    /// </summary>
    Hex1bWidget? CurrentWidget { get; }
    
    /// <summary>
    /// Get the current node tree for inspection.
    /// </summary>
    Hex1bNode? RootNode { get; }
    
    /// <summary>
    /// Force a re-render of the UI.
    /// </summary>
    void Invalidate();
}
```

## Creating a Custom Adapter

Build custom workload adapters for specialized needs:

```csharp
public class RemoteTerminalWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly TcpClient _client;
    private Hex1bTerminal? _terminal;
    
    public RemoteTerminalWorkloadAdapter(string host, int port)
    {
        _client = new TcpClient(host, port);
    }
    
    public void Initialize(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var stream = _client.GetStream();
        var buffer = new byte[4096];
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead > 0)
            {
                // Process incoming data and update display
                ProcessData(buffer.AsSpan(0, bytesRead));
            }
        }
    }
    
    public void HandleInput(Hex1bKeyEvent keyEvent)
    {
        // Forward input to remote host
        var data = EncodeKeyEvent(keyEvent);
        _client.GetStream().Write(data);
    }
    
    public void HandleResize(int width, int height)
    {
        // Send resize notification to remote host
    }
    
    public void Dispose()
    {
        _client.Dispose();
    }
}
```

## Combining Workloads

You can create composite workloads that combine multiple adapters:

```csharp
// Example: Terminal multiplexer with multiple panes
public class MultiplexerWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly List<IHex1bTerminalWorkloadAdapter> _panes = new();
    private int _activePaneIndex = 0;
    
    public void AddPane(IHex1bTerminalWorkloadAdapter workload)
    {
        _panes.Add(workload);
    }
    
    public void HandleInput(Hex1bKeyEvent keyEvent)
    {
        // Handle pane switching with Ctrl+Arrow
        if (keyEvent.Modifiers.HasFlag(Hex1bModifiers.Control))
        {
            // Switch panes
        }
        else
        {
            // Forward to active pane
            _panes[_activePaneIndex].HandleInput(keyEvent);
        }
    }
    
    // ... implement other methods
}
```

## Use Cases

| Adapter | Use Case |
|---------|----------|
| `Hex1bAppWorkloadAdapter` | Declarative TUI applications |
| `Hex1bTerminalChildProcess` | Shell hosting, process management |
| Custom remote adapter | SSH clients, remote terminals |
| Custom multiplexer | tmux-like applications |

## Next Steps

- Learn about [Presentation Adapters](./presentation-adapters) for custom display handling
- See [Using the Emulator](./pluggable-terminal-emulator) for a step-by-step tutorial
