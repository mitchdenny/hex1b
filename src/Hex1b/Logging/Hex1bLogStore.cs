using Hex1b.Data;
using Microsoft.Extensions.Logging;

namespace Hex1b.Logging;

/// <summary>
/// Internal logging provider that captures log entries into a circular buffer
/// and exposes them as a virtualized data source for the LoggerPanel widget.
/// </summary>
internal sealed class Hex1bLogStore : ILoggerProvider, IHex1bLogStore, IDisposable
{
    private readonly CircularBuffer<Hex1bLogEntry> _buffer = new(1000);
    private readonly Hex1bLogTableDataSource _dataSource;

    public Hex1bLogStore()
    {
        _dataSource = new Hex1bLogTableDataSource(_buffer);
    }

    internal ITableDataSource<Hex1bLogEntry> DataSource => _dataSource;

    internal CircularBuffer<Hex1bLogEntry> Buffer => _buffer;

    public ILogger CreateLogger(string categoryName)
    {
        return new Hex1bLogger(categoryName, _buffer);
    }

    public void Dispose()
    {
        _dataSource.Dispose();
    }
}
