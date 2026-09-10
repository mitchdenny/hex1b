using System.Collections;
using Hex1b.Layout;

namespace Hex1b.Automation;

internal static class TapePreparation
{
    internal static async Task<TapePreparedRun> PrepareAsync(TapeDocument tape,
        TapePlaybackOptions options, bool? ownsShell, TimeProvider clock, CancellationToken ct)
        => Prepare(await ResolveAsync(tape, options, ct), options, ownsShell, clock, ct);

    internal static async Task<TapeResolvedSource> ResolveAsync(TapeDocument tape,
        TapePlaybackOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();
        var workingDirectory = Path.GetFullPath(options.WorkingDirectory ?? Environment.CurrentDirectory);
        var diagnostics = new List<TapeDiagnostic>();
        var commands = await TapeSourceResolver.ResolveAsync(tape, tape.Parser, workingDirectory,
            diagnostics, ct);
        return new(tape.SourceName, workingDirectory, commands, diagnostics);
    }

    internal static TapePreparedRun Prepare(TapeResolvedSource source, TapePlaybackOptions options,
        bool? ownsShell, TimeProvider clock, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var workingDirectory = source.WorkingDirectory;
        var diagnostics = source.Diagnostics.ToList();
        var rootSpan = new TapeSourceSpan(source.SourceName, 0, 0, 1, 1);
        var commands = source.Commands;
        var artifacts = new List<TapeArtifact>();
        Size? dimensions = options.TerminalSize;
        var overwrite = options.Capture?.Overwrite ?? false;
        var environment = new Dictionary<string, string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var explicitEnvironment = new HashSet<string>(environment.Comparer);
        var shellName = options.DefaultShell ?? (OperatingSystem.IsWindows() ? "cmd" : "bash");

        if (!Directory.Exists(workingDirectory))
            Error($"Working directory does not exist: '{workingDirectory}'.", rootSpan);
        if (dimensions is { } size && (size.Width <= 0 || size.Height <= 0))
            Error("TerminalSize must have positive columns and rows.", rootSpan);

        if (ownsShell is null)
            diagnostics.Add(new("TAPE_FACTORY_VALIDATION", TapeDiagnosticSeverity.Warning, TapeDiagnosticStage.Preflight,
                "The terminal factory is not invoked during validation. Workload-specific shell settings and target capabilities are checked during playback.",
                rootSpan));

        if (ownsShell == false && (options.DefaultShell is not null || options.Environment is not null || !options.InheritEnvironment))
            Error("Shell launch options apply only to the player's default owned shell, not a borrowed terminal or custom workload.", rootSpan);

        if (ownsShell == true)
        {
            if (options.InheritEnvironment)
            {
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    if (entry.Key is string key && entry.Value is string value)
                        environment[key] = value;
                }
            }
            if (options.Environment is { } overrides)
            {
                foreach (var pair in overrides.ToArray())
                    AddEnvironment(pair.Key, pair.Value, rootSpan);
            }
        }

        foreach (var command in commands)
        {
            if (command is TapeEnvCommand env && ownsShell == true)
                AddEnvironment(env.Name, env.Value, env.Span);
            if (command is TapeSetCommand setting && setting.Setting == TapeSetting.Shell && ownsShell == true)
                shellName = setting.Value.Value;
        }

        TapeShellConfiguration? shell = null;
        if (ownsShell == true)
        {
            shell = TapeShellConfiguration.ForName(shellName);
            if (shell is null)
                Error($"Shell '{shellName}' has no supported launch profile.", rootSpan);
            else
            {
                foreach (var pair in shell.Defaults)
                {
                    if (!explicitEnvironment.Contains(pair.Key))
                        environment[pair.Key] = pair.Value;
                }
                if (!explicitEnvironment.Contains("TERM"))
                    environment["TERM"] = "xterm-256color";
                var executable = FindExecutable(shell.FileName, rootSpan);
                if (executable is not null)
                    shell = shell with { FileName = executable };
            }
        }

        if (options.Capture?.AsciinemaPath is { } castPath)
            AddArtifact(castPath, TapeArtifactKind.Asciicast, rootSpan);
        var initiallyEnabledGoldenPaths = new List<string>();
        if (options.Capture?.GoldenTextPath is { } goldenPath)
        {
            if (AddArtifact(goldenPath, TapeArtifactKind.GoldenText, rootSpan) is { } fullPath)
                initiallyEnabledGoldenPaths.Add(fullPath);
        }

        var state = new TapeCompilationState(clock);
        var prepared = new List<TapePreparedCommand>();
        string? tapeGoldenPath = null;
        foreach (var command in commands)
        {
            ct.ThrowIfCancellationRequested();
            var context = state.CreateContext(() => PrepareBuiltIn(command));
            var result = command.PrepareCallback is { } callback ? callback(context) : PrepareBuiltIn(command);
            if (result is null)
                throw new InvalidOperationException($"The '{command}' playback callback returned no command result.");
            if (result.ErrorMessage is { } message)
            {
                diagnostics.Add(new(result.DiagnosticCode, TapeDiagnosticSeverity.Error,
                    result.DiagnosticStage, message, command.Span));
            }
            else if (!result.SkipCommand)
            {
                prepared.Add(new(command, result.SequenceBuilder!.Build(), result.CaptureControl, result.GoldenPath));
            }
            if (command is not (TapeSetCommand or TapeOutputCommand or TapeRequireCommand or TapeSourceCommand))
                state.InPreamble = false;
        }

