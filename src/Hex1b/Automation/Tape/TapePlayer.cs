using System.Runtime.ExceptionServices;

namespace Hex1b.Automation;

/// <summary>Prepares and plays Tape documents against borrowed or player-owned terminals.</summary>
/// <remarks>Passing a terminal borrows it; otherwise the options' factory creates an owned terminal. Disposing a result releases only its final snapshot.</remarks>
public sealed class TapePlayer
{
    /// <summary>Resolves and validates a tape without sending input, resizing, or creating output files.</summary>
    /// <param name="tape">The parsed document to prepare.</param>
    /// <param name="terminal">The caller-owned target terminal.</param>
    /// <param name="options">Playback and capture configuration.</param>
    /// <param name="cancellationToken">Cancellation for preparation.</param>
    /// <returns>All preparation diagnostics and whether execution is supported.</returns>
    /// <remarks>The supplied terminal takes precedence; <see cref="TapePlaybackOptions.TerminalFactory"/> is not invoked.</remarks>
    public async Task<TapeValidationResult> ValidateAsync(TapeDocument tape, Hex1bTerminal terminal,
        TapePlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        var prepared = await TapePreparation.PrepareAsync(tape, options ?? new(), false,
            terminal.AutomationTimeProvider, cancellationToken);
        return ValidateTarget(prepared, terminal, tape.SourceName);
    }

    /// <summary>Validates, executes, and captures a tape without taking ownership of its terminal.</summary>
    /// <param name="tape">The parsed document to execute.</param>
    /// <param name="terminal">The caller-owned target terminal.</param>
    /// <param name="options">Playback and capture configuration.</param>
    /// <param name="cancellationToken">Cancellation for preparation and execution.</param>
    /// <returns>The owned final snapshot and finalized artifact information.</returns>
    /// <remarks>The supplied terminal takes precedence; <see cref="TapePlaybackOptions.TerminalFactory"/> is not invoked.</remarks>
    /// <exception cref="TapeValidationException">Preparation found errors before execution began.</exception>
    /// <exception cref="TapePlaybackException">A command or capture failed after execution began.</exception>
    /// <exception cref="InvalidOperationException">Another tape is already playing on this terminal.</exception>
    public Task<TapeExecutionResult> PlayAsync(TapeDocument tape, Hex1bTerminal terminal,
        TapePlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        return PlayCoreAsync(tape, options ?? new(), _ => terminal, ownsTerminal: false, cancellationToken);
    }

    /// <summary>Validates playback configuration without building a terminal, launching a workload, or creating captures.</summary>
    /// <param name="tape">The parsed document to prepare.</param>
    /// <param name="options">Playback, capture, and default-shell configuration.</param>
    /// <param name="cancellationToken">Cancellation for preparation.</param>
    /// <returns>Preparation diagnostics. With a custom factory, workload-dependent checks are deferred to playback and a warning reports that limitation.</returns>
    /// <remarks>The terminal factory is never invoked by validation.</remarks>
    public async Task<TapeValidationResult> ValidateAsync(TapeDocument tape,
        TapePlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        var prepared = await TapePreparation.PrepareAsync(tape, options,
            options.TerminalFactory is null ? true : null, TimeProvider.System, cancellationToken);
        return prepared.Validation;
    }

    /// <summary>Builds an owned terminal, plays a tape, captures the final state, and cleans up the workload and terminal.</summary>
    /// <param name="tape">The parsed document to execute.</param>
    /// <param name="options">Playback, capture, and default-shell configuration.</param>
    /// <param name="cancellationToken">Cancellation for preparation and playback.</param>
    /// <returns>The final snapshot captured before cleanup, finalized artifacts, and any naturally observed workload exit code.</returns>
    /// <remarks>
    /// The builder defaults to an 80-by-24 headless terminal and a VHS shell profile.
    /// <see cref="TapePlaybackOptions.TerminalFactory"/> may customize the terminal or select a workload with methods such as
    /// <see cref="Hex1bTerminalBuilder.WithHex1bApp(Func{RootContext, Widgets.Hex1bWidget})"/>.
    /// Shell launch options and the Shell, Env, and Require commands apply only when no custom workload is selected.
    /// An explicit <see cref="TapePlaybackOptions.TerminalSize"/> overrides builder dimensions.
    /// The factory must return the supplied builder's Build result without starting it. It runs once per playback, never during validation.
    /// </remarks>
    /// <example>
    /// <code>
    /// using Hex1b;
    /// using Hex1b.Automation;
    ///
    /// var tape = new TapeParser().Parse("Wait+Screen /Ready/ Sleep 100ms");
    /// using var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
    /// {
    ///     TerminalFactory = builder => builder.WithDimensions(100, 30)
    ///         .WithHex1bApp(context => context.Text("Ready")).Build()
    /// });
    /// Console.WriteLine(result.FinalSnapshot.GetScreenText());
    /// </code>
    /// </example>
    /// <exception cref="TapeValidationException">Preparation or target validation failed before playback.</exception>
    /// <exception cref="TapePlaybackException">Workload execution, capture, or owned-resource cleanup failed.</exception>
    public Task<TapeExecutionResult> PlayAsync(TapeDocument tape,
        TapePlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        return PlayCoreAsync(tape, options, options.TerminalFactory ?? (builder => builder.Build()),
            ownsTerminal: true, cancellationToken);
    }

