using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Agent;

/// <summary>
/// Starts the MCP server for AI agent integration (stdio transport).
/// </summary>
internal sealed class AgentMcpCommand : BaseCommand
{
    public AgentMcpCommand(
        OutputFormatter formatter,
        ILogger<AgentMcpCommand> logger)
        : base("mcp", "Start the MCP server (stdio transport)", formatter, logger)
    {
    }

    protected override Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        Formatter.WriteError("MCP agent mode is not yet implemented. See: https://github.com/mitchdenny/hex1b/issues/155");
        return Task.FromResult(1);
    }
}
