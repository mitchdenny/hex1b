using System.CommandLine;
using System.CommandLine.Parsing;

namespace CloudTermDemo;

/// <summary>
/// Context available to command handlers during execution.
/// Resolved at invoke time, not at command definition time.
/// </summary>
public sealed class CommandExecutionContext
{
    public required CloudShellState ShellState { get; init; }
    public required PanelManager PanelManager { get; init; }
    public required TutorialService Tutorial { get; init; }

    /// <summary>The typed result produced by the command.</summary>
    public CommandResult? Result { get; set; }

    /// <summary>Callback to update the spinner status message during execution.</summary>
    public Action<string>? OnStatusUpdate { get; init; }

    /// <summary>Callback to add intermediate output to the shell history during execution.</summary>
    public Action<CommandResult>? OnIntermediateOutput { get; init; }

    /// <summary>Update the spinner message (shown while command is executing).</summary>
    public void UpdateStatus(string message) => OnStatusUpdate?.Invoke(message);

    /// <summary>Add intermediate output that appears in the history immediately.</summary>
    public void AddIntermediateOutput(CommandResult result) => OnIntermediateOutput?.Invoke(result);

    /// <summary>Shortcut: set result to a TextResult with the given lines.</summary>
    public void SetTextResult(params string[] lines)
    {
        var r = new TextResult();
        r.Lines.AddRange(lines);
        Result = r;
    }

    /// <summary>Shortcut: append a line to an existing TextResult or create one.</summary>
    public void WriteLine(string line = "")
    {
        if (Result is TextResult tr)
        {
            tr.Lines.Add(line);
        }
        else
        {
            var r = new TextResult();
            r.Lines.Add(line);
            Result = r;
        }
    }
}

/// <summary>
/// Provides commands for a specific node kind. Providers contribute commands
/// to a shared root — they don't own the root command.
/// </summary>
public interface INodeCommandProvider
{
    CloudNodeKind Kind { get; }
    void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext);
}

/// <summary>
/// Registry that maps node kinds to their command providers and builds
/// per-node command trees with base + type-specific commands merged.
/// </summary>
public sealed class NodeCommandRegistry
{
    private readonly Dictionary<CloudNodeKind, INodeCommandProvider> _providers = new();
    private readonly BaseCommandProvider _baseProvider = new();

    public NodeCommandRegistry(IEnumerable<INodeCommandProvider> providers)
    {
        foreach (var provider in providers)
        {
            _providers[provider.Kind] = provider;
        }
    }

    /// <summary>
    /// Parses and executes a command line in the context of the given node kind.
    /// The result is stored in context.Result.
    /// </summary>
    public async Task ExecuteAsync(string commandLine, CommandExecutionContext context)
    {
        var root = new RootCommand("Cloud Term Shell");
        var contextAccessor = () => context;

        _baseProvider.AddCommands(root, contextAccessor);

        if (_providers.TryGetValue(context.ShellState.CurrentNode.Kind, out var provider))
        {
            provider.AddCommands(root, contextAccessor);
        }

        // Redirect System.CommandLine output (help/errors) to capture
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var capturedOut = new StringWriter();
        Console.SetOut(capturedOut);
        Console.SetError(capturedOut);

        try
        {
            var parseResult = root.Parse(commandLine);
            await parseResult.InvokeAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        // If System.CommandLine produced output (help text, errors) and no result was set
        var sclOutput = capturedOut.ToString();
        if (!string.IsNullOrWhiteSpace(sclOutput) && context.Result == null)
        {
            var lines = sclOutput.Split('\n', StringSplitOptions.None)
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();
            context.SetTextResult(lines);
        }
    }
}
