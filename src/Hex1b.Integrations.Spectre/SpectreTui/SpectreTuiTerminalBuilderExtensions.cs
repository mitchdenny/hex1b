using Hex1b;
using Spectre.Tui;
using Spectre.Tui.App;

namespace Hex1b.Integrations.Spectre.SpectreTui;

/// <summary>
/// Fluent extensions on <see cref="Hex1bTerminalBuilder"/> for hosting a
/// Spectre.Tui application inside a Hex1b terminal.
/// </summary>
public static class SpectreTuiTerminalBuilderExtensions
{
    /// <summary>
    /// Configures the terminal to host a Spectre.Tui <see cref="Application"/>.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="initialScreen">The Spectre.Tui screen to push onto the application stack at startup.</param>
    /// <param name="mode">
    /// Optional terminal mode. Defaults to <see cref="FullscreenMode"/> —
    /// Spectre.Tui's standard alt-screen behaviour. Pass an <see cref="InlineMode"/>
    /// instance to keep the rendered surface on the main buffer (handy when
    /// embedding Spectre.Tui output inside scripted automation or recordings
    /// that need to inspect the main scroll-back).
    /// </param>
    /// <param name="targetFps">
    /// Optional target render frame rate. Defaults to Spectre.Tui's own
    /// default (60 fps) when omitted.
    /// </param>
    /// <returns>The builder, for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Spectre.Tui's render loop runs to completion when the user calls
    /// <c>ApplicationContext.Quit()</c>; the terminal then exits with code 0.
    /// </para>
    /// <para>
    /// Output rendered by Spectre.Tui flows into the Hex1b workload pipeline
    /// frame by frame, so recording (<c>WithAsciinemaRecording</c>),
    /// presentation, and embedding all work without further configuration.
    /// </para>
    /// </remarks>
    public static Hex1bTerminalBuilder WithSpectreTuiApp(
        this Hex1bTerminalBuilder builder,
        Screen initialScreen,
        ITerminalMode? mode = null,
        int? targetFps = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(initialScreen);

        builder.SetWorkloadFactory(presentation =>
        {
            var workloadAdapter = presentation != null
                ? new Hex1bAppWorkloadAdapter(presentation)
                : new Hex1bAppWorkloadAdapter();

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                var terminal = new Hex1bSpectreTuiTerminal(workloadAdapter, mode);
                var input = new Hex1bSpectreTuiInputReader(workloadAdapter);

                var settings = targetFps is { } fps
                    ? new ApplicationSettings
                    {
                        Terminal = terminal,
                        InputReader = input,
                        TargetFps = fps,
                    }
                    : new ApplicationSettings
                    {
                        Terminal = terminal,
                        InputReader = input,
                    };

                var app = Application.Create(settings);
                await app.RunAsync(initialScreen).WaitAsync(ct).ConfigureAwait(false);

                workloadAdapter.Flush();

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
    /// Configures the terminal to host a low-level Spectre.Tui workload that
    /// drives the <see cref="ITerminal"/> directly, without using
    /// <see cref="Application"/>.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="run">
    /// Asynchronous delegate that receives the bridged
    /// <see cref="ITerminal"/>. The terminal exits when the delegate returns
    /// or the supplied <see cref="CancellationToken"/> fires.
    /// </param>
    /// <param name="mode">
    /// Optional terminal mode. Defaults to <see cref="FullscreenMode"/>.
    /// </param>
    /// <returns>The builder, for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when consuming Spectre.Tui without the
    /// <c>Spectre.Tui.App</c> package (custom render loop, embedded usage,
    /// or unit tests). For a screen-based app, prefer
    /// <see cref="WithSpectreTuiApp"/>.
    /// </remarks>
    public static Hex1bTerminalBuilder WithSpectreTuiTerminal(
        this Hex1bTerminalBuilder builder,
        Func<ITerminal, CancellationToken, Task> run,
        ITerminalMode? mode = null)
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
                using var terminal = new Hex1bSpectreTuiTerminal(workloadAdapter, mode);
                await run(terminal, ct).ConfigureAwait(false);

                workloadAdapter.Flush();
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
}
