using System.Globalization;
using Spectre.Console;

namespace SpectreConsoleDemo;

/// <summary>
/// A self-contained, looping Spectre.Console showcase. The entry point hands
/// us an <see cref="IAnsiConsole"/> (provided by Hex1b's bridge) and we drive
/// every interaction through that interface — there is no direct knowledge of
/// Hex1b in here, which is the whole point of the bridge.
/// </summary>
internal static class InteractiveDemo
{
    /// <summary>
    /// The canonical menu order. Exposed publicly so <see cref="DemoAutomator"/>
    /// can drive the same items in the same order without duplicating the list.
    /// </summary>
    public static IReadOnlyList<string> MenuItems { get; } =
    [
        "Markup, Rules & Panels",
        "Tables",
        "Trees",
        "Calendar",
        "Bar Chart",
        "Breakdown Chart",
        "Status Spinner",
        "Live Display",
        "Progress",
        "Prompts",
        "Quit",
    ];

    public const string QuitItem = "Quit";

    public static async Task RunAsync(IAnsiConsole console, CancellationToken ct)
    {
        var smallFont = LoadSmallFont();
        console.Write(new FigletText(smallFont, "Hex1b").Color(Color.Aqua));
        console.MarkupLine("    [red]\u2764[/]  [bold yellow]Spectre Console[/]");
        console.MarkupLine("[grey]An interactive showcase of Spectre.Console controls running inside a Hex1b terminal.[/]");
        console.MarkupLine("[grey]Use [yellow]\u2191/\u2193[/] and [yellow]Enter[/] to navigate. Pick [red]Quit[/] (last item) to exit.[/]");
        console.WriteLine();

        while (!ct.IsCancellationRequested)
        {
            var choice = await console.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("[bold aqua]Pick a demo[/] [grey](or [red]Quit[/] to exit)[/]")
                    .HighlightStyle(new Style(foreground: Color.Black, background: Color.Aqua))
                    .PageSize(MenuItems.Count)
                    .UseConverter(item => string.Equals(item, QuitItem, StringComparison.Ordinal) ? "[red bold]Quit[/]" : item)
                    .AddChoices(MenuItems),
                ct);

            if (string.Equals(choice, QuitItem, StringComparison.Ordinal))
            {
                console.MarkupLine("[green]Goodbye![/]");
                return;
            }

            console.WriteLine();
            console.Write(new Rule($"[yellow]{choice}[/]").LeftJustified().RuleStyle("grey"));
            console.WriteLine();

            switch (choice)
            {
                case "Markup, Rules & Panels":
                    RunMarkupDemo(console);
                    break;
                case "Tables":
                    RunTablesDemo(console);
                    break;
                case "Trees":
                    RunTreesDemo(console);
                    break;
                case "Calendar":
                    RunCalendarDemo(console);
                    break;
                case "Bar Chart":
                    RunBarChartDemo(console);
                    break;
                case "Breakdown Chart":
                    RunBreakdownChartDemo(console);
                    break;
                case "Status Spinner":
                    await RunStatusDemoAsync(console, ct);
                    break;
                case "Live Display":
                    await RunLiveDisplayDemoAsync(console, ct);
                    break;
                case "Progress":
                    await RunProgressDemoAsync(console, ct);
                    break;
                case "Prompts":
                    await RunPromptsDemoAsync(console, ct);
                    break;
            }

            console.WriteLine();
            console.MarkupLine("[grey]Press [yellow]Enter[/] to return to the menu...[/]");
            await WaitForEnterAsync(console, ct);
            console.Clear();
        }
    }

    private static void RunMarkupDemo(IAnsiConsole console)
    {
        console.MarkupLine("[bold aqua]Spectre.Console[/] supports a rich [italic yellow]markup[/] syntax.");
        console.MarkupLine("[red]Errors[/], [green]successes[/], and [grey]hints[/] all render with semantic colours.");
        console.MarkupLine("Hyperlinks render as [link=https://spectreconsole.net]actual OSC-8 links[/] in capable terminals.");
        console.WriteLine();

        var panel = new Panel(
            new Markup("[bold]Panels[/] frame any renderable.\nUse them for callouts, summaries, or boxed sections."))
        {
            Header = new PanelHeader("[yellow] Panel Header [/]", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Aqua),
            Padding = new Padding(2, 1),
        };
        console.Write(panel);

        console.Write(new Rule("[grey]Rules separate sections[/]").RuleStyle("grey").DoubleBorder());

        console.Write(new Columns(
            new Panel("Column 1") { Border = BoxBorder.Square },
            new Panel("Column 2") { Border = BoxBorder.Square },
            new Panel("Column 3") { Border = BoxBorder.Square }));
    }

    private static void RunTablesDemo(IAnsiConsole console)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold yellow]Hex1b \u2764 Spectre Capability Matrix[/]")
            .AddColumn(new TableColumn("[yellow]Feature[/]"))
            .AddColumn(new TableColumn("[yellow]Provided by[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Notes[/]"));

        table.AddRow("Markup, tables, charts", "[aqua]Spectre.Console[/]", "Rich text, structured layouts");
        table.AddRow("Recording / muxing / web", "[aqua]Hex1b[/]", "asciinema, presentation adapters");
        table.AddRow("Live displays, prompts", "[aqua]Spectre.Console[/]", "Status, Live, SelectionPrompt");
        table.AddRow("Embedding, headless capture", "[aqua]Hex1b[/]", "TerminalWidget + WithHeadless()");
        table.AddRow("Test automation", "[aqua]Both[/]", "Spectre.Console runs, Hex1b drives");

        console.Write(table);
    }

    private static void RunTreesDemo(IAnsiConsole console)
    {
        var tree = new Tree("[bold yellow]src/[/]");

        var hex1b = tree.AddNode("[aqua]Hex1b[/]");
        hex1b.AddNode("Hex1bApp.cs");
        hex1b.AddNode("Hex1bTerminal.cs");
        var widgets = hex1b.AddNode("Widgets/");
        widgets.AddNode("ButtonWidget.cs");
        widgets.AddNode("TextBlockWidget.cs");
        widgets.AddNode("[grey](dozens more)[/]");

        var integrations = tree.AddNode("[aqua]Hex1b.Integrations.Spectre[/]");
        var sc = integrations.AddNode("SpectreConsole/");
        sc.AddNode("Hex1bAnsiConsole.cs");
        sc.AddNode("Hex1bAnsiConsoleInput.cs");
        sc.AddNode("Hex1bAnsiConsoleOutput.cs");
        var st = integrations.AddNode("SpectreTui/");
        st.AddNode("Hex1bSpectreTuiTerminal.cs");
        st.AddNode("Hex1bSpectreTuiInputReader.cs");

        console.Write(tree);
    }

    private static void RunCalendarDemo(IAnsiConsole console)
    {
        var calendar = new Spectre.Console.Calendar(2026, 5)
            .HighlightStyle(new Style(Color.Yellow, decoration: Decoration.Bold))
            .HeaderStyle(new Style(Color.Aqua, decoration: Decoration.Bold))
            .AddCalendarEvent(2026, 5, 8)
            .AddCalendarEvent(2026, 5, 14)
            .AddCalendarEvent(2026, 5, 21);

        console.Write(calendar);
        console.MarkupLine("[grey]Calendar with three highlighted events.[/]");
    }

    private static void RunBarChartDemo(IAnsiConsole console)
    {
        var chart = new BarChart()
            .Width(60)
            .Label("[bold yellow]Hex1b stars per month (sample data)[/]")
            .CenterLabel()
            .AddItem("Jan", 12, Color.Aqua)
            .AddItem("Feb", 28, Color.Aqua)
            .AddItem("Mar", 45, Color.Yellow)
            .AddItem("Apr", 63, Color.Yellow)
            .AddItem("May", 88, Color.Green)
            .AddItem("Jun", 124, Color.Green);

        console.Write(chart);
    }

    private static void RunBreakdownChartDemo(IAnsiConsole console)
    {
        var chart = new BreakdownChart()
            .Width(72)
            .AddItem("C#", 78, Color.Aqua)
            .AddItem("Markdown", 9, Color.Yellow)
            .AddItem("XML", 6, Color.Green)
            .AddItem("PowerShell", 4, Color.Magenta1)
            .AddItem("Other", 3, Color.Grey);

        console.Write(chart);
        console.MarkupLine("[grey]Approximate language breakdown for the Hex1b repository.[/]");
    }

    private static async Task RunStatusDemoAsync(IAnsiConsole console, CancellationToken ct)
    {
        var stages = new[]
        {
            ("[aqua]Reticulating splines...[/]", "dots"),
            ("[aqua]Negotiating with the terminal...[/]", "earth"),
            ("[aqua]Hydrating widget tree...[/]", "moon"),
            ("[green]Done![/]", "star"),
        };

        await console.Status()
            .AutoRefresh(true)
            .StartAsync("[grey]Starting...[/]", async ctx =>
            {
                foreach (var (label, spinner) in stages)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    ctx.Status(label);
                    ctx.Spinner(spinner switch
                    {
                        "dots" => Spinner.Known.Dots,
                        "earth" => Spinner.Known.Earth,
                        "moon" => Spinner.Known.Moon,
                        _ => Spinner.Known.Star,
                    });
                    ctx.Refresh();
                    await Task.Delay(600, ct);
                }
            });

        console.MarkupLine("[green]Status demo finished.[/]");
    }

    private static async Task RunLiveDisplayDemoAsync(IAnsiConsole console, CancellationToken ct)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold yellow]Live Display[/]")
            .AddColumn("[yellow]Tick[/]")
            .AddColumn("[yellow]Timestamp[/]")
            .AddColumn("[yellow]Value[/]");

        await console.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                for (var i = 1; i <= 6; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    table.AddRow(
                        i.ToString(CultureInfo.InvariantCulture),
                        DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        $"[aqua]{i * i}[/]");
                    ctx.Refresh();
                    await Task.Delay(350, ct);
                }
            });

        console.MarkupLine("[green]Live display finished.[/]");
    }

    private static async Task RunProgressDemoAsync(IAnsiConsole console, CancellationToken ct)
    {
        await console.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async progress =>
            {
                var build = progress.AddTask("[green]Building[/]", maxValue: 100);
                var test = progress.AddTask("[yellow]Testing[/]", maxValue: 100);

                while (!progress.IsFinished && !ct.IsCancellationRequested)
                {
                    build.Increment(7);
                    test.Increment(4);
                    await Task.Delay(60, ct);
                }
            });

        console.MarkupLine("[green]Progress demo finished.[/]");
    }

    private static async Task RunPromptsDemoAsync(IAnsiConsole console, CancellationToken ct)
    {
        var name = await console.AskAsync<string>("[aqua]What's your name?[/]", ct);
        console.MarkupLine($"[grey]Hello, [yellow]{Markup.Escape(name)}[/]![/]");

        var keepGoing = await console.ConfirmAsync("[aqua]Ready to keep going?[/]", defaultValue: true, ct);
        console.MarkupLine(keepGoing ? "[green]Great![/]" : "[red]Bailing out.[/]");

        var luckyNumber = await console.PromptAsync(
            new TextPrompt<int>("[aqua]Pick a number between 1 and 10:[/]")
                .Validate(n => n is >= 1 and <= 10 ? ValidationResult.Success() : ValidationResult.Error("[red]Out of range[/]")),
            ct);
        console.MarkupLine($"[grey]You picked [yellow]{luckyNumber}[/].[/]");

        var picked = await console.PromptAsync(
            new MultiSelectionPrompt<string>()
                .Title("[aqua]Pick your favourite features:[/]")
                .NotRequired()
                .InstructionsText("[grey]Press [yellow]<space>[/] to toggle, [yellow]<enter>[/] to accept[/]")
                .AddChoices("Recording", "Muxing", "Embedding", "Headless capture", "Automation"),
            ct);

        if (picked.Count == 0)
        {
            console.MarkupLine("[grey]No favourites picked.[/]");
        }
        else
        {
            console.MarkupLine($"[grey]Favourites: [yellow]{string.Join(", ", picked)}[/][/]");
        }

        console.MarkupLine("[green]All prompts complete.[/]");
    }

    private static async Task WaitForEnterAsync(IAnsiConsole console, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var key = await console.Input.ReadKeyAsync(intercept: true, ct);
            if (key is null)
            {
                return;
            }

            if (key.Value.Key == ConsoleKey.Enter)
            {
                return;
            }
        }
    }

    private static FigletFont LoadSmallFont()
    {
        // The Small font (by Glenn Chappell, public license) ships next to the
        // assembly via <Content Include="fonts\Small.flf" />. It's 5 rows tall
        // (vs Standard's 6) with full smushing for a tighter, more compact
        // header — much friendlier than Standard.flf when terminals are short.
        var fontPath = Path.Combine(AppContext.BaseDirectory, "fonts", "Small.flf");
        return FigletFont.Load(fontPath);
    }
}
