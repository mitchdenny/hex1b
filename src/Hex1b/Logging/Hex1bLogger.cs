using Microsoft.Extensions.Logging;

namespace Hex1b.Logging;

/// <summary>
/// Logger implementation that writes formatted entries to the shared circular buffer.
/// </summary>
internal sealed class Hex1bLogger : ILogger
{
    private readonly string _category;
    private readonly CircularBuffer<Hex1bLogEntry> _buffer;

    public Hex1bLogger(string category, CircularBuffer<Hex1bLogEntry> buffer)
    {
        _category = category;
        _buffer = buffer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);

        var entry = new Hex1bLogEntry(
            DateTime.UtcNow,
            logLevel,
            _category,
            message,
            eventId,
            exception);

        _buffer.Add(entry);
    }
}