        return new TapePreparedRun
        {
            WorkingDirectory = workingDirectory,
            Commands = prepared.AsReadOnly(),
            Diagnostics = diagnostics.AsReadOnly(),
            Artifacts = artifacts.AsReadOnly(),
            InitiallyEnabledGoldenPaths = initiallyEnabledGoldenPaths.AsReadOnly(),
            TerminalSize = dimensions,
            Overwrite = overwrite,
            Shell = shell,
            Environment = ownsShell == true ? environment : null
        };

        TapeCommandResult PrepareBuiltIn(TapeCommand command)
        {
            switch (command)
            {
                case TapeSourceCommand:
                    return TapeCommandResult.NoInput(skip: true);
                case TapeOutputCommand when command.IsIncludedOutput:
                    return TapeCommandResult.NoInput(skip: true);
                case TapeOutputCommand output:
                    if (Path.GetExtension(output.Path) is not ".ascii" and not ".txt")
                        return TapeCommandResult.PreflightError($"Output '{output.Path}' requires an unsupported rendering backend. Select asciicast through TapeCaptureOptions.");
                    if (tapeGoldenPath is not null && tapeGoldenPath != output.Path)
                        return TapeCommandResult.PreflightError("Multiple distinct text Output destinations in one tape are not supported.");
                    tapeGoldenPath = output.Path;
                    var outputPath = AddArtifact(output.Path, TapeArtifactKind.GoldenText, output.Span);
                    return TapeCommandResult.NoInput(goldenPath: outputPath);
                case TapeEnvCommand:
                    if (ownsShell == false)
                        return TapeCommandResult.PreflightError("Env requires the player's default owned shell; it cannot change a borrowed terminal or custom workload.");
                    return TapeCommandResult.NoInput();
                case TapeRequireCommand require:
                    if (ownsShell == false)
                        return TapeCommandResult.PreflightError("Require cannot be checked in the launch context of a borrowed terminal or custom workload.");
                    if (!state.InPreamble)
                    {
                        Warn("Late Require is ignored, matching VHS's setup ordering.", command.Span);
                        return TapeCommandResult.NoInput(skip: true);
                    }
                    if (ownsShell == true)
                        FindExecutable(require.Program, require.Span);
                    return TapeCommandResult.NoInput();
                case TapeSetCommand setting when setting.Setting == TapeSetting.Shell:
                    if (ownsShell == false)
                        return TapeCommandResult.PreflightError("Set Shell cannot replace a borrowed terminal or a builder-configured workload.");
                    if (!state.InPreamble)
                    {
                        Warn("Shell was selected at startup; late Set Shell is not executed again in the tape body.", command.Span);
                        return TapeCommandResult.NoInput(skip: true);
                    }
                    return TapeCommandResult.NoInput();
                default:
                    return TapeCommandCompilation.Compile(command, state, diagnostics);
            }
        }

        void Error(string message, TapeSourceSpan span) => diagnostics.Add(new(
            "TAPE_PREFLIGHT", TapeDiagnosticSeverity.Error, TapeDiagnosticStage.Preflight, message, span));

        void Warn(string message, TapeSourceSpan span) => diagnostics.Add(new(
            "TAPE_VHS_BEHAVIOR", TapeDiagnosticSeverity.Warning, TapeDiagnosticStage.Compilation, message, span));

        void AddEnvironment(string key, string value, TapeSourceSpan span)
        {
            if (string.IsNullOrEmpty(key) || key.Contains('=') || key.Contains('\0') || value is null || value.Contains('\0'))
            {
                Error("Environment names must be nonempty and contain neither '=' nor NUL; values cannot contain NUL.", span);
                return;
            }
            environment[key] = value;
            explicitEnvironment.Add(key);
        }

        string? FindExecutable(string program, TapeSourceSpan span)
        {
            try
            {
                var executable = TapeShellConfiguration.FindExecutable(program, workingDirectory, environment);
                if (executable is null)
                    Error($"Executable '{program}' was not found using the child's PATH and working directory.", span);
                return executable;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Error($"Cannot resolve executable '{program}': {ex.Message}", span);
                return null;
            }
        }

        string? AddArtifact(string path, TapeArtifactKind kind, TapeSourceSpan span)
        {
            try
            {
                var fullPath = Path.GetFullPath(path, workingDirectory);
                var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var existing = artifacts.FirstOrDefault(a => comparer.Equals(a.Path, fullPath));
                if (existing is not null)
                {
                    if (existing.Kind != kind)
                        Error("Asciicast and golden text cannot share an output path.", span);
                    return existing.Path;
                }
                if (Directory.Exists(fullPath))
                    Error($"Output path is a directory: '{fullPath}'.", span);
                else if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                    Error($"Output parent directory does not exist: '{fullPath}'.", span);
                else if (!overwrite && File.Exists(fullPath))
                    Error($"Output already exists; enable Overwrite explicitly to replace it: '{fullPath}'.", span);
                else
                {
                    artifacts.Add(new(kind, fullPath));
                    return fullPath;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Error($"Invalid output path '{path}': {ex.Message}", span);
            }
            return null;
        }
    }
}
