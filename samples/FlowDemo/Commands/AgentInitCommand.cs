using Hex1b;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the interactive "flowdemo agent init" command, mocking the aspire agent init experience.
/// </summary>
internal static class AgentInitCommand
{
    private record AgentInfo(string Name, string ConfigFile, string Description);

    private static readonly AgentInfo[] DetectedAgents =
    [
        new("GitHub Copilot", ".github/copilot-instructions.md", "AI pair programmer by GitHub"),
        new("Cursor", ".cursor/rules", "AI-powered code editor"),
        new("Claude Code", "CLAUDE.md", "Anthropic's coding assistant"),
        new("Windsurf", ".windsurfrules", "Codeium's AI IDE"),
    ];

    public static async Task RunAsync()
    {
        var cursorRow = Console.GetCursorPosition().Top;

        var detected = new List<AgentInfo>();
        var selected = new HashSet<string>();
        var configured = new List<string>();

        await Hex1bTerminal.CreateBuilder()
            .WithScrollback()
            .WithHex1bFlow(async flow =>
            {
                // Step 1: Detection spinner
                var detecting = true;
                var detectedCount = 0;

                await flow.SliceAsync(
                    configure: app =>
                    {
                        _ = Task.Run(async () =>
                        {
                            // Simulate scanning for agents
                            foreach (var agent in DetectedAgents)
                            {
                                await Task.Delay(600);
                                detected.Add(agent);
                                detectedCount = detected.Count;
                                app.Invalidate();
                            }

                            detecting = false;
                            app.Invalidate();
                            await Task.Delay(300);
                            app.RequestStop();
                        });

                        return ctx => ctx.HStack(h =>
                        [
                            detecting ? h.Spinner(SpinnerStyle.Dots) : h.Text("✓"),
                            h.Text(detecting
                                ? $" Detecting agents... ({detectedCount} found)"
                                : $" Found {detected.Count} agents"),
                        ]);
                    },
                    @yield: ctx => ctx.Text($"  ✓ Detected {detected.Count} agents"),
                    options: new Hex1bFlowSliceOptions { MaxHeight = 1 }
                );

                // Step 2: Agent selection with checkboxes
                // Pre-select all agents
                foreach (var agent in detected)
                {
                    selected.Add(agent.Name);
                }

                await flow.SliceAsync(
                    builder: ctx => ctx.VStack(v =>
                    [
                        v.Text("Select agents to configure:"),
                        .. detected.Select(agent =>
                            (Hex1bWidget)v.Checkbox(selected.Contains(agent.Name) ? CheckboxState.Checked : CheckboxState.Unchecked)
                                .Label(agent.Name)
                                .OnToggled(e =>
                                {
                                    if (selected.Contains(agent.Name))
                                        selected.Remove(agent.Name);
                                    else
                                        selected.Add(agent.Name);
                                })),
                        v.Text($"  {selected.Count} of {detected.Count} selected"),
                        v.Button("Configure selected").OnClick(e => e.Context.RequestStop()),
                    ]),
                    @yield: ctx => ctx.Text($"  ✓ Selected: {string.Join(", ", selected)}"),
                    options: new Hex1bFlowSliceOptions { MaxHeight = detected.Count + 5 }
                );

                // Step 3: Configuration spinner per agent
                var configuring = true;
                var currentAgent = "";
                var configuredIndex = 0;

                await flow.SliceAsync(
                    configure: app =>
                    {
                        _ = Task.Run(async () =>
                        {
                            var selectedAgents = detected.Where(a => selected.Contains(a.Name)).ToList();

                            foreach (var agent in selectedAgents)
                            {
                                currentAgent = agent.Name;
                                configuredIndex++;
                                app.Invalidate();
                                await Task.Delay(1000);
                                configured.Add(agent.Name);
                            }

                            configuring = false;
                            app.Invalidate();
                            await Task.Delay(300);
                            app.RequestStop();
                        });

                        return ctx => ctx.HStack(h =>
                        [
                            configuring ? h.Spinner(SpinnerStyle.Dots) : h.Text("✓"),
                            h.Text(configuring
                                ? $" Configuring {currentAgent}... ({configuredIndex}/{selected.Count})"
                                : $" Configured {configured.Count} agents"),
                        ]);
                    },
                    @yield: ctx => ctx.Text($"  ✓ Configured {configured.Count} agents"),
                    options: new Hex1bFlowSliceOptions { MaxHeight = 1 }
                );

            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();

        // Summary output
        Console.WriteLine();
        Console.WriteLine("Agent configuration complete!");
        Console.WriteLine();

        var selectedAgents = DetectedAgents.Where(a => configured.Contains(a.Name));
        foreach (var agent in selectedAgents)
        {
            Console.WriteLine($"  ✓ {agent.Name} — {agent.ConfigFile}");
        }

        Console.WriteLine();
        Console.WriteLine("Configuration files have been written to your project.");
    }
}
