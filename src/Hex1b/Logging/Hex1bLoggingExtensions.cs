using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hex1b.Logging;

/// <summary>
/// Extension methods for integrating Hex1b logging into the .NET logging infrastructure.
/// </summary>
public static class Hex1bLoggingExtensions
{
    /// <summary>
    /// Adds the Hex1b logging provider, which captures log entries for display in a LoggerPanel widget.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="logStore">
    /// An opaque handle to the log store. Pass this to <c>ctx.LoggerPanel(logStore)</c> to display logs.
    /// </param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddHex1b(this ILoggingBuilder builder, out IHex1bLogStore logStore)
    {
        var store = new Hex1bLogStore();
        builder.Services.AddSingleton<ILoggerProvider>(store);
        logStore = store;
        return builder;
    }
}
