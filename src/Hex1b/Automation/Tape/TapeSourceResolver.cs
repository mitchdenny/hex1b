namespace Hex1b.Automation;

internal static class TapeSourceResolver
{
    internal static async Task<IReadOnlyList<TapeCommand>> ResolveAsync(
        TapeDocument document,
        TapeParser parser,
        string workingDirectory,
        List<TapeDiagnostic> diagnostics,
        CancellationToken ct)
    {
        var commands = new List<TapeCommand>();
        var activeFiles = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var sources = new Dictionary<string, TapeDocument>(activeFiles.Comparer);
        if (document.SourceName is { } name && Path.IsPathFullyQualified(name))
            activeFiles.Add(Path.GetFullPath(name));
        await ExpandAsync(document, 0);
        return commands.AsReadOnly();

        async Task ExpandAsync(TapeDocument current, int depth)
        {
            var snapshot = current.Commands.ToArray();
            if (snapshot.Any(command => command is null))
            {
                diagnostics.Add(new("TAPE_DOCUMENT", TapeDiagnosticSeverity.Error, TapeDiagnosticStage.Preflight,
                    "Commands cannot contain null entries.", new(current.SourceName, 0, 0, 1, 1)));
                return;
            }
            foreach (var command in snapshot)
            {
                ct.ThrowIfCancellationRequested();
                if (depth > 0 && command is TapeOutputCommand)
                {
                    commands.Add(command with { IsIncludedOutput = true });
                    continue;
                }

                if (command is not TapeSourceCommand source)
                {
                    commands.Add(command);
                    continue;
                }
                // Preserve its callback for preparation, even though include expansion itself happens here.
                commands.Add(source);
                if (depth >= 256)
                {
                    AddError("The Source nesting depth exceeds the safe resolution limit of 256.", source.Span);
                    continue;
                }
                if (Path.GetExtension(source.Path) != ".tape")
                {
                    AddError("Source paths must have the exact '.tape' extension.", source.Span);
                    continue;
                }

                string path;
                try
                {
                    path = Path.GetFullPath(source.Path, workingDirectory);
                }
                catch (ArgumentException ex)
                {
                    AddError(ex.Message, source.Span);
                    continue;
                }
                if (!activeFiles.Add(path))
                {
                    AddError($"Source cycle detected at '{path}'.", source.Span);
                    continue;
                }
                sources.TryGetValue(path, out var included);
                try
                {
                    if (included is null)
                    {
                        var bytes = await File.ReadAllBytesAsync(path, ct);
                        if (bytes.Length == 0)
                            AddError($"Source '{path}' is empty.", source.Span);
                        else
                        {
                            using var input = new MemoryStream(bytes, writable: false);
                            included = await parser.ParseAsync(input, path, ct);
                            sources.Add(path, included);
                        }
                    }
                }
                catch (TapeParseException ex)
                {
                    diagnostics.AddRange(ex.Diagnostics);
                }
                catch (IOException ex)
                {
                    AddError($"Cannot read Source '{path}': {ex.Message}", source.Span);
                }
                catch (UnauthorizedAccessException ex)
                {
                    AddError($"Cannot read Source '{path}': {ex.Message}", source.Span);
                }
                if (included is not null)
                    await ExpandAsync(included, depth + 1);
                activeFiles.Remove(path);
            }
        }

        void AddError(string message, TapeSourceSpan span) => diagnostics.Add(new(
            "TAPE_SOURCE", TapeDiagnosticSeverity.Error, TapeDiagnosticStage.Resolution, message, span));
    }
}
