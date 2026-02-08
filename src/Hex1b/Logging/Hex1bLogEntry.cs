using Microsoft.Extensions.Logging;

namespace Hex1b.Logging;

/// <summary>
/// Represents a single log entry captured by the Hex1b logging provider.
/// </summary>
internal sealed record Hex1bLogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    EventId EventId,
    Exception? Exception);
