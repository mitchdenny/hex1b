namespace Hex1b.Tests.NativeAot;

// Reconstruct only the pre-experiment construction regions. This keeps the historical
// measurements runnable without a checkout of an older revision or a shallow-clone fetch.
internal static class ConsoleConstructionBaseline
{
    internal static Dictionary<string, string> ReadAll()
    {
        var app = ReadCurrent("Hex1bApp.cs");
        app = Replace(app, "internal Hex1bApp(", "public Hex1bApp(", expected: 2);
        app = Replace(app, """
                    _adapter = options.WorkloadAdapter
                        ?? throw new ArgumentException("WorkloadAdapter is required. Create apps with Hex1bTerminal.CreateBuilder().WithHex1bApp().", nameof(options));
            """, "");
        app = Replace(app, "    private readonly Hex1bRenderContext _context;",
            "    private readonly Hex1bTerminal? _ownedTerminal; // Terminal we created and should dispose\n" +
            "    private readonly Hex1bRenderContext _context;");
        app = Replace(app, "        // Propagate the app's mouse-enabled option", """
                    // Create or use provided adapter
                    if (options.WorkloadAdapter != null)
                    {
                        // Use provided adapter directly
                        _adapter = options.WorkloadAdapter;
                        _ownedTerminal = null;
                    }
                    else
                    {
                        // Default: create console terminal using new architecture
                        var presentation = new ConsolePresentationAdapter(enableMouse: _mouseEnabled);
                        var workload = new Hex1bAppWorkloadAdapter(presentation) { EnableMouse = _mouseEnabled };
                        _ownedTerminal = new Hex1bTerminal(new Hex1bTerminalOptions
                        {
                            PresentationAdapter = presentation,
                            WorkloadAdapter = workload
                        });
                        _adapter = workload;
                    }

                    // Propagate the app's mouse-enabled option
            """);
        app = Replace(app, "        // Dispose the adapter\n", """
                    // Dispose the owned terminal if we created it
                    // The terminal handles writing mouse disable sequences directly to the console
                    _ownedTerminal?.Dispose();

                    // Dispose the adapter

            """);
        app = Replace(app, "        // Dispose the adapter asynchronously\n", """
                    // Dispose the owned terminal if we created it
                    // The terminal handles writing mouse disable sequences directly to the console
                    if (_ownedTerminal != null)
                    {
                        await _ownedTerminal.DisposeAsync();
                    }

                    // Dispose the adapter asynchronously

            """);

        var builder = ReadCurrent("Hex1bTerminalBuilder.cs");
        builder = Replace(builder,
            "private Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter>? _presentationFactory;",
            "private Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter> _presentationFactory = \n" +
            "        builder => new ConsolePresentationAdapter(enableMouse: builder._enableMouse, preserveOPost: builder._preserveOPost);");
        builder = Replace(builder, """
                public Hex1bTerminalBuilder()
                    : this(builder => new ConsolePresentationAdapter(enableMouse: builder._enableMouse, preserveOPost: builder._preserveOPost))
                {
                }

                private Hex1bTerminalBuilder(Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter>? presentationFactory)
                {
                    _presentationFactory = presentationFactory;
                }

                internal static Hex1bTerminalBuilder CreateSlim() => new(presentationFactory: null);
            """, """
                public Hex1bTerminalBuilder()
                {
                }
            """);
        builder = Replace(builder, """
                    if (_presentationFactory is null)
                    {
                        throw new InvalidOperationException(
                            "No presentation configured. Call WithPresentation() or WithHeadless() before Build().");
                    }

            """, "");

        var terminal = ReadCurrent("Hex1bTerminal.cs");
        var start = terminal.IndexOf("    /// <summary>\n    /// Creates a terminal builder without a default presentation", StringComparison.Ordinal);
        var end = terminal.IndexOf("    // === Constructor ===", start, StringComparison.Ordinal);
        if (start < 0 || end <= start)
            throw new InvalidDataException("Slim builder declaration changed; review the construction baseline.");
        terminal = terminal[..start] + terminal[end..];
        return new()
        {
            ["Hex1bApp.cs"] = app,
            ["Hex1bTerminalBuilder.cs"] = builder,
            ["Hex1bTerminal.cs"] = terminal
        };
    }

    internal static Dictionary<string, string> ReadCurrent()
        => new[] { "Hex1bApp.cs", "Hex1bTerminalBuilder.cs", "Hex1bTerminal.cs" }
            .ToDictionary(name => name, ReadCurrent);

    private static string ReadCurrent(string name)
        => File.ReadAllText(Path.Combine(ScenarioRunner.RepositoryRoot, "src", "Hex1b", name))
            .ReplaceLineEndings("\n");

    private static string Replace(string source, string before, string after, int expected = 1)
    {
        before = before.ReplaceLineEndings("\n");
        if (source.Split(before, StringSplitOptions.None).Length != expected + 1)
            throw new InvalidDataException("App/builder source changed; review the pre-experiment construction baseline.");
        return source.Replace(before, after.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
