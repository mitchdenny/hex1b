using System.CommandLine;
using FlowDemo.Commands;

// FlowDemo — Mock Aspire CLI using Hex1b Flow
// Demonstrates interactive CLI flows with inline slices and scrollback.

var softWrapOption = new Option<bool>("--soft-wrap", "-s")
{
    Description = "Enable experimental soft-wrap tombstones (Hex1bFlowOptions.UseSoftWrapTombstones). When set, completed-step output is emitted as proper logical lines so the host terminal reflows it on resize and scrolls older tombstones into the scrollback buffer naturally.",
    Recursive = true,
};

var nameOption = new Option<string?>("--name", "-n") { Description = "The name of the project to create." };
var outputOption = new Option<string?>("--output", "-o") { Description = "The output path for the project." };

var newCommand = new Command("new", "Create a new project from a template.");
newCommand.Options.Add(nameOption);
newCommand.Options.Add(outputOption);
newCommand.SetAction(async (parseResult, ct) =>
{
    var name = parseResult.GetValue(nameOption);
    var output = parseResult.GetValue(outputOption);
    var softWrap = parseResult.GetValue(softWrapOption);
    await NewCommand.RunAsync(name, output, softWrap);
});

var initCommand = new Command("init", "Initialize agent environment configuration for detected agents.");
initCommand.SetAction(async (parseResult, ct) =>
{
    var softWrap = parseResult.GetValue(softWrapOption);
    await AgentInitCommand.RunAsync(softWrap);
});

var agentCommand = new Command("agent", "Manage agent configurations.");
agentCommand.Subcommands.Add(initCommand);

var sizzleCommand = new Command("sizzle", "Showcase exotic Hex1b controls.");
sizzleCommand.SetAction(async (parseResult, ct) =>
{
    var softWrap = parseResult.GetValue(softWrapOption);
    await SizzleCommand.RunAsync(softWrap);
});

var copilotCommand = new Command("copilot", "Mock Copilot CLI chat interface.");
copilotCommand.SetAction(async (parseResult, ct) =>
{
    var softWrap = parseResult.GetValue(softWrapOption);
    await CopilotCommand.RunAsync(softWrap);
});

var rootCommand = new RootCommand("FlowDemo — Mock Aspire CLI powered by Hex1b Flow");
rootCommand.Options.Add(softWrapOption);
rootCommand.Subcommands.Add(newCommand);
rootCommand.Subcommands.Add(agentCommand);
rootCommand.Subcommands.Add(sizzleCommand);
rootCommand.Subcommands.Add(copilotCommand);

return await rootCommand.Parse(args).InvokeAsync();
