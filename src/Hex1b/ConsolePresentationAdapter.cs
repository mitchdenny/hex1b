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
    IInternalTerminalReflowProvider,
    INativeUpstreamPresentationAdapter
{
    private const uint KgpProbeImageId = 2147483647u;
    private static readonly byte[] KgpProbeQuery = Encoding.ASCII.GetBytes(
        $"\x1b_Gi={KgpProbeImageId},s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\");
    // OSC 11 with "?" payload asks the terminal to report its current default
    // background colour. The response comes back as ESC ] 11 ; rgb:RRRR/GGGG/BBBB
    // ST (where ST is either ESC \ or BEL). Most modern terminals support this
    // (xterm, iTerm2, kitty, WezTerm, Alacritty, Windows Terminal, VS Code).
    private static readonly byte[] BackgroundProbeQuery = Encoding.ASCII.GetBytes("\x1b]11;?\x1b\\");
    private static readonly TimeSpan DefaultKgpProbeTimeout = TimeSpan.FromMilliseconds(150);

    // Sixel capability/metrics probe queries (#455). All are safe, universally
    // documented queries: DA1 is a basic ECMA-48 query every VT-compatible
    // terminal answers, and the XTWINOPS variants (CSI 14/16/18 t) are read-only
    // reports with no visible side effect. See docs/sixel-terminal-behavior.md
    // for the full discovery precedence these responses feed into.
    private static readonly byte[] Da1ProbeQuery = Encoding.ASCII.GetBytes("\x1b[c");
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
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
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
        TimeSpan? kgpProbeTimeout = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _enableMouse = enableMouse;
        _preserveOPost = preserveOPost;
        _kgpProbeTimeout = kgpProbeTimeout ?? DefaultKgpProbeTimeout;
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
    /// declaration is the highest-precedence source and pre-empts probing (no DA1 or
    /// XTWINOPS queries are sent once this has been called). Use this when the hosting
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
        SixelPresentationSupport.Translated or
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

        // Windows uses console input records rather than a raw stdin byte stream, so
        // none of these probe replies are currently readable through the built-in
        // console driver (see WindowsConsoleDriver's ENABLE_VIRTUAL_TERMINAL_INPUT /
        // lone-ESC-disambiguation notes). Sixel support and metrics stay unknown on
        // Windows unless declared directly via WithSixelSupport.
        if (_driver is WindowsConsoleDriver)
        {
            if (_declaredSixelSupport is null)
            {
                const string reason = "Windows console driver does not support Sixel capability probing.";
                _sixelDiagnostics = new SixelCapabilityProbeDiagnostics(
                    Attempts:
                    [
                        new SixelMetricsProbeAttempt(SixelCellMetricsSource.Csi16, SixelMetricsProbeOutcome.NotAttempted, reason),
                        new SixelMetricsProbeAttempt(SixelCellMetricsSource.Osc1337, SixelMetricsProbeOutcome.NotAttempted, reason),
                        new SixelMetricsProbeAttempt(SixelCellMetricsSource.Derived, SixelMetricsProbeOutcome.NotAttempted, reason)
                    ],
                    Da1DeclaresSixel: null,
                    SelectedMetrics: null,
                    MetricsDisagreement: false,
                    DisagreementDetail: null);
                // Explicit, not just relying on the enum default: capability
                // discovery could not run, so support is unknown, never "confirmed
                // unsupported."
                _capabilities = _capabilities with { SixelSupport = SixelPresentationSupport.Unknown };
            }
            return;
        }

        var sixelProbeNeeded = _declaredSixelSupport is null;

        _driver.Write(KgpProbeQuery);
        // Piggyback an OSC 11 background-colour query, and (unless Sixel support was
        // already declared directly) the Sixel discovery queries, on the same bounded
        // probe pass rather than opening a second competing reader.
        _driver.Write(BackgroundProbeQuery);
        if (sixelProbeNeeded)
        {
            _driver.Write(Da1ProbeQuery);
            _driver.Write(Csi16ProbeQuery);
            _driver.Write(Csi14ProbeQuery);
            _driver.Write(Csi18ProbeQuery);
            _driver.Write(Osc1337CellSizeProbeQuery);
        }
        _driver.Flush();

        var bufferedInput = new List<byte>();
        var readBuffer = new byte[256];

        bool? da1DeclaresSixel = null;
        var da1Done = !sixelProbeNeeded;
        var da1Malformed = false;

        (double Height, double Width)? csi16 = null;
        var csi16Done = !sixelProbeNeeded;
        var csi16Malformed = false;

        (double Height, double Width)? csi14 = null;
        var csi14Done = !sixelProbeNeeded;
        var csi14Malformed = false;

        (double Rows, double Cols)? csi18 = null;
        var csi18Done = !sixelProbeNeeded;
        var csi18Malformed = false;

        (double Height, double Width)? osc1337 = null;
        var osc1337Done = !sixelProbeNeeded;
        var osc1337Malformed = false;

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

                if (TryConsumeKgpProbeResponse(bufferedInput, KgpProbeImageId))
                {
                    _capabilities = _capabilities with { SupportsKgp = true };
                }

                if (!_backgroundProbeCompleted &&
                    TryConsumeBackgroundProbeResponse(bufferedInput, out var bgRgb))
                {
                    _capabilities = _capabilities with { DefaultBackground = bgRgb };
                    _backgroundProbeCompleted = true;
                }

                if (sixelProbeNeeded)
                {
                    if (!da1Done && TryConsumeDeviceAttributesResponse(bufferedInput, out var sixelDeclared, out var da1Bad))
                    {
                        da1Done = true;
                        da1Malformed = da1Bad;
                        da1DeclaresSixel = da1Bad ? null : sixelDeclared;
                    }

                    if (!csi16Done && TryConsumeWindowOperationResponse(bufferedInput, reportCode: 6, out var c16A, out var c16B, out var c16Bad))
                    {
                        csi16Done = true;
                        csi16Malformed = c16Bad;
                        if (!c16Bad)
                            csi16 = (c16A, c16B);
                    }

                    if (!csi14Done && TryConsumeWindowOperationResponse(bufferedInput, reportCode: 4, out var c14A, out var c14B, out var c14Bad))
                    {
                        csi14Done = true;
                        csi14Malformed = c14Bad;
                        if (!c14Bad)
                            csi14 = (c14A, c14B);
                    }

                    if (!csi18Done && TryConsumeWindowOperationResponse(bufferedInput, reportCode: 8, out var c18A, out var c18B, out var c18Bad))
                    {
                        csi18Done = true;
                        csi18Malformed = c18Bad;
                        if (!c18Bad)
                            csi18 = (c18A, c18B);
                    }

                    if (!osc1337Done && TryConsumeOsc1337CellSizeResponse(bufferedInput, out var oH, out var oW, out var oscBad))
                    {
                        osc1337Done = true;
                        osc1337Malformed = oscBad;
                        if (!oscBad)
                            osc1337 = (oH, oW);
                    }
                }

                if (_capabilities.SupportsKgp && _backgroundProbeCompleted &&
                    da1Done && csi16Done && csi14Done && csi18Done && osc1337Done)
                    break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !_disposeCts.IsCancellationRequested)
        {
            // Timed out waiting for a probe reply: treat unanswered probes as unsupported / use defaults.
        }
        finally
        {
            if (sixelProbeNeeded)
            {
                ResolveSixelCapabilities(
                    da1Done, da1Malformed, da1DeclaresSixel,
                    csi16Done, csi16Malformed, csi16,
                    osc1337Done, osc1337Malformed, osc1337,
                    csi14Done, csi14Malformed, csi14,
                    csi18Done, csi18Malformed, csi18);
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
    /// Scans for a DA1 (Primary Device Attributes) reply of the form
    /// <c>CSI ? Pn(;Pn)* c</c> and reports whether it declares Sixel support via DEC
    /// parameter 4. Only sequences that include the <c>?</c> private-parameter marker
    /// (which every DA1 reply this library targets includes) are treated as replies,
    /// so a workload-issued bare <c>CSI c</c> query is never misinterpreted as one.
    /// </summary>
    private static bool TryConsumeDeviceAttributesResponse(List<byte> buffer, out bool sixelDeclared, out bool malformed)
    {
        sixelDeclared = false;
        malformed = false;

        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 4; start++)
        {
            if (span[start] != 0x1b || span[start + 1] != (byte)'[' || span[start + 2] != (byte)'?')
                continue;

            var scanStart = start + 3;
            var end = -1;
            for (var i = scanStart; i < span.Length; i++)
            {
                var b = span[i];
                if (b == (byte)'c')
                {
                    end = i;
                    break;
                }
                if (b != (byte)';' && (b < (byte)'0' || b > (byte)'9'))
                    break; // Not a DA1 reply at this position.
            }

            if (end < 0)
                continue; // Either not a DA1 reply, or the terminator has not arrived yet.

            var paramsText = Encoding.ASCII.GetString(span[scanStart..end]);
            buffer.RemoveRange(start, end + 1 - start);

            if (paramsText.Length == 0)
            {
                malformed = true;
                return true;
            }

            var declaresSixel = false;
            var parseOk = true;
            foreach (var part in paramsText.Split(';'))
            {
                if (!int.TryParse(part, out var value))
                {
                    parseOk = false;
                    continue;
                }
                if (value == 4)
                    declaresSixel = true;
            }

            malformed = !parseOk;
            sixelDeclared = parseOk && declaresSixel;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Scans for an XTWINOPS report reply of the form <c>CSI Pn;Pn;Pn t</c> whose
    /// leading parameter matches <paramref name="reportCode"/> (4 for a CSI 14 t
    /// reply, 6 for CSI 16 t, 8 for CSI 18 t). Replies for a different report code are
    /// left untouched (not consumed) so a later call for that code can still find
    /// them, and ordinary keyboard sequences that happen to start with a digit (for
    /// example <c>CSI 3 ~</c> for Delete) never match because they do not end in
    /// <c>t</c>.
    /// </summary>
    private static bool TryConsumeWindowOperationResponse(
        List<byte> buffer,
        int reportCode,
        out double first,
        out double second,
        out bool malformed)
    {
        first = 0;
        second = 0;
        malformed = false;

        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 3; start++)
        {
            if (span[start] != 0x1b || span[start + 1] != (byte)'[')
                continue;

            var scanStart = start + 2;
            if (scanStart >= span.Length || span[scanStart] < (byte)'0' || span[scanStart] > (byte)'9')
                continue; // DA1 replies (and other non-digit-led CSI sequences) are handled elsewhere.

            var end = -1;
            for (var i = scanStart; i < span.Length; i++)
            {
                var b = span[i];
                if (b == (byte)'t')
                {
                    end = i;
                    break;
                }
                if (b != (byte)';' && (b < (byte)'0' || b > (byte)'9'))
                    break; // Not this kind of reply (e.g. a keyboard sequence terminated by '~' or a letter).
            }

            if (end < 0)
                continue; // Either not a window-op reply, or the terminator has not arrived yet.

            var paramsText = Encoding.ASCII.GetString(span[scanStart..end]);
            var parts = paramsText.Split(';');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var code) || code != reportCode)
                continue; // A different report code (or none we recognize); leave it untouched.

            var parseOk = double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out first);
            parseOk &= double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out second);

            buffer.RemoveRange(start, end + 1 - start);
            malformed = !parseOk;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Scans for an iTerm2 OSC 1337 <c>ReportCellSize</c> reply of the form
    /// <c>OSC 1337 ; ReportCellSize=[height];[width][;[scale]] ST</c>. Height and
    /// width are reported in points; when a scale factor is present, the pixel size
    /// is <c>points * scale</c> (older iTerm2 versions omit scale, implying
    /// 1.0 / non-retina). Other, unrelated OSC 1337 subcommands are left untouched.
    /// </summary>
    private static bool TryConsumeOsc1337CellSizeResponse(List<byte> buffer, out double heightPixels, out double widthPixels, out bool malformed)
    {
        heightPixels = 0;
        widthPixels = 0;
        malformed = false;

        const string signature = "ReportCellSize=";

        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 7; start++)
        {
            if (span[start] != 0x1b || span[start + 1] != (byte)']' ||
                span[start + 2] != (byte)'1' || span[start + 3] != (byte)'3' ||
                span[start + 4] != (byte)'3' || span[start + 5] != (byte)'7' ||
                span[start + 6] != (byte)';')
                continue;

            var payloadStart = start + 7;
            var end = -1;
            var terminatorLength = 0;
            for (var i = payloadStart; i < span.Length; i++)
            {
                if (span[i] == 0x07)
                {
                    end = i;
                    terminatorLength = 1;
                    break;
                }
                if (span[i] == 0x1b && i + 1 < span.Length && span[i + 1] == (byte)'\\')
                {
                    end = i;
                    terminatorLength = 2;
                    break;
                }
            }

            if (end < 0)
                return false; // Payload incomplete; wait for more bytes.

            var payload = Encoding.ASCII.GetString(span[payloadStart..end]);
            if (!payload.StartsWith(signature, StringComparison.Ordinal))
                continue; // A different OSC 1337 subcommand; leave it untouched.

            buffer.RemoveRange(start, end + terminatorLength - start);

            var fields = payload[signature.Length..].Split(';');
            if (fields.Length is < 2 or > 3)
            {
                malformed = true;
                return true;
            }

            var parseOk = double.TryParse(fields[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height);
            parseOk &= double.TryParse(fields[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width);

            var scale = 1.0;
            if (fields.Length == 3 && !double.TryParse(fields[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale))
                parseOk = false;

            if (!parseOk)
            {
                malformed = true;
                return true;
            }

            heightPixels = height * scale;
            widthPixels = width * scale;
            return true;
        }

        return false;
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

    private static bool TryConsumeKgpProbeResponse(List<byte> buffer, uint probeImageId)
    {
        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 4; start++)
        {
            if (span[start] != 0x1b || span[start + 1] != (byte)'_' || span[start + 2] != (byte)'G')
                continue;

            for (var end = start + 3; end < span.Length - 1; end++)
            {
                if (span[end] != 0x1b || span[end + 1] != (byte)'\\')
                    continue;

                var content = Encoding.ASCII.GetString(span[(start + 3)..end]);
                if (IsKgpProbeResponse(content, probeImageId))
                {
                    buffer.RemoveRange(start, end + 2 - start);
                    return true;
                }

                start = end + 1;
                break;
            }
        }

        return false;
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

    /// <summary>
    /// Scans the buffered input for an OSC 11 background-colour reply. The reply
    /// has the shape <c>ESC ] 11 ; rgb:RRRR/GGGG/BBBB ST</c> where ST is either
    /// <c>ESC \</c> (string terminator) or <c>BEL</c> (0x07). Component widths
    /// vary by terminal — common widths are 4 hex digits (xterm) but 2 and even
    /// 1 are also seen in the wild — so we parse leniently and scale up.
    /// </summary>
    private static bool TryConsumeBackgroundProbeResponse(List<byte> buffer, out int rgb)
    {
        rgb = 0;
        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 5; start++)
        {
            // Match ESC ] 1 1 ;
            if (span[start] != 0x1b || span[start + 1] != (byte)']' ||
                span[start + 2] != (byte)'1' || span[start + 3] != (byte)'1' ||
                span[start + 4] != (byte)';')
                continue;

            // Find the string terminator (ESC \ or BEL).
            var end = -1;
            var terminatorLength = 0;
            for (var i = start + 5; i < span.Length; i++)
            {
                if (span[i] == 0x07)
                {
                    end = i;
                    terminatorLength = 1;
                    break;
                }
                if (span[i] == 0x1b && i + 1 < span.Length && span[i + 1] == (byte)'\\')
                {
                    end = i;
                    terminatorLength = 2;
                    break;
                }
            }

            if (end < 0)
                return false; // payload incomplete; wait for more bytes

            var payload = Encoding.ASCII.GetString(span[(start + 5)..end]);
            if (TryParseRgbColor(payload, out rgb))
            {
                buffer.RemoveRange(start, end + terminatorLength - start);
                return true;
            }

            // Malformed payload; skip past this match and keep looking.
            start = end + terminatorLength - 1;
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
