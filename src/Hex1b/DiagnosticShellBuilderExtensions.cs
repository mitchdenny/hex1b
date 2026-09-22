using System.Text;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// Extension methods for adding diagnostic shell to <see cref="Hex1bTerminalBuilder"/>.
/// </summary>
public static class DiagnosticShellBuilderExtensions
{
    /// <summary>
    /// Configures the terminal to run the diagnostic shell workload.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The diagnostic shell provides a simulated shell environment for testing
    /// terminal control codes without requiring PTY infrastructure.
    /// </para>
    /// <para>
    /// Available commands include: help, echo, colors, cursor, scroll, capture, dump, exit.
    /// Use up/down arrows to navigate command history.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithDiagnosticShell()
    ///     .Build();
    /// 
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithDiagnosticShell(this Hex1bTerminalBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.SetWorkloadFactory(presentation =>
        {
            var width = presentation?.Width ?? 80;
            var height = presentation?.Height ?? 24;

            var adapter = new DiagnosticShellWorkloadAdapter(width, height);

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                adapter.Start();
                
                // Wait for shell to exit or cancellation
                var tcs = new TaskCompletionSource<int>();
                
                adapter.Disconnected += () => tcs.TrySetResult(0);
                
                using var registration = ct.Register(() => tcs.TrySetCanceled());
                
                try
                {
                    return await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    return 130; // Standard exit code for Ctrl+C
                }
            };

            return new Hex1bTerminalBuildContext(adapter, runCallback);
        });

        return builder;
    }
}
