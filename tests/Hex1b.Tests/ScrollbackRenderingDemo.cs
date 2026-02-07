using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Generates sample SVG and HTML files demonstrating scrollback rendering.
/// Run with: dotnet test --filter "FullyQualifiedName~ScrollbackRenderingDemo"
/// </summary>
public class ScrollbackRenderingDemo
{
    [Fact]
    public void GenerateScrollbackDemo()
    {
        // Create a terminal with scrollback enabled
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .WithScrollback(1000)
            .Build();

        // Write 40 lines of colored content into a 24-row terminal
        // This pushes 16 lines into scrollback
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= 40; i++)
        {
            var color = i switch
            {
                <= 10 => "31", // red (these will be in scrollback)
                <= 20 => "33", // yellow (partially in scrollback)
                <= 30 => "32", // green (visible area)
                _ => "36",     // cyan (visible area)
            };
            sb.Append($"\x1b[{color}m");
            sb.Append($"Line {i,3}: ");
            sb.Append(new string((char)('A' + (i % 26)), 60));
            sb.Append("\x1b[0m");
            if (i < 40) sb.Append("\r\n");
        }
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(sb.ToString()));

        // Create snapshot with 10 scrollback lines
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 10);

        Assert.Equal(10, snapshot.ScrollbackLineCount);
        Assert.Equal(34, snapshot.Height); // 10 scrollback + 24 visible

        // Write SVG output
        var svgContent = snapshot.ToSvg();
        var outputDir = Path.Combine(Path.GetTempPath(), "hex1b-scrollback-demo");
        Directory.CreateDirectory(outputDir);

        var svgPath = Path.Combine(outputDir, "scrollback-demo.svg");
        File.WriteAllText(svgPath, svgContent);

        // Write HTML output
        var htmlContent = snapshot.ToHtml();
        var htmlPath = Path.Combine(outputDir, "scrollback-demo.html");
        File.WriteAllText(htmlPath, htmlContent);

        // Output paths for the user
        Console.WriteLine($"SVG:  {svgPath}");
        Console.WriteLine($"HTML: {htmlPath}");

        // Verify the SVG contains the scrollback separator line
        Assert.Contains("stroke-dasharray", svgContent);
    }
}
