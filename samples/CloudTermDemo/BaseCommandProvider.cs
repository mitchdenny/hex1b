using System.CommandLine;

namespace CloudTermDemo;

/// <summary>
/// Provides commands available at every node type: ls, cd, pwd, info.
/// </summary>
internal sealed class BaseCommandProvider
{
    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        // ls
        var lsCommand = new Command("ls", "List child resources");
        lsCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var node = ctx.ShellState.CurrentNode;
            if (node.Children.Count == 0)
            {
                ctx.WriteLine("  (no child resources)");
                return;
            }
            foreach (var child in node.Children)
            {
                var desc = child.Description != null ? $"  ({child.Description})" : "";
                ctx.WriteLine($"  {child.TypeLabel,-18} {child.Name}{desc}");
            }

            if (ctx.Tutorial.CurrentStep == 0)
                ctx.Tutorial.Advance();
        });
        root.Subcommands.Add(lsCommand);

        // cd
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
                ctx.WriteLine($"  → {ctx.ShellState.GetPath()}");
                return;
            }
            if (target == "..")
            {
                if (!ctx.ShellState.NavigateUp())
                    ctx.WriteLine("  Already at root.");
                else
                    ctx.WriteLine($"  → {ctx.ShellState.GetPath()}");
                return;
            }
            if (ctx.ShellState.NavigateTo(target))
            {
                ctx.WriteLine($"  → {ctx.ShellState.GetPath()}");
                if (ctx.Tutorial.CurrentStep == 1)
                    ctx.Tutorial.Advance();
            }
            else
            {
                ctx.WriteLine($"  Not found: {target}");
                var suggestions = ctx.ShellState.CurrentNode.Children
                    .Where(c => c.Name.Contains(target, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Name)
                    .Take(3);
                if (suggestions.Any())
                    ctx.WriteLine($"  Did you mean: {string.Join(", ", suggestions)}?");
            }
        });
        root.Subcommands.Add(cdCommand);

        // pwd
        var pwdCommand = new Command("pwd", "Show current path");
        pwdCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.WriteLine(ctx.ShellState.GetPath());
        });
        root.Subcommands.Add(pwdCommand);

        // info
        var infoCommand = new Command("info", "Show details about the current resource");
        infoCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var node = ctx.ShellState.CurrentNode;
            ctx.WriteLine($"  Name:     {node.Name}");
            ctx.WriteLine($"  Type:     {node.TypeLabel}");
            if (node.Description != null)
                ctx.WriteLine($"  Details:  {node.Description}");
            ctx.WriteLine($"  Path:     {ctx.ShellState.GetPath()}");
            ctx.WriteLine($"  Children: {node.Children.Count}");
        });
        root.Subcommands.Add(infoCommand);
    }
}
