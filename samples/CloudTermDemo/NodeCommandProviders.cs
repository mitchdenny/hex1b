using System.CommandLine;

namespace CloudTermDemo;

/// <summary>
/// Helper to add the 'edit' command to resource types that support YAML editing.
/// </summary>
internal static class EditCommandHelper
{
    public static void AddEditCommand(RootCommand root, Func<CommandExecutionContext> getContext, Func<CloudNode, string> yamlProvider)
    {
        var editCommand = new Command("edit", "Edit this resource's YAML in the editor");
        editCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var node = ctx.ShellState.CurrentNode;
            var yaml = yamlProvider(node);
            ctx.PanelManager.OpenEditorPanel($"edit: {node.Name}", yaml);
            ctx.SetTextResult($"  Editing {node.TypeLabel} '{node.Name}'...");
        });
        root.Subcommands.Add(editCommand);
    }
}

/// <summary>
/// Commands available when inside a Pod: attach, logs, describe.
/// </summary>
internal sealed class PodCommandProvider : INodeCommandProvider
{
    public CloudNodeKind Kind => CloudNodeKind.Pod;

    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        var attachCommand = new Command("attach", "Attach to this pod's terminal");
        attachCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var pod = ctx.ShellState.CurrentNode;
            ctx.PanelManager.OpenTerminalPanel($"pod/{pod.Name}");
            ctx.WriteLine($"  Attaching to pod {pod.Name}...");
        });
        root.Subcommands.Add(attachCommand);

        var logsCommand = new Command("logs", "View pod logs");
        var followOption = new Option<bool>("--follow", "-f") { Description = "Stream logs continuously" };
        logsCommand.Options.Add(followOption);
        logsCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var pod = ctx.ShellState.CurrentNode;
            var follow = parseResult.GetValue(followOption);
            ctx.WriteLine($"  === Logs for {pod.Name} ===");
            ctx.WriteLine($"  2024-01-15T10:23:01Z  INFO   Starting application...");
            ctx.WriteLine($"  2024-01-15T10:23:02Z  INFO   Listening on port 8080");
            ctx.WriteLine($"  2024-01-15T10:23:05Z  INFO   Health check passed");
            ctx.WriteLine($"  2024-01-15T10:24:12Z  WARN   High memory usage: 78%");
            ctx.WriteLine($"  2024-01-15T10:25:00Z  INFO   Request processed in 45ms");
            if (follow)
                ctx.WriteLine($"  (streaming mode — press Ctrl+C to stop)");
        });
        root.Subcommands.Add(logsCommand);

        var describeCommand = new Command("describe", "Show detailed pod information");
        describeCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var pod = ctx.ShellState.CurrentNode;
            ctx.WriteLine($"  Name:       {pod.Name}");
            ctx.WriteLine($"  Status:     {pod.Description}");
            ctx.WriteLine($"  Node:       aks-node-0");
            ctx.WriteLine($"  IP:         10.244.0.{Random.Shared.Next(2, 254)}");
            ctx.WriteLine($"  Image:      mcr.microsoft.com/dotnet/aspnet:10.0");
            ctx.WriteLine($"  Restarts:   0");
            ctx.WriteLine($"  CPU:        {Random.Shared.Next(10, 80)}m");
            ctx.WriteLine($"  Memory:     {Random.Shared.Next(64, 512)}Mi");
        });
        root.Subcommands.Add(describeCommand);

        EditCommandHelper.AddEditCommand(root, getContext, node =>
        {
            var ns = node.Parent?.Name ?? "default";
            return MockYaml.ForPod(node.Name, ns);
        });
    }
}

/// <summary>
/// Commands available when inside a Namespace: get pods, get services.
/// </summary>
internal sealed class NamespaceCommandProvider : INodeCommandProvider
{
    public CloudNodeKind Kind => CloudNodeKind.Namespace;

    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        var getCommand = new Command("get", "Get Kubernetes resources");

        var getPodsCommand = new Command("pods", "List pods in this namespace");
        getPodsCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var ns = ctx.ShellState.CurrentNode;
            ctx.WriteLine($"  NAMESPACE: {ns.Name}");
            ctx.WriteLine($"  {"NAME",-35} {"STATUS",-12} {"RESTARTS",-10} AGE");
            foreach (var pod in ns.Children.Where(c => c.Kind == CloudNodeKind.Pod))
            {
                ctx.WriteLine($"  {pod.Name,-35} {"Running",-12} {"0",-10} {Random.Shared.Next(1, 30)}d");
            }
        });
        getCommand.Subcommands.Add(getPodsCommand);

        var getServicesCommand = new Command("services", "List services in this namespace");
        getServicesCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.WriteLine($"  {"NAME",-25} {"TYPE",-15} {"CLUSTER-IP",-18} PORT(S)");
            ctx.WriteLine($"  {"kubernetes",-25} {"ClusterIP",-15} {"10.0.0.1",-18} 443/TCP");
            if (ctx.ShellState.CurrentNode.Name == "ingress-nginx")
                ctx.WriteLine($"  {"ingress-controller",-25} {"LoadBalancer",-15} {"10.0.0.42",-18} 80/TCP,443/TCP");
        });
        getCommand.Subcommands.Add(getServicesCommand);

        root.Subcommands.Add(getCommand);

        EditCommandHelper.AddEditCommand(root, getContext, node => MockYaml.ForNamespace(node.Name));
    }
}

