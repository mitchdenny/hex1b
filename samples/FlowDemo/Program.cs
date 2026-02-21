using System.CommandLine;
using FlowDemo.Commands;

// FlowDemo — Mock Aspire CLI using Hex1b Flow
// Demonstrates interactive CLI flows with inline slices and scrollback.

var nameOption = new Option<string?>("--name", "-n") { Description = "The name of the project to create." };
var outputOption = new Option<string?>("--output", "-o") { Description = "The output path for the project." };

var newCommand = new Command("new", "Create a new project from a template.");
newCommand.Options.Add(nameOption);
newCommand.Options.Add(outputOption);
newCommand.SetAction(async (parseResult, ct) =>
{
    var name = parseResult.GetValue(nameOption);
    var output = parseResult.GetValue(outputOption);
    await NewCommand.RunAsync(name, output);
});

var initCommand = new Command("init", "Initialize agent environment configuration for detected agents.");
initCommand.SetAction(async (parseResult, ct) =>
{
    await AgentInitCommand.RunAsync();
});

var agentCommand = new Command("agent", "Manage agent configurations.");
agentCommand.Subcommands.Add(initCommand);

var sizzleCommand = new Command("sizzle", "Showcase exotic Hex1b controls.");
sizzleCommand.SetAction(async (parseResult, ct) =>
{
    await SizzleCommand.RunAsync();
});

var rootCommand = new RootCommand("FlowDemo — Mock Aspire CLI powered by Hex1b Flow");
rootCommand.Subcommands.Add(newCommand);
rootCommand.Subcommands.Add(agentCommand);
rootCommand.Subcommands.Add(sizzleCommand);

return await rootCommand.Parse(args).InvokeAsync();
