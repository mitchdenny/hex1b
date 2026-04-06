using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Hex1b.PtyHost;

internal sealed class PtyHostFileLoggerProvider : ILoggerProvider
{
    private readonly object _syncLock = new();
    private readonly string _logFilePath;
    private StreamWriter? _writer;
    private bool _disabled;

    public PtyHostFileLoggerProvider(string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException("The PTY host log file path must not be empty.", nameof(logFilePath));
        }
        
        _logFilePath = logFilePath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new PtyHostFileLogger(categoryName, this);
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            _disabled = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private bool TryWriteLogRecord(string categoryName, LogLevel logLevel, string message, Exception? exception)
    {
        lock (_syncLock)
        {
            if (_disabled)
            {
                return false;
            }

            if (_writer is null && !TryOpenWriter())
            {
                return false;
            }

            _writer!.Write(DateTime.UtcNow.ToString("O"));
            _writer.Write(' ');
            _writer.Write(logLevel.ToString().ToUpperInvariant());
            _writer.Write(" [");
            _writer.Write(categoryName);
            _writer.Write("] ");
            _writer.WriteLine(message);

            if (exception != null)
            {
                _writer.WriteLine(exception);
            }

            return true;
        }
    }

    private bool TryOpenWriter()
    {
        try
        {
            var fullPath = Path.GetFullPath(_logFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(new FileStream(
                fullPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete))
            {
                AutoFlush = true
            };

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            _disabled = true;
            Trace.WriteLine($"PTY host file logging disabled because '{_logFilePath}' could not be opened: {ex.Message}");
            return false;
        }
    }

    private sealed class PtyHostFileLogger(
        string categoryName,
        PtyHostFileLoggerProvider owner) : ILogger
    {
        private readonly string _categoryName = categoryName;
        private readonly PtyHostFileLoggerProvider _owner = owner;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            _owner.TryWriteLogRecord(_categoryName, logLevel, message, exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