    private static async Task<TapeExecutionResult> PlayCoreAsync(TapeDocument tape, TapePlaybackOptions options,
        Func<Hex1bTerminalBuilder, Hex1bTerminal> factory, bool ownsTerminal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tape);
        cancellationToken.ThrowIfCancellationRequested();
        var builder = Hex1bTerminal.CreateBuilder().WithHeadless();
        Hex1bTerminal? terminal = ownsTerminal ? null : factory(builder);
        using var borrowedLease = terminal is not null ? TapePlaybackLease.Acquire(terminal) : null;
        var source = await TapePreparation.ResolveAsync(tape, options, cancellationToken);
        TapePreparedRun? prepared = null;
        var buildAttempted = false;
        if (ownsTerminal)
        {
            builder.BuildCallback = configured =>
            {
                if (buildAttempted)
                    throw new InvalidOperationException("A tape terminal factory must build the supplied builder exactly once.");
                buildAttempted = true;
                var ownsShell = configured.GetConfiguredWorkloadAdapter() is null &&
                    configured.GetConfiguredWorkloadFactory() is null;
                prepared = TapePreparation.Prepare(source, options, ownsShell, configured.GetConfiguredTimeProvider(), cancellationToken);
                if (!prepared.Validation.CanExecute)
                    throw new TapeValidationException(prepared.Diagnostics);
                ConfigureOwnedBuilder(configured, prepared, ownsShell);
                terminal = configured.BuildCore(deferStart: true);
                return terminal;
            };
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<int>? workloadTask = null;
        TapeExecutionResult? result = null;
        Exception? failure = null;
        TapePlaybackException? playbackFailure = null;
        OperationCanceledException? cancellationFailure = null;
        var executionStarted = false;
        try
        {
            if (ownsTerminal)
            {
                var returned = factory(builder);
                if (terminal is null || !ReferenceEquals(returned, terminal))
                    throw new InvalidOperationException("A tape terminal factory must return the terminal built by its supplied builder.");
            }
            var target = terminal ?? throw new InvalidOperationException("The terminal factory returned no terminal.");
            prepared ??= TapePreparation.Prepare(source, options, false, target.AutomationTimeProvider, cancellationToken);
            var validation = ValidateTarget(prepared, target, tape.SourceName);
            if (!validation.CanExecute)
                throw new TapeValidationException(validation.Diagnostics);
            using var ownedLease = ownsTerminal ? TapePlaybackLease.Acquire(target) : null;
            executionStarted = true;
            result = await TapeExecution.ExecuteAsync(prepared, target, ownsTerminal ? () =>
            {
                workloadTask = target.RunWorkloadAsync(lifetime.Token);
                return Task.CompletedTask;
            } : null, () => workloadTask, cancellationToken);
            if (workloadTask is { IsCompleted: true })
                result.ProcessExitCode = await workloadTask;
        }
        catch (Exception ex)
        {
            failure = ex;
            playbackFailure = ex as TapePlaybackException;
            cancellationFailure = ex as OperationCanceledException;
        }
        finally
        {
            builder.BuildCallback = null;
        }
        try
        {
            await lifetime.CancelAsync();
            if (workloadTask is not null)
            {
                try
                {
                    await workloadTask;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                {
                    // Cleanup cancellation is not a naturally observed workload exit.
                }
            }
        }
        catch (Exception ex)
        {
            AddCleanupFailure(ex);
        }
        try
        {
            if (ownsTerminal && terminal is not null)
                await terminal.DisposeAsync();
        }
        catch (Exception ex)
        {
            AddCleanupFailure(ex);
        }
        if (failure is not null)
        {
            var text = result?.FinalSnapshot.GetScreenText() ?? playbackFailure?.TerminalText ?? "";
            var count = result?.CompletedCommandCount ?? playbackFailure?.CompletedCommandCount ?? 0;
            var artifacts = result?.Artifacts ?? playbackFailure?.PartialArtifacts ?? [];
            result?.Dispose();
            if (!executionStarted || failure is OperationCanceledException or TapePlaybackException or TapeValidationException)
                ExceptionDispatchInfo.Capture(failure).Throw();
            if (cancellationFailure is not null)
                throw new OperationCanceledException("Tape playback was cancelled and owned-resource cleanup also failed.",
                    failure, cancellationFailure.CancellationToken);
            throw new TapePlaybackException(playbackFailure?.Command, count, text, artifacts, failure);
        }
        return result ?? throw new InvalidOperationException("Tape execution returned no result.");

        void AddCleanupFailure(Exception error)
        {
            if (failure is null)
                failure = error;
            else if (!ReferenceEquals(failure, error) && !ReferenceEquals(failure.InnerException, error))
                failure = new AggregateException(failure, error);
        }
    }

    private static void ConfigureOwnedBuilder(Hex1bTerminalBuilder builder, TapePreparedRun prepared, bool ownsShell)
    {
        if (prepared.TerminalSize is { } dimensions)
            builder.WithDimensions(dimensions.Width, dimensions.Height);
        if (ownsShell)
        {
            var shell = prepared.Shell ?? throw new InvalidOperationException("A validated shell playback has no launch profile.");
            builder.WithPtyProcess(process =>
            {
                process.FileName = shell.FileName;
                process.Arguments = shell.Arguments;
                process.WorkingDirectory = prepared.WorkingDirectory;
                process.Environment = prepared.Environment?.ToDictionary(pair => pair.Key, pair => pair.Value);
                process.InheritEnvironment = false;
            });
        }
    }

    private static TapeValidationResult ValidateTarget(TapePreparedRun prepared, Hex1bTerminal terminal, string? sourceName)
    {
        if (prepared.TerminalSize is not null && !terminal.SupportsAutomationResize)
            return new(prepared.Diagnostics.Append(new TapeDiagnostic("TAPE_RESIZE_UNSUPPORTED",
                TapeDiagnosticSeverity.Error, TapeDiagnosticStage.Preflight,
                "The workload cannot confirm an automated resize; preserve its current dimensions.",
                new(sourceName, 0, 0, 1, 1))));
        return prepared.Validation;
    }
}
