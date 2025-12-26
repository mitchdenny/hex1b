// =============================================================================
// Static Generator Template - FOR AI AGENTS
// =============================================================================
// This is a TEMPLATE file. DO NOT modify this file directly.
//
// This generator creates SVG files for static terminal previews.
// The StaticTerminalPreview Vue component parses cell data directly from the
// SVG DOM structure for hover tooltips - no separate HTML file needed.
//
// Agent Workflow:
//   1. Copy this entire directory to a temporary location:
//      mkdir -p .tmp-static-gen && cp .github/skills/doc-writer/static-generator/* .tmp-static-gen/
//
//   2. Fix the project reference path in the copied StaticGenerator.csproj:
//      Change: Include="../../../../src/Hex1b/Hex1b.csproj"
//      To:     Include="../src/Hex1b/Hex1b.csproj"
//
//   3. Modify the copy's Program.cs (the GenerateSnapshots method)
//
//   4. Run from the copy:
//      cd .tmp-static-gen && dotnet run -- output
//
//   5. Copy outputs to static site:
//      cp output/*.svg ../src/content/public/svg/
//
//   6. Clean up:
//      cd .. && rm -rf .tmp-static-gen
//
//   7. Use in markdown:
//      <StaticTerminalPreview svgPath="/svg/your-file.svg">
//
//      ```csharp
//      v.YourCodeHere()
//      ```
//
//      </StaticTerminalPreview>
//
// NOTE: This template generates SVG WITHOUT cursor visibility by default.
//       The cursor is hidden because documentation screenshots should show clean
//       output without a random cursor block confusing readers. If you need to
//       show cursor position (e.g., for TextBox focus documentation), change
//       the ToSvg call to use the snapshot directly instead of casting to
//       IHex1bTerminalRegion.
//
// See .github/skills/doc-writer/SKILL.md for detailed instructions.
// =============================================================================

using Hex1b;
using Hex1b.Terminal;
using Hex1b.Terminal.Testing;
using Hex1b.Widgets;

class Program
{
    static async Task Main(string[] args)
    {
        var outputDir = args.Length > 0 ? args[0] : "output";
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"StaticGenerator - Generating SVG snapshots to: {outputDir}");
        Console.WriteLine();

        await GenerateSnapshots(outputDir);

        Console.WriteLine();
        Console.WriteLine("Done! Generated files:");
        foreach (var file in Directory.GetFiles(outputDir, "*.svg"))
        {
            Console.WriteLine($"  {Path.GetFileName(file)}");
        }
        Console.WriteLine();
        Console.WriteLine("Copy files to: src/content/public/svg/");
    }

    // =========================================================================
    // MODIFY THIS METHOD to generate the snapshots you need
    // =========================================================================
    static async Task GenerateSnapshots(string outputDir)
    {
        // Example: Basic text display
        await GenerateSnapshot(outputDir, "example-basic", "Basic Example", 50, 5,
            ctx => ctx.VStack(v => [
                v.Text("═══ Example Widget ═══"),
                v.Text(""),
                v.Text("This is a static SVG screenshot."),
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
        
        var svgOptions = new TerminalSvgOptions
        {
            ShowCellGrid = false,
            ShowPixelGrid = false,
            DefaultBackground = "#0f0f1a",
            DefaultForeground = "#e0e0e0",
            FontFamily = "'Cascadia Code', 'Fira Code', 'JetBrains Mono', Consolas, monospace",
            FontSize = 14,
            CellWidth = 9,
            CellHeight = 18
        };
        
        // Generate SVG WITHOUT cursor visibility
        // Cast to IHex1bTerminalRegion to use overload that doesn't include cursor position.
        // This keeps documentation screenshots clean without random cursor blocks.
        // If you need to show cursor (e.g., TextBox focus), use snapshot.ToSvg() directly.
        var region = (IHex1bTerminalRegion)snapshot;
        
        var svg = region.ToSvg(svgOptions);
        var svgPath = Path.Combine(outputDir, $"{name}.svg");
        await File.WriteAllTextAsync(svgPath, svg);

        // Cancel the app
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }
}
