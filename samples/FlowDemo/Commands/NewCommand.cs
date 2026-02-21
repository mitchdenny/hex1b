using Hex1b;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the interactive "flowdemo new" command, mocking the aspire new experience.
/// </summary>
internal static class NewCommand
{
    private static readonly (string Id, string Name, string Description)[] Templates =
    [
        ("aspire-starter", "Starter App (ASP.NET Core/Blazor)", "A starter app with an ASP.NET Core backend and Blazor frontend."),
        ("aspire-ts-cs-starter", "Starter App (ASP.NET Core/React)", "A starter app with an ASP.NET Core backend and React frontend."),
        ("aspire-py-starter", "Starter App (FastAPI/React)", "A starter app with a FastAPI backend and React frontend."),
        ("aspire-apphost-singlefile", "Empty AppHost", "An empty Aspire AppHost project."),
    ];

    private static readonly string[] Languages = ["C#", "TypeScript"];

    public static async Task RunAsync(string? name, string? output)
    {
        var cursorRow = Console.GetCursorPosition().Top;

        var selectedTemplate = "";
        var projectName = name ?? "";
        var outputPath = output ?? "";
        var language = "C#";

        await Hex1bTerminal.CreateBuilder()
            .WithScrollback()
            .WithHex1bFlow(async flow =>
            {
                // Step 1: Template picker (skip if subcommand was provided directly)
                var templateIndex = 0;

                await flow.SliceAsync(
                    builder: ctx => ctx.VStack(v =>
                    [
                        v.Text("Select a project template:"),
                        v.List(Templates.Select(t => t.Name).ToArray())
                            .OnItemActivated(e =>
                            {
                                templateIndex = e.ActivatedIndex;
                                selectedTemplate = Templates[e.ActivatedIndex].Id;
                                e.Context.RequestStop();
                            })
                            .FixedHeight(Templates.Length + 1),
                    ]),
                    @yield: ctx => ctx.Text($"  ✓ Template: {Templates[templateIndex].Name}"),
                    options: new Hex1bFlowSliceOptions { MaxHeight = Templates.Length + 3 }
                );

                // Step 2: Project name
                if (string.IsNullOrEmpty(projectName))
                {
                    await flow.SliceAsync(
                        builder: ctx => ctx.VStack(v =>
                        [
                            v.Text("Enter your project name:"),
                            v.TextBox(projectName)
                                .OnSubmit(e =>
                                {
                                    projectName = e.Text;
                                    e.Context.RequestStop();
                                })
                                .FillWidth(),
                        ]),
                        @yield: ctx => ctx.Text($"  ✓ Project name: {projectName}"),
                        options: new Hex1bFlowSliceOptions { MaxHeight = 4 }
                    );
                }

                // Step 3: Output directory
                if (string.IsNullOrEmpty(outputPath))
                {
                    var defaultPath = $"./{projectName}";
                    outputPath = defaultPath;

                    await flow.SliceAsync(
                        builder: ctx => ctx.VStack(v =>
                        [
                            v.Text("Select output directory:"),
                            v.List(new[]
                            {
                                defaultPath,
                                $"./projects/{projectName}",
                                $"./src/{projectName}",
                            })
                                .OnItemActivated(e =>
                                {
                                    outputPath = e.ActivatedText;
                                    e.Context.RequestStop();
                                })
                                .FixedHeight(4),
                        ]),
                        @yield: ctx => ctx.Text($"  ✓ Output: {outputPath}"),
                        options: new Hex1bFlowSliceOptions { MaxHeight = 6 }
                    );
                }

                // Step 4: Language picker
                await flow.SliceAsync(
                    builder: ctx => ctx.VStack(v =>
                    [
                        v.Text("Select AppHost language:"),
                        v.List(Languages)
                            .OnItemActivated(e =>
                            {
                                language = e.ActivatedText;
                                e.Context.RequestStop();
                            })
                            .FixedHeight(Languages.Length + 1),
                    ]),
                    @yield: ctx => ctx.Text($"  ✓ Language: {language}"),
                    options: new Hex1bFlowSliceOptions { MaxHeight = Languages.Length + 3 }
                );

                // Step 5: Creation spinner
                var creating = true;
                var step = "";

                await flow.SliceAsync(
                    configure: app =>
                    {
                        _ = Task.Run(async () =>
                        {
                            var steps = new[]
                            {
                                "Creating project structure...",
                                "Installing NuGet packages...",
                                "Configuring AppHost...",
                                "Generating solution file...",
                            };

                            foreach (var s in steps)
                            {
                                step = s;
                                app.Invalidate();
                                await Task.Delay(800);
                            }

                            creating = false;
                            app.Invalidate();
                            await Task.Delay(300);
                            app.RequestStop();
                        });

                        return ctx => ctx.HStack(h =>
                        [
                            creating ? h.Spinner(SpinnerStyle.Dots) : h.Text("✓"),
                            h.Text(creating ? $" {step}" : " Project created successfully!"),
                        ]);
                    },
                    @yield: ctx => ctx.Text($"  ✓ Project created at {outputPath}"),
                    options: new Hex1bFlowSliceOptions { MaxHeight = 1 }
                );

            }, options => options.InitialCursorRow = cursorRow)
            .Build()
            .RunAsync();

        // Summary output (normal Console.WriteLine after flow completes)
        Console.WriteLine();
        Console.WriteLine($"The project \"{projectName}\" has been created at \"{outputPath}\".");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  cd {outputPath}");
        Console.WriteLine($"  dotnet run --project {projectName}.AppHost");
    }
}
