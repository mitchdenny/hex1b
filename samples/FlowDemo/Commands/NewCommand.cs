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

                var step = flow.Step(ctx => ctx.VStack(v =>
                [
                    v.Text("Select a project template:"),
                    v.List(Templates.Select(t => t.Name).ToArray())
                        .OnItemActivated(e =>
                        {
                            templateIndex = e.ActivatedIndex;
                            selectedTemplate = Templates[e.ActivatedIndex].Id;
                            ctx.Step.Complete(y => y.Text($"  ✓ Template: {Templates[templateIndex].Name}"));
                        })
                        .FixedHeight(Templates.Length + 1),
                ]),
                    options: opts => opts.MaxHeight = Templates.Length + 3
                );
                await step.WaitForCompletionAsync();

                // Step 2: Project name
                if (string.IsNullOrEmpty(projectName))
                {
                    var nameStep = flow.Step(ctx => ctx.VStack(v =>
                    [
                        v.Text("Enter your project name:"),
                        v.TextBox(projectName)
                            .OnSubmit(e =>
                            {
                                projectName = e.Text;
                                ctx.Step.Complete(y => y.Text($"  ✓ Project name: {projectName}"));
                            })
                            .FillWidth(),
                    ]),
                        options: opts => opts.MaxHeight = 4
                    );
                    await nameStep.WaitForCompletionAsync();
                }

                // Step 3: Output directory
                if (string.IsNullOrEmpty(outputPath))
                {
                    var defaultPath = $"./{projectName}";
                    outputPath = defaultPath;

                    var dirStep = flow.Step(ctx => ctx.VStack(v =>
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
                                ctx.Step.Complete(y => y.Text($"  ✓ Output: {outputPath}"));
                            })
                            .FixedHeight(4),
                    ]),
                        options: opts => opts.MaxHeight = 6
                    );
                    await dirStep.WaitForCompletionAsync();
                }

                // Step 4: Language picker
                var langStep = flow.Step(ctx => ctx.VStack(v =>
                [
                    v.Text("Select AppHost language:"),
                    v.List(Languages)
                        .OnItemActivated(e =>
                        {
                            language = e.ActivatedText;
                            ctx.Step.Complete(y => y.Text($"  ✓ Language: {language}"));
                        })
                        .FixedHeight(Languages.Length + 1),
                ]),
                    options: opts => opts.MaxHeight = Languages.Length + 3
                );
                await langStep.WaitForCompletionAsync();

                // Step 5: Creation spinner
                var creating = true;
                var currentStep = "";

                var spinnerStep = flow.Step(ctx => ctx.HStack(h =>
                [
                    creating ? h.Spinner(SpinnerStyle.Dots) : h.Text("✓"),
                    h.Text(creating ? $" {currentStep}" : " Project created successfully!"),
                ]),
                    options: opts => opts.MaxHeight = 1
                );

                var steps = new[]
                {
                    "Creating project structure...",
                    "Installing NuGet packages...",
                    "Configuring AppHost...",
                    "Generating solution file...",
                };

                foreach (var s in steps)
                {
                    currentStep = s;
                    spinnerStep.Invalidate();
                    await Task.Delay(800);
                }

                creating = false;
                spinnerStep.Invalidate();
                await Task.Delay(300);
                await spinnerStep.CompleteAsync(y => y.Text($"  ✓ Project created at {outputPath}"));

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