/// <summary>
/// Commands available when inside an AKS Cluster: nodes, scale.
/// </summary>
internal sealed class AksClusterCommandProvider : INodeCommandProvider
{
    public CloudNodeKind Kind => CloudNodeKind.AksCluster;

    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        var nodesCommand = new Command("nodes", "List cluster nodes");
        nodesCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var cluster = ctx.ShellState.CurrentNode;
            ctx.WriteLine($"  Cluster: {cluster.Name}");
            ctx.WriteLine($"  {"NAME",-25} {"STATUS",-10} {"ROLES",-15} VERSION");
            for (var i = 0; i < 3; i++)
                ctx.WriteLine($"  {"aks-nodepool1-" + (30000000 + i),-25} {"Ready",-10} {"<none>",-15} v1.30.2");
        });
        root.Subcommands.Add(nodesCommand);

        var scaleCommand = new Command("scale", "Scale the node pool");
        var countArg = new Argument<int>("count") { Description = "Target node count" };
        scaleCommand.Arguments.Add(countArg);
        scaleCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var count = parseResult.GetValue(countArg);
            ctx.WriteLine($"  Scaling {ctx.ShellState.CurrentNode.Name} to {count} nodes...");
            ctx.WriteLine($"  (simulated — would take 3-5 minutes)");
        });
        root.Subcommands.Add(scaleCommand);

        EditCommandHelper.AddEditCommand(root, getContext, node => MockYaml.ForAksCluster(node.Name));

        var monitorCommand = new Command("monitor", "Open live CPU monitoring for cluster nodes");
        monitorCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var cluster = ctx.ShellState.CurrentNode;
            var state = new ClusterMonitorState(cluster.Name);
            ctx.PanelManager.InsertPanelRight($"monitor: {cluster.Name}", tag: "monitor", data: state);
            ctx.SetTextResult($"  Monitoring {cluster.Name}...");
        });
        root.Subcommands.Add(monitorCommand);
    }
}

/// <summary>
/// Commands available on an App Service: logs, restart, browse.
/// </summary>
internal sealed class AppServiceCommandProvider : INodeCommandProvider
{
    public CloudNodeKind Kind => CloudNodeKind.AppService;

    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        var logsCommand = new Command("logs", "Stream application logs");
        logsCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.WriteLine($"  === App Service Logs: {ctx.ShellState.CurrentNode.Name} ===");
            ctx.WriteLine($"  2024-01-15 10:23:01  Hosting started");
            ctx.WriteLine($"  2024-01-15 10:23:02  Now listening on: http://[::]:8080");
            ctx.WriteLine($"  2024-01-15 10:24:00  Request: GET /health → 200 (12ms)");
            ctx.WriteLine($"  2024-01-15 10:24:15  Request: GET /api/data → 200 (45ms)");
        });
        root.Subcommands.Add(logsCommand);

        var restartCommand = new Command("restart", "Restart the app service");
        restartCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.WriteLine($"  Restarting {ctx.ShellState.CurrentNode.Name}...");
            ctx.WriteLine($"  (simulated — restart in progress)");
        });
        root.Subcommands.Add(restartCommand);

        var browseCommand = new Command("browse", "Open the app service URL");
        browseCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.WriteLine($"  https://{ctx.ShellState.CurrentNode.Name}.azurewebsites.net");
        });
        root.Subcommands.Add(browseCommand);

        EditCommandHelper.AddEditCommand(root, getContext, node => MockYaml.ForAppService(node.Name));
    }
}

/// <summary>
/// Commands available on a SQL Server: connect, status.
/// </summary>
internal sealed class SqlServerCommandProvider : INodeCommandProvider
{
    public CloudNodeKind Kind => CloudNodeKind.SqlServer;

    public void AddCommands(RootCommand root, Func<CommandExecutionContext> getContext)
    {
        var connectCommand = new Command("connect", "Open an interactive SQL session");
        connectCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            ctx.PanelManager.OpenTerminalPanel($"sql/{ctx.ShellState.CurrentNode.Name}");
            ctx.WriteLine($"  Connecting to {ctx.ShellState.CurrentNode.Name}...");
        });
        root.Subcommands.Add(connectCommand);

        var statusCommand = new Command("status", "Show server status");
        statusCommand.SetAction(async (parseResult, ct) =>
        {
            var ctx = getContext();
            var node = ctx.ShellState.CurrentNode;
            ctx.WriteLine($"  Server:   {node.Name}");
            ctx.WriteLine($"  Status:   Online");
            ctx.WriteLine($"  DTU:      {node.Description}");
            ctx.WriteLine($"  Storage:  {Random.Shared.Next(1, 100)}% used");
        });
        root.Subcommands.Add(statusCommand);
    }
}
