using Hex1b;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// Fluent extensions on <see cref="Hex1bTerminalBuilder"/> for hosting a
/// Spectre.Console application inside a Hex1b terminal.
/// </summary>
public static class SpectreConsoleTerminalBuilderExtensions
{
    /// <summary>
    /// Configures the terminal to host a Spectre.Console workload.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="run">
    /// Asynchronous delegate that receives a fully wired
    /// <see cref="IAnsiConsole"/> bridged to Hex1b. The terminal exits when
    /// the delegate completes (or throws).
    /// </param>
    /// <returns>The builder, for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Output that the Spectre app writes to <see cref="IAnsiConsole"/> flows
    /// straight into the Hex1b workload pipeline — recording, presentation,
    /// muxing, and embedding all work as if a regular Hex1b workload were
    /// running.
    /// </para>
    /// <para>
    /// Keystrokes that arrive on the Hex1b input channel are made available
    /// to the Spectre app via <see cref="IAnsiConsole.Input"/>, so prompts
    /// (e.g. <see cref="AnsiConsole.Ask{T}(string)"/>),
    /// <c>SelectionPrompt</c>, and <c>Live</c> displays react to user input
    /// the same way they would in a real terminal.
    /// </para>
    /// <para>
    /// The bridge does <b>not</b> automatically switch to the alternate
    /// screen buffer or hide the cursor — Spectre.Console manages those
    /// itself for the displays that need them (<see cref="StatusContext"/>,
    /// <see cref="ProgressContext"/>, <see cref="LiveDisplayContext"/>).
    /// Inline output (<see cref="AnsiConsole.MarkupLine(string)"/>,
    /// <see cref="Table"/>, <c>FigletText</c>) renders into the main buffer
    /// just as it would on a real console.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithSpectreConsole(async (console, ct) =>
    ///     {
    ///         console.MarkupLine("[green]Hello from Spectre.Console![/]");
    ///         var name = await console.AskAsync&lt;string&gt;("Your name?", ct);
    ///         console.MarkupLine($"Welcome, [yellow]{name}[/]!");
    ///     })
    ///     .WithAsciinemaRecording("./demo.cast")
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithSpectreConsole(
        this Hex1bTerminalBuilder builder,
        Func<IAnsiConsole, CancellationToken, Task> run)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(run);

        builder.SetWorkloadFactory(presentation =>
        {
            var workloadAdapter = presentation != null
                ? new Hex1bAppWorkloadAdapter(presentation)
                : new Hex1bAppWorkloadAdapter();

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                var console = Hex1bAnsiConsole.Create(workloadAdapter);
                await run(console, ct).ConfigureAwait(false);
                workloadAdapter.Flush();

                // The Spectre delegate has finished, but the terminal pump may
                // still have unread output in the workload's output channel.
                // Block until the channel is fully drained so callers see all
                // of Spectre's final frame in their snapshots / recordings.
                while (workloadAdapter.OutputQueueDepth > 0)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(5, ct).ConfigureAwait(false);
                }

                return 0;
            };

            return new Hex1bTerminalBuildContext(workloadAdapter, runCallback);
        });

        return builder;
    }

    /// <summary>
    /// Configures the terminal to host a Spectre.Console workload using a
    /// synchronous delegate. The delegate runs on a background task so that
    /// long-running Spectre operations do not block the build pipeline.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="run">
    /// Synchronous delegate that receives a fully wired
    /// <see cref="IAnsiConsole"/> bridged to Hex1b.
    /// </param>
    /// <returns>The builder, for fluent chaining.</returns>
    public static Hex1bTerminalBuilder WithSpectreConsole(
        this Hex1bTerminalBuilder builder,
        Action<IAnsiConsole> run)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(run);

        return builder.WithSpectreConsole((console, ct) =>
        {
            run(console);
            return Task.CompletedTask;
        });
    }
}
