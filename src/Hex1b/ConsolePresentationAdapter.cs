using System.Runtime.InteropServices;
using System.Text;
using Hex1b.Input;
using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// Console presentation adapter using platform-specific raw mode for proper input handling.
/// </summary>
/// <remarks>
/// This adapter uses raw terminal mode (termios on Unix, SetConsoleMode on Windows)
/// to properly capture mouse events, escape sequences, and control characters.
/// </remarks>
public sealed class ConsolePresentationAdapter :
    IHex1bTerminalPresentationAdapter,
    ITerminalReflowProvider,
    IInternalTerminalReflowProvider
{
    private const uint KgpProbeImageId = 2147483647u;
    private static readonly byte[] KgpProbeQuery = Encoding.ASCII.GetBytes(
        $"\x1b_Gi={KgpProbeImageId},s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\");
    // OSC 11 with "?" payload asks the terminal to report its current default
    // background colour. The response comes back as ESC ] 11 ; rgb:RRRR/GGGG/BBBB
    // ST (where ST is either ESC \ or BEL). Most modern terminals support this
    // (xterm, iTerm2, kitty, WezTerm, Alacritty, Windows Terminal, VS Code).
    private static readonly byte[] BackgroundProbeQuery = Encoding.ASCII.GetBytes("\x1b]11;?\x1b\\");
    private static readonly byte[] MouseProbeQuery = "\x1b[?1003$p\x1b[?1006$p"u8.ToArray();
    private static readonly TimeSpan DefaultKgpProbeTimeout = TimeSpan.FromMilliseconds(150);

    // Sixel capability/metrics probe queries (#455). All are safe, universally
    // documented queries: DA1 is a basic ECMA-48 query every VT-compatible
    // terminal answers, and the XTWINOPS variants (CSI 14/16/18 t) are read-only
    // reports with no visible side effect. See docs/sixel-terminal-behavior.md
    // for the full discovery precedence these responses feed into.
    private static readonly byte[] Da1ProbeQuery = Encoding.ASCII.GetBytes("\x1b[c");
    // Kitty keyboard progressive enhancement support query (CSI ? u). Terminals that
    // implement the protocol answer CSI ? flags u before answering the DA1 query.
    private static readonly byte[] KittyKeyboardProbeQuery = "\x1b[?u"u8.ToArray();
    private static readonly byte[] Csi14ProbeQuery = Encoding.ASCII.GetBytes("\x1b[14t");
    private static readonly byte[] Csi16ProbeQuery = Encoding.ASCII.GetBytes("\x1b[16t");
    private static readonly byte[] Csi18ProbeQuery = Encoding.ASCII.GetBytes("\x1b[18t");
    private static readonly byte[] Osc1337CellSizeProbeQuery = Encoding.ASCII.GetBytes("\x1b]1337;ReportCellSize\x1b\\");

    // A cell dimension above this is treated as implausible/overflow garbage
    // rather than a real (if unusual) HiDPI or legacy display value. Real
    // terminals report single-digit-to-low-double-digit pixel cells; this bound
    // is deliberately generous so it only rejects clearly malformed data.
    private const double MaxPlausibleCellDimension = 10_000d;

    private readonly IConsoleDriver _driver;
    private readonly bool _enableMouse;
    private readonly bool _preserveOPost;
    private readonly TimeSpan _kgpProbeTimeout;
    private readonly bool _kittyDa1Conclusive;
    private readonly CancellationTokenSource _disposeCts = new();
    private ITerminalReflowProvider _reflowStrategy;
    private TerminalCapabilities _capabilities;
    private byte[] _prefetchedInput = [];
    private Encoding? _inputEncoding;
    private Decoder? _inputDecoder;
    private bool _kgpProbeCompleted;
    private bool _backgroundProbeCompleted;
    private bool _reflowEnabled;
    private bool _disposed;
    private bool _inRawMode;
    private SixelPresentationSupport? _declaredSixelSupport;
    private Sixel.SixelCellMetrics? _declaredSixelMetrics;
    private SixelCapabilityProbeDiagnostics _sixelDiagnostics = SixelCapabilityProbeDiagnostics.NotProbed;

    /// <summary>
    /// Creates a new console presentation adapter with raw mode support.
    /// </summary>
    /// <param name="enableMouse">
    /// Whether to advertise mouse support to the app when terminal mode queries
    /// do not explicitly reject it. Defaults to false on every platform.
    /// The workload controls actual reporting through escape sequences.
    /// </param>
    /// <param name="preserveOPost">
    /// If true, preserve output post-processing (LF→CRLF conversion) in raw mode.
    /// This is useful for WithProcess scenarios where child programs expect normal output handling.
    /// Defaults to false for full raw mode (required for terminal emulators and Hex1bApp).
    /// </param>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown if raw mode is not supported on the current platform.
    /// </exception>
    public ConsolePresentationAdapter(bool enableMouse = false, bool preserveOPost = false)
        : this(CreateConsoleDriver(), enableMouse, preserveOPost)
    {
    }

    internal ConsolePresentationAdapter(
        IConsoleDriver driver,
        bool enableMouse = false,
        bool preserveOPost = false,
        TimeSpan? kgpProbeTimeout = null,
        bool? kittyDa1Conclusive = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _enableMouse = enableMouse;
        _preserveOPost = preserveOPost;
        _kgpProbeTimeout = kgpProbeTimeout ?? DefaultKgpProbeTimeout;
        // ConPTY answers DA1 itself, possibly before the outer terminal's Kitty reply.
        _kittyDa1Conclusive = kittyDa1Conclusive ?? driver is not WindowsConsoleDriver;
        _capabilities = CreateCapabilities(supportsKgp: false);
        
        // Wire up resize events. A resize invalidates only derived cell metrics
        // (computed from window-pixel-size / grid-size reports, which change
        // with a resize); authoritative/declared metrics reflect font geometry
        // that a mere resize does not alter, so they are left untouched. See
        // docs/sixel-terminal-behavior.md for the full invalidation rationale.
        _driver.Resized += (w, h) =>
        {
            InvalidateDerivedSixelMetricsOnResize();
            Resized?.Invoke(w, h);
        };
        
        // Auto-detect terminal emulator reflow strategy (not enabled by default)
        _reflowStrategy = DetectReflowStrategy();
    }

    /// <summary>
    /// Declares Sixel support and, optionally, protocol cell metrics directly,
    /// skipping the active discovery probe entirely.
    /// </summary>
    /// <param name="support">
    /// The Sixel support level to report. Passing <see cref="SixelPresentationSupport.None"/>
    /// declares "confirmed unsupported," which is distinct from
    /// <see cref="SixelPresentationSupport.Unknown"/> — the value left in effect by
    /// never calling this method at all (support is then left to be discovered, or
    /// stays <see cref="SixelPresentationSupport.Unknown"/> if discovery does not run
    /// or does not conclude).
    /// </param>
    /// <param name="metrics">
    /// Known protocol cell metrics to report alongside <paramref name="support"/>, or
    /// <see langword="null"/> to let metrics still be probed for
    /// (<see cref="SixelMetricsProbeOutcome"/> attempts for support and metrics are
    /// independent).
    /// </param>
    /// <returns>This adapter for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Per the discovery precedence in
    /// <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see>, a direct
    /// declaration is the highest-precedence source and pre-empts Sixel probing (no
    /// XTWINOPS or cell-size queries are sent, and the Sixel attribute of any DA1 reply is
    /// ignored). DA1 itself is still sent because Kitty keyboard detection relies on it.
    /// Use this when the hosting
    /// application already knows the upstream terminal's capabilities out of band (for
    /// example, from its own configuration) and wants to avoid the round trip.
    /// </para>
    /// <para>
    /// Must be called before <see cref="EnterRawModeAsync"/> to take effect; calling it
    /// afterward has no effect on a probe that has already run.
    /// </para>
    /// </remarks>
    public ConsolePresentationAdapter WithSixelSupport(SixelPresentationSupport support, Sixel.SixelCellMetrics? metrics = null)
    {
        _declaredSixelSupport = support;
        _declaredSixelMetrics = metrics;
        _capabilities = _capabilities with
        {
            SupportsSixel = IsAdvertisableSupport(support),
            SixelSupport = support,
            SixelCellMetrics = metrics
        };
        return this;
    }

    /// <summary>
    /// Whether <paramref name="support"/> should be advertised to a hosted workload as
    /// "Sixel is available." Only an affirmative, effective support level counts —
    /// both <see cref="SixelPresentationSupport.Unknown"/> (not yet established) and
    /// <see cref="SixelPresentationSupport.None"/> (confirmed unsupported) must not be
    /// advertised.
    /// </summary>
    private static bool IsAdvertisableSupport(SixelPresentationSupport support) => support is
        SixelPresentationSupport.Native or
        SixelPresentationSupport.Headless;

    /// <summary>
    /// Gets bounded diagnostics describing how Sixel support and protocol cell
    /// metrics were discovered (or why they were not).
    /// </summary>
    /// <remarks>
    /// Reflects <see cref="SixelCapabilityProbeDiagnostics.NotProbed"/> until
    /// <see cref="EnterRawModeAsync"/> has run the probe (or a direct declaration via
    /// <see cref="WithSixelSupport"/> pre-empted it).
    /// </remarks>
    public SixelCapabilityProbeDiagnostics SixelProbeDiagnostics => _sixelDiagnostics;

    private void InvalidateDerivedSixelMetricsOnResize()
    {
        if (_capabilities.SixelCellMetrics is { Source: Sixel.SixelCellMetricsSource.Derived })
        {
            _capabilities = _capabilities with { SixelCellMetrics = null };
        }
    }

    /// <summary>
    /// Enables reflow using the auto-detected strategy for the current terminal emulator.
    /// By default, reflow is disabled and resize uses standard crop behavior.
    /// </summary>
    /// <returns>This adapter for fluent chaining.</returns>
    public ConsolePresentationAdapter WithReflow()
    {
        _reflowEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables reflow with a specific strategy, overriding auto-detection.
    /// By default, reflow is disabled and resize uses standard crop behavior.
    /// </summary>
    /// <param name="strategy">The reflow strategy to use during resize operations.</param>
    /// <returns>This adapter for fluent chaining.</returns>
    public ConsolePresentationAdapter WithReflow(ITerminalReflowProvider strategy)
    {
        _reflowStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _reflowEnabled = true;
        return this;
    }

    /// <inheritdoc/>
    public bool ReflowEnabled => _reflowEnabled;

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => _reflowStrategy.ShouldClearSoftWrapOnAbsolutePosition;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context) => _reflowStrategy.Reflow(context);

    bool IInternalTerminalReflowProvider.TryReflowWithAnchors(
        ReflowContext context,
        IReadOnlyList<TerminalReflowAnchor> anchors,
        out InternalReflowResult result)
        => InternalTerminalReflow.TryReflow(
            _reflowStrategy,
            context,
            anchors,
            out result);

    /// <summary>
    /// Detects the current terminal emulator and returns the appropriate reflow strategy.
    /// </summary>
    private static ITerminalReflowProvider DetectReflowStrategy()
    {
        return AutoReflowStrategy.Detect();
    }

    /// <inheritdoc />
    public int Width => _driver.Width;

    /// <inheritdoc />
    public int Height => _driver.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _capabilities;

    /// <summary>
    /// Always <see langword="true"/>: this adapter connects Hex1b directly to a real
    /// terminal's raw stdin/stdout, so DA1 and window-operation queries reach that
    /// terminal untouched and its reply is forwarded back untouched in turn.
    /// </summary>
    /// <inheritdoc />
    public bool AnswersProtocolQueriesDirectly => true;

    private static IConsoleDriver CreateConsoleDriver()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixConsoleDriver();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsConsoleDriver();
        }

        throw new PlatformNotSupportedException(
            $"Platform {Environment.OSVersion} is not supported.");
    }

    private TerminalCapabilities CreateCapabilities(bool supportsKgp)
    {
        return new TerminalCapabilities
        {
            SupportsMouse = _enableMouse,
            SupportsTrueColor = true,
            Supports256Colors = true,
            SupportsAlternateScreen = true,
            HandlesAlternateScreenNatively = true,  // Real upstream terminal handles buffer switching
            SupportsBracketedPaste = true,  // Raw mode can handle this
            SupportsStyledUnderlines = true,
            SupportsUnderlineColor = true,
            SupportsKgp = supportsKgp
        };
    }

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;

        _driver.Write(data.Span);
        _driver.Flush();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        
        var buffer = new byte[256];
        
        try
        {
            if (_prefetchedInput.Length > 0)
            {
                var prefetched = _prefetchedInput;
                _prefetchedInput = [];
                var result = NormalizeInputToUtf8(prefetched);
                if (!result.IsEmpty)
                {
                    return result;
                }
            }

            while (!linkedCts.Token.IsCancellationRequested)
            {
                var bytesRead = await _driver.ReadAsync(buffer, linkedCts.Token);

                if (bytesRead == 0)
                {
                    // EOF or cancelled
                    return ReadOnlyMemory<byte>.Empty;
                }

                var result = NormalizeInputToUtf8(buffer.AsSpan(0, bytesRead));
                if (!result.IsEmpty)
                {
                    return result;
                }
            }

            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    private ReadOnlyMemory<byte> NormalizeInputToUtf8(ReadOnlySpan<byte> input)
    {
        var encoding = _driver.InputEncoding;
        if (IsUtf8(encoding))
        {
            return input.ToArray();
        }

        if (_inputDecoder is null || !IsSameEncoding(_inputEncoding, encoding))
        {
            _inputEncoding = encoding;
            _inputDecoder = encoding.GetDecoder();
        }

        var chars = new char[encoding.GetMaxCharCount(input.Length)];
        var charCount = _inputDecoder.GetChars(input, chars, flush: false);
        if (charCount == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return Encoding.UTF8.GetBytes(new string(chars, 0, charCount));
    }

    private static bool IsUtf8(Encoding encoding)
        => encoding.CodePage == Encoding.UTF8.CodePage ||
           string.Equals(GetBodyNameOrNull(encoding), Encoding.UTF8.BodyName, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameEncoding(Encoding? left, Encoding right)
        => left is not null &&
           left.CodePage == right.CodePage &&
           string.Equals(GetBodyNameOrNull(left), GetBodyNameOrNull(right), StringComparison.OrdinalIgnoreCase);

    private static string? GetBodyNameOrNull(Encoding encoding)
    {
        try
        {
            return encoding.BodyName;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        _driver.Flush();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
    {
        if (_inRawMode) return ValueTask.CompletedTask;
        _inRawMode = true;

        // Enter raw mode for proper input capture
        // No escape sequences - screen mode is controlled by the workload
        _driver.EnterRawMode(_preserveOPost);

        return ProbeCapabilitiesAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default)
    {
        if (!_inRawMode) return ValueTask.CompletedTask;
        _inRawMode = false;

        // Drain any pending input before exiting raw mode
        for (int i = 0; i < 3; i++)
        {
            Thread.Sleep(20);
            _driver.DrainInput();
        }

        // Exit raw mode
        _driver.ExitRawMode();

        return ValueTask.CompletedTask;
    }

    private async ValueTask ProbeCapabilitiesAsync(CancellationToken ct)
    {
        if (_kgpProbeCompleted)
            return;

        _kgpProbeCompleted = true;

        var sixelProbeNeeded = _declaredSixelSupport is null;

        _driver.Write(KgpProbeQuery);
        // Piggyback an OSC 11 background-colour query, and (unless Sixel support was
        // already declared directly) the Sixel discovery queries, on the same bounded
        // probe pass rather than opening a second competing reader.
        _driver.Write(BackgroundProbeQuery);
        _driver.Write(MouseProbeQuery);
        // The Kitty query is always followed by DA1, which every terminal answers, so a
        // DA1 reply without a preceding Kitty reply can conclude "unsupported". DA1 is sent
        // even when Sixel support was declared; its Sixel attribute is then ignored.
        _driver.Write(KittyKeyboardProbeQuery);
        _driver.Write(Da1ProbeQuery);
        if (sixelProbeNeeded)
        {
            _driver.Write(Csi16ProbeQuery);
            _driver.Write(Csi14ProbeQuery);
            _driver.Write(Csi18ProbeQuery);
            _driver.Write(Osc1337CellSizeProbeQuery);
        }
        _driver.Flush();

        var bufferedInput = new List<byte>();
        var readBuffer = new byte[256];
        var probe = new ProbeState { SixelProbeNeeded = sixelProbeNeeded };

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        probeCts.CancelAfter(_kgpProbeTimeout);

        try
        {
            while (!probeCts.IsCancellationRequested)
            {
                var bytesRead = await _driver.ReadAsync(readBuffer, probeCts.Token);
                if (bytesRead <= 0)
                    break;

                bufferedInput.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());
                ConsumeProbeReplies(bufferedInput, probe);

                if (probe.IsComplete(_backgroundProbeCompleted))
                    break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !_disposeCts.IsCancellationRequested)
        {
            // Timed out waiting for a probe reply: treat unanswered probes as unsupported / use defaults.
        }
        finally
        {
            _capabilities = _capabilities with
            {
                SupportsKittyKeyboard = probe.KittyFlags is not null,
                SupportsMouse = _enableMouse &&
                    probe.MouseTracking is not (0 or 4) &&
                    probe.MouseEncoding is not (0 or 4)
            };
            System.Diagnostics.Trace.WriteLine(
                $"Mouse mode probe: 1003={probe.MouseTracking?.ToString() ?? "unknown"}, " +
                $"1006={probe.MouseEncoding?.ToString() ?? "unknown"}.");
            System.Diagnostics.Trace.WriteLine(
                $"Kitty keyboard probe: flags={probe.KittyFlags?.ToString() ?? "none"}, da1={probe.Da1Done}.");

            if (sixelProbeNeeded)
            {
                ResolveSixelCapabilities(
                    probe.Da1Done, probe.Da1Malformed, probe.Da1DeclaresSixel,
                    probe.Csi16.Done, probe.Csi16.Malformed, probe.Csi16.Value,
                    probe.Osc1337.Done, probe.Osc1337.Malformed, probe.Osc1337.Value,
                    probe.Csi14.Done, probe.Csi14.Malformed, probe.Csi14.Value,
                    probe.Csi18.Done, probe.Csi18.Malformed, probe.Csi18.Value);
            }

            if (bufferedInput.Count > 0)
            {
                AppendPrefetchedInput(CollectionsMarshal.AsSpan(bufferedInput));
            }
        }
    }

    private const double MaxPlausibleWindowPixelDimension = 1_000_000d;

    private static bool IsPlausibleCellDimension(double value) =>
        double.IsFinite(value) && value > 0 && value <= MaxPlausibleCellDimension;

    private static bool IsPlausibleWindowPixelDimension(double value) =>
        double.IsFinite(value) && value > 0 && value <= MaxPlausibleWindowPixelDimension;

    /// <summary>
    /// Consumes genuine probe replies from <paramref name="buffer"/>. Only complete,
    /// top-level, ESC-introduced frames are considered (see <see cref="ConsoleProbeFrameScanner"/>),
    /// so reply-shaped bytes inside bracketed paste or string controls are never consumed.
    /// Frames that are not a recognised, well-formed reply stay in the buffer as input.
    /// </summary>
    private void ConsumeProbeReplies(List<byte> buffer, ProbeState probe)
    {
        var span = CollectionsMarshal.AsSpan(buffer);
        List<(int Start, int End)>? consumed = null;
        foreach (var frame in ConsoleProbeFrameScanner.Scan(span, _driver.InputEncoding))
        {
            if (!frame.SevenBit)
                continue;

            var content = (ReadOnlySpan<byte>)span[frame.ContentStart..frame.ContentEnd];
            var claimed = frame.Kind switch
            {
                ConsoleProbeFrameScanner.FrameKind.Csi => TryClaimCsiReply(content, span[frame.End - 1], probe),
                ConsoleProbeFrameScanner.FrameKind.Osc => TryClaimOscReply(content, probe),
                ConsoleProbeFrameScanner.FrameKind.Apc => TryClaimApcReply(content, probe),
                _ => false,
            };
            if (claimed)
                (consumed ??= []).Add((frame.Start, frame.End));
        }

        if (consumed is null)
            return;
        for (var i = consumed.Count - 1; i >= 0; i--)
            buffer.RemoveRange(consumed[i].Start, consumed[i].End - consumed[i].Start);
    }

    private bool TryClaimCsiReply(ReadOnlySpan<byte> parameters, byte final, ProbeState probe)
    {
        if (!parameters.IsEmpty && parameters[0] == (byte)'?')
        {
            switch (final)
            {
                case (byte)'u':
                    // Kitty keyboard support reply (CSI ? flags u). Every valid reply belongs
                    // to the probe; the first one decides support. Malformed replies stay input.
                    if (!TryParseKittyFlags(parameters[1..], out var flags))
                        return false;
                    if (!probe.KittyDone)
                    {
                        probe.KittyDone = true;
                        probe.KittyFlags = flags;
                    }
                    return true;

                case (byte)'c':
                    if (probe.Da1Done || !TryParseDeviceAttributes(parameters[1..], out var sixelDeclared, out var malformed))
                        return false;
                    probe.Da1Done = true;
                    probe.Da1Malformed = malformed;
                    probe.Da1DeclaresSixel = malformed ? null : sixelDeclared;
                    // The specification treats DA1 before a Kitty reply as unsupported. ConPTY can
                    // answer DA1 before the outer terminal's Kitty reply, so there it waits instead.
                    if (_kittyDa1Conclusive && !probe.KittyDone)
                        probe.KittyDone = true;
                    return true;

                case (byte)'y':
                    return TryClaimMouseModeReply(parameters[1..], probe);
            }

            return false;
        }

        return final == (byte)'t' && probe.SixelProbeNeeded && TryClaimWindowOperationReply(parameters, probe);
    }

    /// <summary>
    /// Claims a DECRPM reply (<c>CSI ? mode ; status $ y</c>) for mouse modes 1003 and 1006.
    /// </summary>
    private static bool TryClaimMouseModeReply(ReadOnlySpan<byte> parameters, ProbeState probe)
    {
        // "1003;N$" or "1006;N$" with a single status digit 0-4.
        if (parameters.Length != 7 || parameters[4] != (byte)';' || parameters[6] != (byte)'$' ||
            parameters[5] is < (byte)'0' or > (byte)'4')
            return false;

        var status = parameters[5] - (byte)'0';
        if (parameters[..4].SequenceEqual("1003"u8) && probe.MouseTracking is null)
        {
            probe.MouseTracking = status;
            return true;
        }
        if (parameters[..4].SequenceEqual("1006"u8) && probe.MouseEncoding is null)
        {
            probe.MouseEncoding = status;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Claims an XTWINOPS report reply of the form <c>CSI Pn;Pn;Pn t</c> whose leading
    /// parameter is 4 (CSI 14 t), 6 (CSI 16 t) or 8 (CSI 18 t). Replies for other or
    /// already-answered report codes stay input, and ordinary keyboard sequences that start
    /// with a digit (for example <c>CSI 3 ~</c> for Delete) never match because they do not
    /// end in <c>t</c>.
    /// </summary>
    private static bool TryClaimWindowOperationReply(ReadOnlySpan<byte> parameters, ProbeState probe)
    {
        if (parameters.IsEmpty || parameters[0] is < (byte)'0' or > (byte)'9')
            return false;
        foreach (var b in parameters)
        {
            if (b != (byte)';' && b is < (byte)'0' or > (byte)'9')
                return false;
        }

        var parts = Encoding.ASCII.GetString(parameters).Split(';');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var code))
            return false;

        var reply = code switch
        {
            4 => probe.Csi14,
            6 => probe.Csi16,
            8 => probe.Csi18,
            _ => null,
        };
        if (reply is null || reply.Done)
            return false;

        var parseOk = double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var first);
        parseOk &= double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var second);
        reply.Done = true;
        reply.Malformed = !parseOk;
        if (parseOk)
            reply.Value = (first, second);
        return true;
    }

    /// <summary>
    /// Claims an OSC 11 background-colour reply (<c>OSC 11 ; rgb:RRRR/GGGG/BBBB ST</c>, with
    /// component widths of 1–4 hex digits) or an iTerm2 <c>OSC 1337 ; ReportCellSize=h;w[;scale] ST</c>
    /// reply. Malformed background replies and unrelated OSC 1337 subcommands stay input.
    /// </summary>
    private bool TryClaimOscReply(ReadOnlySpan<byte> content, ProbeState probe)
    {
        if (content.StartsWith("11;"u8))
        {
            if (_backgroundProbeCompleted || !TryParseRgbColor(Encoding.ASCII.GetString(content[3..]), out var rgb))
                return false;
            _capabilities = _capabilities with { DefaultBackground = rgb };
            _backgroundProbeCompleted = true;
            return true;
        }

        ReadOnlySpan<byte> cellSize = "1337;ReportCellSize="u8;
        if (!probe.SixelProbeNeeded || probe.Osc1337.Done || !content.StartsWith(cellSize))
            return false;

        // Height and width are reported in points; the pixel size is points * scale (older
        // iTerm2 versions omit scale, implying 1.0 / non-retina).
        var reply = probe.Osc1337;
        reply.Done = true;
        var fields = Encoding.ASCII.GetString(content[cellSize.Length..]).Split(';');
        var parseOk = fields.Length is 2 or 3;
        double height = 0, width = 0, scale = 1.0;
        if (parseOk)
        {
            parseOk = double.TryParse(fields[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out height);
            parseOk &= double.TryParse(fields[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out width);
            if (fields.Length == 3)
                parseOk &= double.TryParse(fields[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale);
        }

        reply.Malformed = !parseOk;
        if (parseOk)
            reply.Value = (height * scale, width * scale);
        return true;
    }

    /// <summary>Claims the Kitty graphics protocol probe reply (<c>APC G i=id;OK ST</c>).</summary>
    private bool TryClaimApcReply(ReadOnlySpan<byte> content, ProbeState probe)
    {
        if (probe.KgpDone || content.IsEmpty || content[0] != (byte)'G')
            return false;

        var response = Encoding.ASCII.GetString(content[1..]);
        if (!IsKgpProbeResponse(response, KgpProbeImageId))
            return false;

        probe.KgpDone = true;
        _capabilities = _capabilities with
        {
            SupportsKgp = response.AsSpan(response.IndexOf(';') + 1).SequenceEqual("OK")
        };
        return true;
    }

    private sealed class ProbeState
    {
        public bool SixelProbeNeeded;
        public bool KgpDone;
        public int? MouseTracking;
        public int? MouseEncoding;
        public bool KittyDone;
        public int? KittyFlags;
        public bool Da1Done;
        public bool Da1Malformed;
        public bool? Da1DeclaresSixel;
        public MetricReply Csi16 { get; } = new();
        public MetricReply Csi14 { get; } = new();
        public MetricReply Csi18 { get; } = new();
        public MetricReply Osc1337 { get; } = new();

        public bool IsComplete(bool backgroundDone) =>
            KgpDone && backgroundDone && KittyDone && Da1Done &&
            MouseTracking is not null && MouseEncoding is not null &&
            (!SixelProbeNeeded || (Csi16.Done && Csi14.Done && Csi18.Done && Osc1337.Done));
    }

    private sealed class MetricReply
    {
        public bool Done;
        public bool Malformed;
        public (double, double)? Value;
    }

    private static bool TryParseKittyFlags(ReadOnlySpan<byte> digits, out int flags)
    {
        flags = 0;
        if (digits.IsEmpty || digits.Length > 9)
            return false;
        foreach (var b in digits)
        {
            if (b is < (byte)'0' or > (byte)'9')
                return false;
            flags = flags * 10 + (b - '0');
        }
        return true;
    }

    private static bool TryParseDeviceAttributes(ReadOnlySpan<byte> parameters, out bool sixelDeclared, out bool malformed)
    {
        sixelDeclared = false;
        malformed = false;
        foreach (var b in parameters)
        {
            if (b != (byte)';' && b is < (byte)'0' or > (byte)'9')
                return false;
        }

        if (parameters.IsEmpty)
        {
            malformed = true;
            return true;
        }

        foreach (var part in Encoding.ASCII.GetString(parameters).Split(';'))
        {
            if (!int.TryParse(part, out var value))
                malformed = true;
            else if (value == 4)
                sixelDeclared = true;
        }

        sixelDeclared &= !malformed;
        return true;
    }

    /// <summary>
    /// Applies the Sixel discovery precedence documented in
    /// <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see> and
    /// <c>docs/sixel-terminal-behavior.md</c> to the raw probe results collected by
    /// <see cref="ProbeCapabilitiesAsync"/>, updating <see cref="Capabilities"/> and
    /// <see cref="SixelProbeDiagnostics"/>.
    /// </summary>
    private void ResolveSixelCapabilities(
        bool da1Done,
        bool da1Malformed,
        bool? da1DeclaresSixel,
        bool csi16Done,
        bool csi16Malformed,
        (double Height, double Width)? csi16,
        bool osc1337Done,
        bool osc1337Malformed,
        (double Height, double Width)? osc1337,
        bool csi14Done,
        bool csi14Malformed,
        (double Height, double Width)? csi14,
        bool csi18Done,
        bool csi18Malformed,
        (double Rows, double Cols)? csi18)
    {
        var attempts = new List<SixelMetricsProbeAttempt>(4);
        var accepted = new List<(SixelCellMetricsSource Source, double Height, double Width)>();
        Sixel.SixelCellMetrics? selected = null;

        // Tier 1: CSI 16 t (preferred for Sixel even over physical/OSC values).
        if (!csi16Done)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Csi16, SixelMetricsProbeOutcome.TimedOut));
        }
        else if (csi16Malformed)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Csi16, SixelMetricsProbeOutcome.Malformed, "CSI 16 t reply parameters were not valid numbers."));
        }
        else if (csi16 is { } c16 && IsPlausibleCellDimension(c16.Height) && IsPlausibleCellDimension(c16.Width))
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Csi16, SixelMetricsProbeOutcome.Accepted));
            accepted.Add((SixelCellMetricsSource.Csi16, c16.Height, c16.Width));
            selected ??= new Sixel.SixelCellMetrics(c16.Width, c16.Height, SixelCellMetricsSource.Csi16, SixelCellMetricsReliability.Authoritative);
        }
        else if (csi16 is { } badC16)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Csi16, SixelMetricsProbeOutcome.Rejected,
                $"CSI 16 t reported an implausible cell size {badC16.Width}x{badC16.Height}."));
        }

        // Tier 2: OSC 1337 ReportCellSize.
        if (!osc1337Done)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Osc1337, SixelMetricsProbeOutcome.TimedOut));
        }
        else if (osc1337Malformed)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Osc1337, SixelMetricsProbeOutcome.Malformed, "OSC 1337 ReportCellSize reply was not well-formed."));
        }
        else if (osc1337 is { } o && IsPlausibleCellDimension(o.Height) && IsPlausibleCellDimension(o.Width))
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Osc1337, SixelMetricsProbeOutcome.Accepted));
            accepted.Add((SixelCellMetricsSource.Osc1337, o.Height, o.Width));
            selected ??= new Sixel.SixelCellMetrics(o.Width, o.Height, SixelCellMetricsSource.Osc1337, SixelCellMetricsReliability.Authoritative);
        }
        else if (osc1337 is { } badOsc)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Osc1337, SixelMetricsProbeOutcome.Rejected,
                $"OSC 1337 ReportCellSize reported an implausible cell size {badOsc.Width}x{badOsc.Height}."));
        }

        // Tier 3: CSI 14 t (window pixels) divided by CSI 18 t (rows/cols), fractional.
        if (!csi14Done || !csi18Done)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.TimedOut,
                "CSI 14 t and/or CSI 18 t did not both reply in time."));
        }
        else if (csi14Malformed || csi18Malformed)
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.Malformed,
                "CSI 14 t or CSI 18 t reply parameters were not valid numbers."));
        }
        else if (csi14 is { } px && csi18 is { } grid &&
                 IsPlausibleWindowPixelDimension(px.Height) && IsPlausibleWindowPixelDimension(px.Width) &&
                 IsPlausibleCellDimension(grid.Rows) && IsPlausibleCellDimension(grid.Cols))
        {
            var derivedHeight = px.Height / grid.Rows;
            var derivedWidth = px.Width / grid.Cols;
            if (IsPlausibleCellDimension(derivedHeight) && IsPlausibleCellDimension(derivedWidth))
            {
                attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.Accepted));
                accepted.Add((SixelCellMetricsSource.Derived, derivedHeight, derivedWidth));
                selected ??= new Sixel.SixelCellMetrics(derivedWidth, derivedHeight, SixelCellMetricsSource.Derived, SixelCellMetricsReliability.Derived);
            }
            else
            {
                attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.Rejected,
                    $"CSI 14 t / CSI 18 t derived an implausible cell size {derivedWidth}x{derivedHeight}."));
            }
        }
        else
        {
            attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.Rejected,
                "CSI 14 t / CSI 18 t reported implausible window pixel or grid dimensions."));
        }

        // Tier 4: TIOCGWINSZ pixel fields (local, no query round trip), only when
        // nonzero/trustworthy and only as a last resort.
        if (selected is null &&
            _driver.TryGetWindowPixelSize(out var wsPixelWidth, out var wsPixelHeight) &&
            IsPlausibleWindowPixelDimension(wsPixelWidth) && IsPlausibleWindowPixelDimension(wsPixelHeight) &&
            _driver.Width > 0 && _driver.Height > 0)
        {
            var derivedHeight = (double)wsPixelHeight / _driver.Height;
            var derivedWidth = (double)wsPixelWidth / _driver.Width;
            if (IsPlausibleCellDimension(derivedHeight) && IsPlausibleCellDimension(derivedWidth))
            {
                attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.Accepted, "Derived from TIOCGWINSZ pixel fields."));
                accepted.Add((SixelCellMetricsSource.Derived, derivedHeight, derivedWidth));
                selected = new Sixel.SixelCellMetrics(derivedWidth, derivedHeight, SixelCellMetricsSource.Derived, SixelCellMetricsReliability.Derived);
            }
            else
            {
                attempts.Add(new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.Rejected,
                    $"TIOCGWINSZ derived an implausible cell size {derivedWidth}x{derivedHeight}."));
            }
        }

        var disagreement = false;
        string? disagreementDetail = null;
        if (accepted.Count > 1)
        {
            var (firstSource, firstHeight, firstWidth) = accepted[0];
            for (var i = 1; i < accepted.Count; i++)
            {
                var (source, height, width) = accepted[i];
                if (Math.Abs(height - firstHeight) > 0.5 || Math.Abs(width - firstWidth) > 0.5)
                {
                    disagreement = true;
                    disagreementDetail =
                        $"{firstSource} reported {firstWidth}x{firstHeight} but {source} reported {width}x{height}; " +
                        $"{firstSource} was used per discovery precedence.";
                    break;
                }
            }
        }

        bool supportsSixel;
        SixelPresentationSupport sixelSupport;
        bool? effectiveDa1DeclaresSixel;
        if (!da1Done || da1Malformed)
        {
            // Unknown: DA1 never answered (or answered unparseably). Do not claim
            // support since nothing renders it if we are wrong, and — unlike a DA1
            // reply that affirmatively omits parameter 4 — this is "unknown" rather
            // than "confirmed unsupported," both in the capability model itself
            // (SixelPresentationSupport.Unknown) and via Da1DeclaresSixel (null).
            supportsSixel = false;
            sixelSupport = SixelPresentationSupport.Unknown;
            effectiveDa1DeclaresSixel = null;
        }
        else
        {
            supportsSixel = da1DeclaresSixel == true;
            sixelSupport = supportsSixel ? SixelPresentationSupport.Native : SixelPresentationSupport.None;
            effectiveDa1DeclaresSixel = da1DeclaresSixel;
        }

        _capabilities = _capabilities with
        {
            SupportsSixel = supportsSixel,
            SixelSupport = sixelSupport,
            SixelCellMetrics = selected
        };

        _sixelDiagnostics = new SixelCapabilityProbeDiagnostics(
            Attempts: attempts,
            Da1DeclaresSixel: effectiveDa1DeclaresSixel,
            SelectedMetrics: selected,
            MetricsDisagreement: disagreement,
            DisagreementDetail: disagreementDetail);
    }

    private void AppendPrefetchedInput(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        if (_prefetchedInput.Length == 0)
        {
            _prefetchedInput = data.ToArray();
            return;
        }

        var combined = new byte[_prefetchedInput.Length + data.Length];
        _prefetchedInput.CopyTo(combined, 0);
        data.CopyTo(combined.AsSpan(_prefetchedInput.Length));
        _prefetchedInput = combined;
    }

    private static bool IsKgpProbeResponse(string response, uint probeImageId)
    {
        var separator = response.IndexOf(';');
        if (separator < 0)
            return false;

        var controlData = response[..separator];
        var fields = controlData.Split(',');
        foreach (var field in fields)
        {
            if (!field.StartsWith("i=", StringComparison.Ordinal))
                continue;

            if (uint.TryParse(field.AsSpan(2), out var imageId) && imageId == probeImageId)
                return true;
        }

        return false;
    }

    private static bool TryParseRgbColor(string payload, out int rgb)
    {
        rgb = 0;

        // Expected form: "rgb:RRRR/GGGG/BBBB" (xterm) or shorter widths.
        const string prefix = "rgb:";
        if (!payload.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var components = payload[prefix.Length..].Split('/');
        if (components.Length != 3)
            return false;

        Span<byte> rgbBytes = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            var c = components[i];
            if (c.Length == 0 || c.Length > 4)
                return false;
            if (!ushort.TryParse(c, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
                return false;

            // Scale variable-width hex (1..4 digits) up to 8 bits. xterm uses 4
            // digits per channel (16-bit), so RRRR maps to (RRRR >> 8). Shorter
            // widths are bit-extended by repeating the highest nibble.
            int scaled = c.Length switch
            {
                1 => (value << 4) | value,                  // 0xR  -> 0xRR
                2 => value,                                  // already 8-bit
                3 => (value << 4) | (value & 0x000F),        // pad low nibble
                4 => value >> 8,                             // xterm 16-bit -> 8-bit
                _ => value
            };
            rgbBytes[i] = (byte)Math.Clamp(scaled, 0, 255);
        }

        rgb = (rgbBytes[0] << 16) | (rgbBytes[1] << 8) | rgbBytes[2];
        return true;
    }

    /// <inheritdoc />
    public (int Row, int Column) GetCursorPosition()
    {
        try
        {
            var (left, top) = Console.GetCursorPosition();
            return (top, left);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

        if (_inRawMode)
        {
            await ExitRawModeAsync();
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _driver.Dispose();
    }
}
