namespace Hex1b.Terminal;

/// <summary>
/// Extension methods for configuring <see cref="Hex1bTerminalOptions"/>.
/// </summary>
public static class Hex1bTerminalOptionsExtensions
{
    /// <summary>
    /// Adds an Asciinema recorder to the terminal options.
    /// </summary>
    /// <param name="options">The terminal options.</param>
    /// <param name="filePath">Path to the output file (typically with .cast extension).</param>
    /// <param name="recorderOptions">Options for the recorder.</param>
    /// <returns>The recorder instance, which can be used to flush recordings and add markers.</returns>
    /// <example>
    /// <code>
    /// var options = new Hex1bTerminalOptions { ... };
    /// var recorder = options.AddAsciinemaRecorder("demo.cast", new AsciinemaRecorderOptions { Title = "Demo" });
    /// var terminal = new Hex1bTerminal(options);
    /// // ... run application ...
    /// // Recording is automatically saved on dispose
    /// </code>
    /// </example>
    public static AsciinemaRecorder AddAsciinemaRecorder(
        this Hex1bTerminalOptions options,
        string filePath,
        AsciinemaRecorderOptions? recorderOptions = null)
    {
        var recorder = new AsciinemaRecorder(filePath, recorderOptions);
        options.WorkloadFilters.Add(recorder);
        return recorder;
    }

    /// <summary>
    /// Adds a Hex1bApp render optimization filter that only transmits cells that have changed.
    /// </summary>
    /// <param name="options">The terminal options.</param>
    /// <returns>The filter instance.</returns>
    /// <remarks>
    /// <para>
    /// This filter is specifically designed for Hex1bApp workloads. It understands
    /// the frame boundary tokens (HEX1BAPP:FRAME:BEGIN/END) emitted by Hex1bApp and
    /// uses them to batch changes and perform delta encoding.
    /// </para>
    /// <para>
    /// The filter maintains dual buffers for delta encoding:
    /// <list type="bullet">
    ///   <item><b>Pending buffer</b>: Accumulates changes during the current frame</item>
    ///   <item><b>Committed buffer</b>: Represents what was last sent to the presentation layer</item>
    /// </list>
    /// </para>
    /// <para>
    /// Benefits:
    /// <list type="bullet">
    ///   <item>Reduces bandwidth for remote terminal connections</item>
    ///   <item>Improves rendering performance by avoiding redundant updates</item>
    ///   <item>Enables efficient partial screen updates</item>
    ///   <item>Eliminates flicker from intermediate render states</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new Hex1bTerminalOptions { ... };
    /// options.AddHex1bAppRenderOptimization();
    /// var terminal = new Hex1bTerminal(options);
    /// </code>
    /// </example>
    public static Hex1bAppRenderOptimizationFilter AddHex1bAppRenderOptimization(this Hex1bTerminalOptions options)
    {
        var filter = new Hex1bAppRenderOptimizationFilter();
        options.PresentationFilters.Add(filter);
        return filter;
    }
}
