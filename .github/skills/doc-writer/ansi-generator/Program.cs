// =============================================================================
// ANSI Generator Template - FOR AI AGENTS
// =============================================================================
// This is a TEMPLATE file. DO NOT modify this file directly.
//
// Agent Workflow:
//   1. Copy this entire directory to a temporary location:
//      mkdir -p .tmp-ansi-gen && cp .github/skills/doc-writer/ansi-generator/* .tmp-ansi-gen/
//
//   2. Fix the project reference path in the copied AnsiGenerator.csproj:
//      Change: Include="../../../../src/Hex1b/Hex1b.csproj"
//      To:     Include="../src/Hex1b/Hex1b.csproj"
//
//   3. Modify the copy's Program.cs (the GenerateSnapshots method)
//
//   4. Run from the copy:
//      cd .tmp-ansi-gen && dotnet run -- output
//
//   5. Copy outputs to static site:
//      cp output/*.ansi ../src/content/public/ansi/
//
//   6. Clean up:
//      cd .. && rm -rf .tmp-ansi-gen
//
//   7. Use in markdown:
//      <StaticTerminal file="ansi/your-file.ansi" title="Title" :cols="80" :rows="24" />
//      OR
//      <StaticCodeBlock ansiFile="ansi/your-file.ansi" terminalTitle="Title" :cols="60" :rows="6">
//      v.YourCodeHere()
//      </StaticCodeBlock>
//
// See .github/skills/doc-writer/SKILL.md for detailed instructions.
// =============================================================================

using Hex1b;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

class Program
{
    static async Task Main(string[] args)
    {
        var outputDir = args.Length > 0 ? args[0] : "output";
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"AnsiGenerator - Generating static ANSI snapshots to: {outputDir}");
        Console.WriteLine();

        await GenerateSnapshots(outputDir);

        Console.WriteLine();
        Console.WriteLine("Done! Generated ANSI files:");
        foreach (var file in Directory.GetFiles(outputDir, "*.ansi"))
        {
            Console.WriteLine($"  {Path.GetFileName(file)}");
        }
        Console.WriteLine();
        Console.WriteLine("Copy files to: src/content/public/ansi/");
    }

    // =========================================================================
    // MODIFY THIS METHOD to generate the snapshots you need
    // =========================================================================
    static async Task GenerateSnapshots(string outputDir)
    {
        // Example: Basic text display
        await GenerateSnapshot(outputDir, "example-basic", "Basic Example", 80, 24,
            ctx => ctx.VStack(v => [
                v.Text("═══ Example Widget ═══"),
                v.Text(""),
                v.Text("This is a static ANSI screenshot."),
                v.Text("Modify GenerateSnapshots() to render your own content.")
            ]));

        // Add more snapshots here...
        // await GenerateSnapshot(outputDir, "name", "Title", width, height,
        //     ctx => ctx.YourWidget(...));
    }

    static async Task GenerateSnapshot(
        string outputDir,
        string name,
        string description,
        int width,
        int height,
        Func<RootContext, Hex1bWidget> widgetBuilder)
    {
        Console.WriteLine($"  Generating: {name} ({description})");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, width, height);

        using var app = new Hex1bApp(
            ctx => widgetBuilder(ctx),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for initial render
        await Task.Delay(100);

        // Capture the snapshot
        var snapshot = terminal.CreateSnapshot();
        var ansi = snapshot.ToAnsi(new TerminalAnsiOptions
        {
            IncludeClearScreen = false,
            ResetAtEnd = true,
            IncludeCursorPosition = false,
            IncludeTrailingNewline = true
        });

        // Write to file
        var filePath = Path.Combine(outputDir, $"{name}.ansi");
        await File.WriteAllTextAsync(filePath, ansi);

        // Cancel the app
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }
}
