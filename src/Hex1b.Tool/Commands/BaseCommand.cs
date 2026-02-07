using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands;

/// <summary>
/// Abstract base class for all Hex1b CLI commands.
/// Provides shared infrastructure (output formatting, logging) and delegates
/// execution to the <see cref="ExecuteAsync"/> method.
/// </summary>
internal abstract class BaseCommand : Command
{
    protected OutputFormatter Formatter { get; }
    protected ILogger Logger { get; }

    protected BaseCommand(string name, string description,
        OutputFormatter formatter, ILogger logger) : base(name, description)
    {
        Formatter = formatter;
        Logger = logger;

        SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                return await ExecuteAsync(parseResult, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return 130; // Standard SIGINT exit code
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Command failed");
                Formatter.WriteError(ex.Message);
                return 1;
            }
        });
    }

    protected abstract Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken);
}
