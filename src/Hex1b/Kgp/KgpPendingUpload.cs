namespace Hex1b;

internal sealed class KgpPendingUpload : IDisposable
{
    private byte[]? _buffer = [];
    private int _length;

    internal KgpPendingUpload(
        KgpParsedCommand? initialCommand,
        KgpParsedCommand.TransmissionData transmission,
        KgpParsedCommand.QuietMode quiet,
        long maximumBytes)
    {
        InitialCommand = initialCommand;
        Transmission = transmission;
        EffectiveQuiet = quiet;
        MaximumBytes = Math.Max(0, maximumBytes);
    }

    internal KgpParsedCommand? InitialCommand { get; }

    internal KgpParsedCommand.TransmissionData Transmission { get; }

    internal KgpParsedCommand.QuietMode EffectiveQuiet { get; private set; }

    internal long MaximumBytes { get; }

    internal long Length => _length;

    internal void ApplyQuiet(KgpParsedCommand.QuietMode quiet)
    {
        if (quiet != KgpParsedCommand.QuietMode.Normal)
            EffectiveQuiet = quiet;
    }

    internal bool TryAppend(ReadOnlySpan<byte> data, out long attemptedLength)
    {
        var buffer = _buffer
            ?? throw new ObjectDisposedException(nameof(KgpPendingUpload));

        attemptedLength = (long)_length + data.Length;
        if (attemptedLength > MaximumBytes)
            return false;

        if (attemptedLength > buffer.Length)
        {
            var doubledLength = Math.Max((long)buffer.Length * 2, attemptedLength);
            var newLength = checked((int)Math.Min(MaximumBytes, doubledLength));
            var expanded = new byte[newLength];
            buffer.AsSpan(0, _length).CopyTo(expanded);
            _buffer = buffer = expanded;
        }

        data.CopyTo(buffer.AsSpan(_length));
        _length += data.Length;
        return true;
    }

    internal byte[] Complete()
    {
        var buffer = _buffer
            ?? throw new ObjectDisposedException(nameof(KgpPendingUpload));
        byte[] data;
        if (_length == buffer.Length)
        {
            data = buffer;
        }
        else
        {
            data = new byte[_length];
            buffer.AsSpan(0, _length).CopyTo(data);
        }

        _buffer = null;
        _length = 0;
        return data;
    }

    public void Dispose()
    {
        _buffer = null;
        _length = 0;
    }
}
