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
}
