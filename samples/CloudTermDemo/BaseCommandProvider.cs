using System.CommandLine;

namespace CloudTermDemo;

/// <summary>
/// Provides commands available at every node type: ls, cd, pwd, info.
/// </summary>
internal sealed class BaseCommandProvider
{
    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        // ls — produces a ResourceListResult for table rendering
        var lsCommand = new Command("ls", "List child resources");
        lsCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var node = ctx.ShellState.CurrentNode;
            if (node.Children.Count == 0)
            {
                ctx.SetTextResult("  (no child resources)");
                return;
            }

            var result = new ResourceListResult();
            foreach (var child in node.Children)
                result.Rows.Add(new(child.TypeLabel, child.Name, child.Description));
            ctx.Result = result;

            if (ctx.Tutorial.CurrentStep == 0)
                ctx.Tutorial.Advance();
        });
        root.Subcommands.Add(lsCommand);

        // cd — produces a NavigationResult
        var cdArg = new Argument<string>("target") { Description = "Resource name, '..' for parent, '/' for root", Arity = ArgumentArity.ZeroOrOne };
        var cdCommand = new Command("cd", "Navigate to a resource");
        cdCommand.Arguments.Add(cdArg);
        cdCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var target = parseResult.GetValue(cdArg) ?? "";
            if (string.IsNullOrEmpty(target) || target == "/")
            {
                ctx.ShellState.NavigateToRoot();
                ctx.Result = new NavigationResult { Path = ctx.ShellState.GetPath() };
                return;
            }
            if (target == "..")
            {
                if (!ctx.ShellState.NavigateUp())
                    ctx.Result = new NavigationResult { Error = "Already at root." };
                else
                    ctx.Result = new NavigationResult { Path = ctx.ShellState.GetPath() };
                return;
            }
            if (ctx.ShellState.NavigateTo(target))
            {
                ctx.Result = new NavigationResult { Path = ctx.ShellState.GetPath() };
                if (ctx.Tutorial.CurrentStep == 1)
                    ctx.Tutorial.Advance();
            }
            else
            {
                var suggestions = ctx.ShellState.CurrentNode.Children
                    .Where(c => c.Name.Contains(target, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Name)
                    .Take(3)
                    .ToList();
                ctx.Result = new NavigationResult
                {
                    Error = $"Not found: {target}",
                    Suggestions = suggestions.Count > 0 ? suggestions : null,
                };
            }
        });
        root.Subcommands.Add(cdCommand);

        // pwd
        var pwdCommand = new Command("pwd", "Show current path");
        pwdCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.SetTextResult(ctx.ShellState.GetPath());
        });
        root.Subcommands.Add(pwdCommand);

        // info — produces a DetailResult
        var infoCommand = new Command("info", "Show details about the current resource");
        infoCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var node = ctx.ShellState.CurrentNode;
            var detail = new DetailResult();
            detail.Fields.Add(("Name:", node.Name));
            detail.Fields.Add(("Type:", node.TypeLabel));
            if (node.Description != null)
                detail.Fields.Add(("Details:", node.Description));
            detail.Fields.Add(("Path:", ctx.ShellState.GetPath()));
            detail.Fields.Add(("Children:", node.Children.Count.ToString()));
            ctx.Result = detail;
        });
        root.Subcommands.Add(infoCommand);
    }
}
