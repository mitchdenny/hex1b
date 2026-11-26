using System.Buffers;
using System.Globalization;
using System.Text;
using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Presents a remote HMP1 producer's Kitty keyboard state on a physical host
/// without letting the remote stream edit the host's own keyboard stacks.
/// </summary>
/// <remarks>
/// <para>
/// The remote stream's keyboard controls are applied to per-screen models and never
/// forwarded. Instead the viewer owns a single entry on the physical host: it pushes
/// one entry before the first output, sets that entry to the remote screen's active
/// flags with <c>CSI = flags u</c>, moves the entry across screen switches, and pops
/// it when the connection ends. Queries are dropped because the producer replies.
/// </para>
/// <para>
/// A remote full reset (<c>ESC c</c>) would empty both physical keyboard stacks,
/// discarding entries the host's own program pushed before attaching. On a host that
/// supports the protocol the reset is therefore replaced by the owned pop, a return
/// to the main screen, a soft reset and explicit clears, and a fresh owned push.
/// That replacement does not reset tab stops, titles, or other state outside DECSTR
/// and the explicit mode resets below.
/// </para>
/// <para>
/// Hosts without Kitty keyboard support receive the stream with keyboard controls
/// removed and resets forwarded unchanged.
/// </para>
/// </remarks>
internal sealed class Hmp1KeyboardPresentation : IHmp1KeyboardControlSink
{
    // Replaces RIS on a host with keyboard stacks. The owned pop precedes this and a
    // fresh owned push follows it, so the host's pre-existing entries survive.
    internal const string FullResetReplacement =
        "\x1b[?1049l" +
        "\x1b[!p" +
        "\x1b[?1000l\x1b[?1002l\x1b[?1003l\x1b[?1005l\x1b[?1006l\x1b[?1015l\x1b[?1004l\x1b[?2004l" +
        "\x1b[?7h\x1b[?25h" +
        "\x1b[0m\x1b[H\x1b[2J\x1b[3J";

    private readonly bool _hostSupportsKeyboard;
    private readonly Hmp1KeyboardControlScanner _scanner = new();
    private readonly ArrayBufferWriter<byte> _output = new();
    private readonly KittyKeyboardMode _main = new();
    private readonly KittyKeyboardMode _alternate = new();
    private bool _remoteAlternate;
    private Hmp1KeyboardOwnership _ownership;
    private int _emittedFlags = -1;
    private bool _pushOrPopInBatch;

    internal Hmp1KeyboardPresentation(bool hostSupportsKeyboard)
    {
        _hostSupportsKeyboard = hostSupportsKeyboard;
    }

    /// <summary>Whether the viewer's entry is known to be on the host.</summary>
    internal Hmp1KeyboardOwnership Ownership => _ownership;

    /// <summary>The remote producer's active flags on its current screen.</summary>
    internal int RemoteFlags => Active.Flags;

    internal bool RemoteAlternate => _remoteAlternate;

    private KittyKeyboardMode Active => _remoteAlternate ? _alternate : _main;

    private bool Managing => _hostSupportsKeyboard && _ownership != Hmp1KeyboardOwnership.Unknown;

    /// <summary>
    /// Writes <paramref name="data"/> to <paramref name="presentation"/> with remote
    /// keyboard controls translated onto the viewer's single owned host entry.
    /// <paramref name="stateSync"/> marks bytes that start a new remote baseline.
    /// </summary>
    internal async ValueTask WriteAsync(IHex1bTerminalPresentationAdapter presentation,
        ReadOnlyMemory<byte> data, bool stateSync, CancellationToken ct)
    {
        var ownership = _ownership;
        var emitted = _emittedFlags;
        if (stateSync)
        {
            _scanner.Reset();
            _main.Reset();
            _alternate.Reset();
            _remoteAlternate = false;
        }
        var output = Rewrite(data.Span);
        if (output.IsEmpty)
            return;
        try
        {
            // Failing here delivers nothing, so the host still holds the prior state.
            ct.ThrowIfCancellationRequested();
        }
        catch
        {
            _ownership = ownership;
            _emittedFlags = emitted;
            throw;
        }
        try
        {
            await presentation.WriteOutputAsync(output, ct).ConfigureAwait(false);
        }
        catch
        {
            // The presentation may have delivered any prefix of the batch.
            if (_pushOrPopInBatch)
                _ownership = Hmp1KeyboardOwnership.Unknown;
            _emittedFlags = -1;
            throw;
        }
    }

