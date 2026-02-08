using System.CommandLine;
using System.Text.Json;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.App;

/// <summary>
/// Inspects the widget/node tree of a TUI application.
/// </summary>
internal sealed class AppTreeCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<bool> s_focusOption = new("--focus") { Description = "Include focus ring info" };
    private static readonly Option<bool> s_popupsOption = new("--popups") { Description = "Include popup stack" };
    private static readonly Option<int?> s_depthOption = new("--depth") { Description = "Limit tree depth" };
    private static readonly Option<bool> s_noPerfOption = new("--no-perf") { Description = "Hide performance timing" };

    public AppTreeCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<AppTreeCommand> logger)
        : base("tree", "Inspect the widget/node tree of a TUI application", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_focusOption);
        Options.Add(s_popupsOption);
        Options.Add(s_depthOption);
        Options.Add(s_noPerfOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var showFocus = parseResult.GetValue(s_focusOption);
        var showPopups = parseResult.GetValue(s_popupsOption);
        var hidePerf = parseResult.GetValue(s_noPerfOption);

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        var response = await _client.SendAsync(resolved.SocketPath!, new DiagnosticsRequest { Method = "tree" }, cancellationToken);
        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Failed to get tree");
            return 1;
        }

        var showPerf = !hidePerf && response.FrameInfo is { TimingEnabled: true };

        if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new
            {
                tree = response.Tree,
                popups = showPopups ? response.Popups : null,
                focusInfo = showFocus ? response.FocusInfo : null,
                frameInfo = showPerf ? response.FrameInfo : null
            });
        }
        else
        {
            if (showPerf && response.FrameInfo != null)
            {
                Formatter.WriteLine($"Frame: build={response.FrameInfo.BuildMs:F2}ms reconcile={response.FrameInfo.ReconcileMs:F2}ms render={response.FrameInfo.RenderMs:F2}ms");
                Formatter.WriteLine("");
            }

            if (response.Tree != null)
            {
                PrintTree(response.Tree, "", true, showPerf);
            }

            if (showPopups && response.Popups is { Count: > 0 })
            {
                Formatter.WriteLine("");
                Formatter.WriteLine("Popups:");
                foreach (var popup in response.Popups)
                {
                    Formatter.WriteLine($"  [{popup.Index}] {popup.ContentType} (barrier={popup.IsBarrier}, anchored={popup.IsAnchored})");
                }
            }

            if (showFocus && response.FocusInfo != null)
            {
                Formatter.WriteLine("");
                Formatter.WriteLine($"Focus: {response.FocusInfo.FocusedNodeType ?? "none"} (index {response.FocusInfo.CurrentFocusIndex}/{response.FocusInfo.FocusableCount})");
            }
        }

        return 0;
    }

    private void PrintTree(DiagnosticNode node, string indent, bool isLast, bool showPerf)
    {
        var connector = isLast ? "└─ " : "├─ ";
        var focused = node.IsFocused ? " [FOCUSED]" : "";

        // Node header
        Formatter.WriteLine($"{indent}{connector}{node.Type}{focused}");

        // Detail lines use extra indentation under the connector
        var detailIndent = indent + (isLast ? "   " : "│  ") + "   ";

        // Properties
        if (node.Properties is { Count: > 0 })
        {
            var props = string.Join(", ", node.Properties.Select(p => $"{p.Key}={p.Value}"));
            Formatter.WriteLine($"{detailIndent}Properties: {props}");
        }

        // Geometry
        Formatter.WriteLine($"{detailIndent}Geometry:   {node.Bounds}");

        // Performance timing
        if (showPerf && node.Timing != null)
        {
            var timingStr = node.Timing.ToString();
            if (timingStr.Length > 0)
            {
                Formatter.WriteLine($"{detailIndent}Performance: {timingStr}");
            }
        }

        // Children
        if (node.Children is { Count: > 0 })
        {
            var childIndent = indent + (isLast ? "   " : "│  ");
            Formatter.WriteLine($"{detailIndent}Children:");
            for (var i = 0; i < node.Children.Count; i++)
            {
                PrintTree(node.Children[i], childIndent + "   ", i == node.Children.Count - 1, showPerf);
            }
        }
    }
}
