using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Hex1b;

namespace WebTerminalDemo;

internal sealed class DemoWorkload : IHex1bTerminalWorkloadAdapter
{
    private const int ImageWidth = 320, ImageHeight = 180;
    private readonly string _scene;
    private readonly Channel<byte[]> _output = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(2)
    {
        FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true
    });
    private readonly CancellationTokenSource _stop = new();
    private readonly object _stateLock = new();
    private readonly Decoder _inputDecoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _input = new();
    private readonly string[] _sixels;
    private readonly string _imageUpload;
    private readonly string _animationUpload;
    private int _columns, _rows, _layoutVersion, _inputVersion, _inputState, _disposed, _started;
    private int _rate = 60, _batch = 100;
    private bool _paused;
    private long _bytesRead;

    public DemoWorkload(string scene, int columns, int rows)
    {
        if (scene is not ("mixed" or "text" or "sixel" or "kgp" or "animation" or "activity"))
            throw new ArgumentException("Unknown demo scene.", nameof(scene));
        _scene = scene;
        if (scene == "activity") _rate = 1;
        _columns = Math.Clamp(columns, 1, 300);
        _rows = Math.Clamp(rows, 2, 100);
        _sixels = scene is "mixed" or "sixel"
            ? Enumerable.Range(0, 4).Select(CreateSixel).ToArray() : [];
        _imageUpload = scene is "mixed" or "kgp" or "animation" ? Upload('t', 0) : "";
        _animationUpload = scene == "animation"
            ? string.Concat(Enumerable.Range(1, 3).Select(frame => Upload('f', frame))) : "";
    }

    public long BytesRead => Interlocked.Read(ref _bytesRead);
    public int Rate { get => Volatile.Read(ref _rate); set => Volatile.Write(ref _rate, Math.Clamp(value, 1, 120)); }
    public int Batch { get => Volatile.Read(ref _batch); set => Volatile.Write(ref _batch, Math.Clamp(value, 1, 1000)); }
    // Pausing the producer deliberately does not pause Hex1bTerminal's KGP animation timer.
    public bool Paused { get => Volatile.Read(ref _paused); set => Volatile.Write(ref _paused, value); }
    public event Action? Disconnected;

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        try
        {
            var bytes = await _output.Reader.ReadAsync(ct);
            Interlocked.Add(ref _bytesRead, bytes.Length);
            return bytes;
        }
        catch (ChannelClosedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_stateLock)
        {
            // Retain decoder and escape state across reads, including fragmented protocol replies.
            var chars = new char[Encoding.UTF8.GetMaxCharCount(data.Length)];
            var count = _inputDecoder.GetChars(data.Span, chars, flush: false);
            for (var i = 0; i < count; i++)
            {
                var ch = chars[i];
                if (_inputState != 0)
                {
                    _inputState = _inputState switch
                    {
                        1 => ch is '[' or 'O' ? 2 : ch is ']' or '_' or 'P' or '^' or 'X' ? 3 : 0,
                        2 => ch is >= '@' and <= '~' ? 0 : 2,
                        3 => ch is '\a' or '\u009c' ? 0 : ch == '\x1b' ? 4 : 3,
                        _ => ch is '\\' or '\a' or '\u009c' ? 0 : ch == '\x1b' ? 4 : 3
                    };
                    continue;
                }
                if (ch == '\x1b') { _inputState = 1; continue; }
                if (ch == '\u009b') { _inputState = 2; continue; }
                if (ch is '\u0090' or '\u009d' or '\u009f') { _inputState = 3; continue; }
                if (ch is '\b' or '\x7f')
                {
                    var starts = StringInfo.ParseCombiningCharacters(_input.ToString());
                    if (starts.Length > 0) _input.Length = starts[^1];
                }
                else if (ch is '\r' or '\n') _input.Append(" [Enter] ");
                else if (ch == '\t') _input.Append(' ');
                else if (ch == '\x15') _input.Clear();
                else if (!char.IsControl(ch)) _input.Append(ch);
                else continue;
                if (_input.Length > 160)
                {
                    var starts = StringInfo.ParseCombiningCharacters(_input.ToString());
                    _input.Remove(0, starts.FirstOrDefault(offset => offset >= _input.Length - 160, _input.Length));
                }
                _inputVersion++;
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_stateLock)
        {
            _columns = Math.Clamp(width, 1, 300);
            _rows = Math.Clamp(height, 2, 100);
            _layoutVersion++;
        }
        return ValueTask.CompletedTask;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The demo workload can only run once.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        ct = linked.Token;
        var layoutVersion = -1;
        var inputVersion = -1;
        long tick = 0, line = 0;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (!Paused)
                {
                    int columns, rows, layout, input;
                    string echo;
                    lock (_stateLock)
                    {
                        (columns, rows, layout, input, echo) =
                            (_columns, _rows, _layoutVersion, _inputVersion, _input.ToString());
                    }
                    var output = new StringBuilder();
                    var rebuild = layout != layoutVersion;
                    if (rebuild)
                    {
                        output.Append("\x1b[?25l\x1b[?7l\x1b[?80h\x1b[?8452h\x1b[r")
                            .Append(Kgp("a=d,d=A,q=2"));
                        Dashboard(output, columns, rows, tick);
                        output.Append(_imageUpload).Append(_animationUpload);
                        if (_scene is "mixed" or "kgp" or "animation")
                            PlaceImage(output, columns, rows, tick);
                        if (_scene == "animation")
                            output.Append(Kgp("a=a,i=1,r=1,z=160,c=1,s=3,v=1,q=2"));
                        layoutVersion = layout;
                    }
                    else if (_scene == "sixel")
                        Dashboard(output, columns, rows, tick);
                    else if (_scene == "kgp")
                        PlaceImage(output, columns, rows, tick);
                    if (_scene == "activity")
                        Activity(output, tick);
                    if (_scene == "text")
                    {
                        output.Append($"\x1b[3;{Math.Max(3, rows - 2)}r")
                            .Append(Cup(Math.Max(3, rows - 2), 1));
                        for (var i = 0; i < Batch; i++)
                        {
                            var text = $"{line++:D10} | Hex1b authoritative terminal | " +
                                new string((char)('A' + i % 26), Math.Min(columns, 160));
                            output.Append("\r\n\x1b[2K").Append($"\x1b[38;5;{16 + line % 216}m")
                                .Append(text.AsSpan(0, Math.Min(text.Length, Math.Max(1, columns - 1))))
                                .Append("\x1b[0m");
                        }
                        output.Append("\x1b[r");
                    }
                    // The animation scene must become silent, not finish: its frames are server-timed.
                    if (rebuild || _scene != "animation" || input != inputVersion)
                    {
                        Status(output, rows - 1, $"{_scene} | tick {tick} | {Rate} Hz | batch {Batch}");
                        Status(output, rows, $"Input: {echo}");
                        inputVersion = input;
                    }
                    if (output.Length > 0)
                    {
                        var bytes = Encoding.UTF8.GetBytes(output.ToString());
                        // Bound channel items too; splitting anywhere is valid for the streaming parser.
                        for (var offset = 0; offset < bytes.Length; offset += 16384)
                            await _output.Writer.WriteAsync(bytes.AsSpan(offset, Math.Min(16384, bytes.Length - offset)).ToArray(), ct);
                    }
                    tick++;
                }
                await Task.Delay(TimeSpan.FromSeconds(1.0 / Rate), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ChannelClosedException) when (ct.IsCancellationRequested) { }
        finally { _output.Writer.TryComplete(); }
    }

    private void Dashboard(StringBuilder output, int columns, int rows, long tick)
    {
        output.Append("\x1b[0m\x1b[2J").Append(Cup(1, 1))
            .Append("\x1b[1;38;2;100;210;255mHex1b WebTerminalDemo\x1b[0m");
        Status(output, 2, $"{_scene}: raw ANSI -> real Hex1bTerminal -> authoritative browser state");
        if (_scene is "text" or "activity") return;
        output.Append(Cup(3, 1)).Append("Wide: \u4e16\u754c \u65e5\u672c\u8a9e | emoji: \U0001f680 \U0001f30d | combining: e\u0301 a\u0308 n\u0303")
            .Append(Cup(4, 1)).Append("\x1b[1mBold\x1b[0m  \x1b[3mItalic\x1b[0m  \x1b[4mUnderline\x1b[0m  \x1b[9mStrike\x1b[0m")
            .Append(Cup(5, 1)).Append("\x1b[2mDim\x1b[0m  \x1b[7mReverse\x1b[0m  \x1b[38;2;255;170;50mTruecolor\x1b[0m  \x1b[48;5;54m Indexed BG \x1b[0m");
        if (_scene is "mixed" or "sixel")
        {
            Status(output, 7, "Sixel: 240 x 120 / repeat-encoded checkerboard");
            output.Append(Cup(8, 2)).Append(_sixels[(int)(tick % _sixels.Length)]);
        }
        if (_scene is "kgp" or "animation")
        {
            Status(output, 7, _scene == "animation"
                ? "KGP: four frames, infinite server-only playback (producer goes silent)"
                : "KGP: one texture, two moving placements / behind and above text");
            for (var row = 9; row < Math.Min(rows - 2, 18); row += 2)
                Status(output, row, "   Text under transparent pixels / text over the negative-z image");
        }
        if (_scene == "mixed")
            output.Append(Cup(7, Math.Max(2, columns / 2))).Append("KGP: RGBA + alpha holes");
    }

    private static void Activity(StringBuilder output, long tick)
    {
        var (sequence, description) = (tick % 12) switch
        {
            0 => ("\x1b]9;4;0\a\x1b]133;A\a", "Prompt starts; progress hidden"),
            1 => ("\x1b]133;B\a", "Command-line input; not executing"),
            2 => ("\x1b]133;C\a\x1b]9;4;3\a", "Command executes; indeterminate progress"),
            3 => ("\x1b]9;4;1;25\a", "Normal progress: 25%"),
            4 => ("\x1b]9;4;1;75\a", "Normal progress: 75%"),
            5 => ("\x1b]9;4;4;75\a", "Warning progress: 75%"),
            6 => ("\x1b]9;4;2;75\a", "Error progress: 75%"),
            7 => ("\x1b]133;D;1\a\x1b]9;4;0\a", "Command finishes with exit code 1; progress cleared"),
            8 => ("\x1b]133;A\a", "Next prompt; previous result remains available"),
            9 => ("\x1b]133;B\a", "Next command-line input"),
            10 => ("\x1b]133;C\a\x1b]9;4;1;100\a", "Next command executes; progress 100%"),
            _ => ("\x1b]133;D;0\a\x1b]9;4;0\a", "Command finishes successfully; progress cleared")
        };
        output.Append(sequence);
        Status(output, 3, "Simulated shell markers and application progress (no commands are run)");
        Status(output, 5, description);
        Status(output, 7, "Watch the host-owned activity strip above the terminal.");
        Status(output, 8, "Pause to attach a late Direct HWT1 or HMP1 relay view.");
    }

    private void PlaceImage(StringBuilder output, int columns, int rows, long tick)
    {
        var width = Math.Max(1, Math.Min(32, columns / 2 - 2));
        var height = Math.Max(1, Math.Min(10, rows - 11));
        var left = _scene == "mixed" ? Math.Max(2, columns / 2) : 2;
        if (_scene == "kgp")
            left += (int)(tick % Math.Max(1, columns - width - 2));
        output.Append(Cup(Math.Min(8, rows - 1), left))
            .Append(Kgp($"a=p,i=1,p=1,c={width},r={height},C=1,z=-1,q=2"));
        if (_scene == "kgp")
            output.Append(Cup(Math.Min(12, rows - 1), Math.Max(1, columns - width - left)))
                .Append(Kgp($"a=p,i=1,p=2,c={width},r={height},C=1,z=1,q=2"));
    }

    private static string Upload(char action, int frame)
    {
        var pixels = new byte[ImageWidth * ImageHeight * 4];
        for (var y = 0; y < ImageHeight; y++)
        for (var x = 0; x < ImageWidth; x++)
        {
            var offset = (y * ImageWidth + x) * 4;
            pixels[offset] = (byte)((x * 255 / ImageWidth + frame * 60) % 256);
            pixels[offset + 1] = (byte)((y * 255 / ImageHeight + frame * 90) % 256);
            pixels[offset + 2] = (byte)((x + y + frame * 75) % 256);
            pixels[offset + 3] = (byte)(((x / 32 + y / 30 + frame) % 3) switch { 0 => 0, 1 => 140, _ => 255 });
        }
        var data = Convert.ToBase64String(pixels);
        var output = new StringBuilder();
        for (var offset = 0; offset < data.Length; offset += 4096)
        {
            var length = Math.Min(4096, data.Length - offset);
            var controls = offset == 0
                ? $"a={action},f=32,s={ImageWidth},v={ImageHeight},i=1,q=2" + (action == 'f' ? ",X=1,z=160" : "")
                : action == 'f' ? "a=f" : "";
            output.Append("\x1b_G").Append(controls);
            if (controls.Length > 0) output.Append(',');
            output.Append($"m={(offset + length < data.Length ? 1 : 0)};")
                .Append(data.AsSpan(offset, length)).Append("\x1b\\");
        }
        return output.ToString();
    }

    private static string CreateSixel(int phase)
    {
        var output = new StringBuilder("\x1bP7;1q\"1;1;240;120");
        output.Append("#0;2;100;25;35#1;2;10;85;100#2;2;100;85;10#3;2;65;25;100");
        for (var band = 0; band < 20; band++)
        {
            for (var tile = 0; tile < 8; tile++)
                output.Append('#').Append((tile + band / 5 + phase) % 4).Append("!30~");
            if (band < 19) output.Append('-');
        }
        return output.Append("\x1b\\").ToString();
    }

    private static string Cup(int row, int column) => $"\x1b[{row};{column}H";
    private static string Kgp(string controls) => $"\x1b_G{controls}\x1b\\";
    private static void Status(StringBuilder output, int row, string text) =>
        output.Append(Cup(Math.Max(1, row), 1)).Append("\x1b[0m\x1b[2K").Append(text).Append("\x1b[0m");

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stop.Cancel();
            _output.Writer.TryComplete();
            _stop.Dispose();
            Disconnected?.Invoke();
        }
        return ValueTask.CompletedTask;
    }
}