    /// <summary>
    /// Ends the connection's presentation: drops any held fragment and pops the
    /// owned entry when the host is known to hold it.
    /// </summary>
    internal async ValueTask CompleteAsync(IHex1bTerminalPresentationAdapter presentation, CancellationToken ct = default)
    {
        _scanner.Reset();
        _main.Reset();
        _alternate.Reset();
        _remoteAlternate = false;
        var owned = _ownership == Hmp1KeyboardOwnership.Owned;
        if (_ownership != Hmp1KeyboardOwnership.Unknown)
            _ownership = Hmp1KeyboardOwnership.None;
        _emittedFlags = -1;
        if (!owned)
            return;
        try
        {
            await presentation.WriteOutputAsync("\x1b[<u"u8.ToArray(), ct).ConfigureAwait(false);
        }
        catch
        {
            _ownership = Hmp1KeyboardOwnership.Unknown;
            throw;
        }
    }

    internal ReadOnlyMemory<byte> Rewrite(ReadOnlySpan<byte> data)
    {
        _output.ResetWrittenCount();
        _pushOrPopInBatch = false;
        if (data.IsEmpty)
            return ReadOnlyMemory<byte>.Empty;
        if (Managing && _ownership == Hmp1KeyboardOwnership.None)
        {
            WriteAscii("\x1b[>");
            WriteFlags(Active.Flags);
            WriteAscii("u");
            _ownership = Hmp1KeyboardOwnership.Owned;
            _emittedFlags = Active.Flags;
            _pushOrPopInBatch = true;
        }
        _scanner.Process(data, this);
        SyncFlags();
        return _output.WrittenSpan.ToArray();
    }

    void IHmp1KeyboardControlSink.Text(ReadOnlySpan<byte> bytes)
    {
        SyncFlags();
        _output.Write(bytes);
    }

    void IHmp1KeyboardControlSink.Keyboard(ReadOnlySpan<byte> sequence)
    {
        Active.Apply(Encoding.ASCII.GetString(sequence), out _);
    }

    void IHmp1KeyboardControlSink.ScreenMode(ReadOnlySpan<byte> sequence)
    {
        var alternate = sequence[^1] == (byte)'h';
        if (!Managing)
        {
            _remoteAlternate = alternate;
            _output.Write(sequence);
            return;
        }
        // Kitty keeps a stack per screen, so the owned entry moves with the screen.
        WriteAscii("\x1b[<u");
        _output.Write(sequence);
        _remoteAlternate = alternate;
        WriteAscii("\x1b[>");
        WriteFlags(Active.Flags);
        WriteAscii("u");
        _emittedFlags = Active.Flags;
        _pushOrPopInBatch = true;
    }

    void IHmp1KeyboardControlSink.FullReset(ReadOnlySpan<byte> sequence)
    {
        _main.Reset();
        _alternate.Reset();
        _remoteAlternate = false;
        if (!_hostSupportsKeyboard)
        {
            _output.Write(sequence);
            return;
        }
        if (_ownership == Hmp1KeyboardOwnership.Owned)
        {
            WriteAscii("\x1b[<u");
            WriteAscii(FullResetReplacement);
            WriteAscii("\x1b[>0u");
            _emittedFlags = 0;
            _pushOrPopInBatch = true;
        }
        else
        {
            WriteAscii(FullResetReplacement);
        }
    }

    private void SyncFlags()
    {
        if (!Managing || _ownership != Hmp1KeyboardOwnership.Owned || _emittedFlags == Active.Flags)
            return;
        WriteAscii("\x1b[=");
        WriteFlags(Active.Flags);
        WriteAscii("u");
        _emittedFlags = Active.Flags;
    }

    private void WriteFlags(int flags) =>
        WriteAscii(flags.ToString(CultureInfo.InvariantCulture));

    private void WriteAscii(string text)
    {
        var span = _output.GetSpan(text.Length);
        for (var i = 0; i < text.Length; i++)
            span[i] = (byte)text[i];
        _output.Advance(text.Length);
    }
}
