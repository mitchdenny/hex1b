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

    /// <summary>Lines of output produced by the command.</summary>
    public List<string> OutputLines { get; } = [];

    public void WriteLine(string line = "") => OutputLines.Add(line);
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
    /// Returns output lines.
    /// </summary>
    public async Task<List<string>> ExecuteAsync(string commandLine, CommandExecutionContext context)
    {
        var root = new RootCommand("Cloud Term Shell");
        var contextAccessor = () => context;

        // Add base commands (ls, cd, pwd, info)
        _baseProvider.AddCommands(root, contextAccessor);

        // Add type-specific commands
        if (_providers.TryGetValue(context.ShellState.CurrentNode.Kind, out var provider))
        {
            provider.AddCommands(root, contextAccessor);
        }

        // Redirect System.CommandLine output to our context
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

        // Capture any System.CommandLine output (help text, errors)
        var sclOutput = capturedOut.ToString();
        if (!string.IsNullOrWhiteSpace(sclOutput))
        {
            foreach (var line in sclOutput.Split('\n', StringSplitOptions.None))
            {
                var trimmed = line.TrimEnd('\r');
                if (!string.IsNullOrEmpty(trimmed))
                    context.OutputLines.Add(trimmed);
            }
        }

        return context.OutputLines;
    }
}
