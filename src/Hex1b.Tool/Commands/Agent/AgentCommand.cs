using System.CommandLine;

namespace Hex1b.Tool.Commands.Agent;

/// <summary>
/// Parent command grouping AI agent integration operations.
/// </summary>
internal sealed class AgentCommand : Command
{
    public AgentCommand(
        AgentMcpCommand mcpCommand,
        AgentInitCommand initCommand)
        : base("agent", "AI agent integration")
    {
        Subcommands.Add(mcpCommand);
        Subcommands.Add(initCommand);
    }
}
