using Hex1b;
using Hex1b.Integrations.Spectre.SpectreConsole;
using Spectre.Console;

// SpectreConsoleDemo - Demonstrates running a Spectre.Console workload inside a
// Hex1b terminal. The Spectre app emits its usual rich output (figlet, table,
// progress bars), but every byte flows through Hex1b's workload pipeline so it
// can be recorded to an asciinema cast file.

var castPath = Path.Combine(AppContext.BaseDirectory, "spectre-console-demo.cast");

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithAsciinemaRecording(castPath)
    .WithSpectreConsole(async (console, ct) =>
    {
        console.Write(new FigletText("Hex1b ⨯ Spectre").Color(Color.Aqua));
        console.MarkupLine("[grey]Spectre.Console is rendering, but Hex1b owns the terminal.[/]");
        console.WriteLine();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Feature[/]");
        table.AddColumn("[yellow]Provided by[/]");
        table.AddRow("Markup, tables, figlet, charts", "Spectre.Console");
        table.AddRow("Recording, presentation, muxing", "Hex1b");
        table.AddRow("Live displays, prompts", "Spectre.Console");
        table.AddRow("Embedding, headless capture", "Hex1b");
        console.Write(table);
        console.WriteLine();

        await console.Progress()
            .StartAsync(async progress =>
            {
                var task = progress.AddTask("[green]Pretending to do work[/]");
                while (!task.IsFinished)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    task.Increment(5);
                    await Task.Delay(50, ct);
                }
            });

        console.MarkupLine($"[green]Done![/] Recording saved to [yellow]{castPath}[/]");
    })
    .Build();

return await terminal.RunAsync();
