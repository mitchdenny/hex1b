using System.Diagnostics;
using System.Runtime.CompilerServices;
using Hex1b.Input;

namespace Hex1b.Automation;

/// <summary>
/// Provides an imperative, async API for automating terminal interactions in tests.
/// Each method executes immediately and records its result in a step history,
/// providing rich diagnostic context when failures occur.
/// </summary>
/// <remarks>
/// <para>
/// The automator layers on top of <see cref="Hex1bTerminalInputSequenceBuilder"/> —
/// each method builds its own input sequence under the covers and runs it via
/// <see cref="Hex1bTerminalInputSequence.ApplyAsync"/>. The flow is always:
/// automator method → sequencer → steps.
/// </para>
/// <para>
/// When a step fails (e.g., a <see cref="WaitUntilStep"/> times out), the automator
/// wraps the exception in <see cref="Hex1bAutomationException"/> which includes the
/// full step history, terminal snapshot, and source location for debugging.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
/// await auto.WaitUntilTextAsync("File");
/// await auto.EnterAsync();
/// await auto.WaitUntilTextAsync("New");
/// await auto.DownAsync();
/// await auto.WaitUntilAsync(s => IsSelected(s, "Open"), description: "Open to be selected");
/// await auto.EnterAsync();
/// </code>
/// </example>
public sealed class Hex1bTerminalAutomator
{
    private readonly Hex1bTerminal _terminal;
    private readonly Hex1bTerminalInputSequenceOptions _options;
    private readonly TimeSpan _defaultTimeout;
    private readonly List<AutomationStepRecord> _completedSteps = [];
    private Hex1bModifiers _pendingModifiers = Hex1bModifiers.None;
    private int _mouseX;
    private int _mouseY;

    // Cached sequences for simple key operations (no modifiers)
    private readonly Dictionary<Hex1bKey, Hex1bTerminalInputSequence> _cachedKeySequences = new();

    /// <summary>
    /// Creates a new automator for the specified terminal.
    /// </summary>
    /// <param name="terminal">The terminal to automate.</param>
    /// <param name="defaultTimeout">Default timeout for <c>WaitUntil*Async</c> methods when no explicit timeout is provided.</param>
    public Hex1bTerminalAutomator(Hex1bTerminal terminal, TimeSpan defaultTimeout)
        : this(terminal, Hex1bTerminalInputSequenceOptions.Default, defaultTimeout)
    {
    }

    /// <summary>
    /// Creates a new automator for the specified terminal with custom options.
    /// </summary>
    /// <param name="terminal">The terminal to automate.</param>
    /// <param name="options">Options for controlling poll intervals, typing speed, and time provider.</param>
    /// <param name="defaultTimeout">Default timeout for <c>WaitUntil*Async</c> methods when no explicit timeout is provided.</param>
    public Hex1bTerminalAutomator(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        TimeSpan defaultTimeout)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _defaultTimeout = defaultTimeout;
    }

    /// <summary>
    /// Gets the list of all steps that have completed successfully.
    /// </summary>
    public IReadOnlyList<AutomationStepRecord> CompletedSteps => _completedSteps;

    /// <summary>
    /// Creates a snapshot of the current terminal state.
    /// </summary>
    public Hex1bTerminalSnapshot CreateSnapshot() => _terminal.CreateSnapshot();

    // ========================================
    // Wait conditions
    // ========================================

    /// <summary>
    /// Waits until a condition is met on the terminal.
    /// </summary>
    /// <param name="predicate">The condition to wait for. Receives a snapshot of the terminal state.</param>
    /// <param name="timeout">Maximum time to wait. If <c>null</c>, uses the default timeout.</param>
    /// <param name="description">Description for error messages. If not provided, the predicate expression is used.</param>
    /// <param name="predicateExpression">Auto-captured predicate source text. Do not pass explicitly.</param>
    /// <param name="callerFilePath">Auto-captured caller file path. Do not pass explicitly.</param>
    /// <param name="callerLineNumber">Auto-captured caller line number. Do not pass explicitly.</param>
    public async Task WaitUntilAsync(
        Func<Hex1bTerminalSnapshot, bool> predicate,
        TimeSpan? timeout = null,
        string? description = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var desc = description ?? predicateExpression ?? "condition";
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .WaitUntil(predicate, effectiveTimeout, desc)
            .Build();

        await RunAndRecordAsync(
            sequence,
            description is not null ? $"WaitUntil(\"{description}\")" : $"WaitUntil({predicateExpression})",
            default,
            callerFilePath,
            callerLineNumber);
    }

    /// <summary>
    /// Waits until the terminal contains the specified text.
    /// </summary>
    /// <param name="text">The text to wait for.</param>
    /// <param name="timeout">Maximum time to wait. If <c>null</c>, uses the default timeout.</param>
    /// <param name="callerFilePath">Auto-captured caller file path. Do not pass explicitly.</param>
    /// <param name="callerLineNumber">Auto-captured caller line number. Do not pass explicitly.</param>
    public async Task WaitUntilTextAsync(
        string text,
        TimeSpan? timeout = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .WaitUntil(s => s.ContainsText(text), effectiveTimeout, $"text \"{text}\" to appear")
            .Build();

        await RunAndRecordAsync(
            sequence,
            $"WaitUntilText(\"{text}\")",
            default,
            callerFilePath,
            callerLineNumber);
    }

    /// <summary>
    /// Waits until the terminal no longer contains the specified text.
    /// </summary>
    /// <param name="text">The text to wait to disappear.</param>
    /// <param name="timeout">Maximum time to wait. If <c>null</c>, uses the default timeout.</param>
    /// <param name="callerFilePath">Auto-captured caller file path. Do not pass explicitly.</param>
    /// <param name="callerLineNumber">Auto-captured caller line number. Do not pass explicitly.</param>
    public async Task WaitUntilNoTextAsync(
        string text,
        TimeSpan? timeout = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .WaitUntil(s => !s.ContainsText(text), effectiveTimeout, $"text \"{text}\" to disappear")
            .Build();

        await RunAndRecordAsync(
            sequence,
            $"WaitUntilNoText(\"{text}\")",
            default,
            callerFilePath,
            callerLineNumber);
    }

    /// <summary>
    /// Waits until the terminal enters alternate screen mode.
    /// </summary>
    /// <param name="timeout">Maximum time to wait. If <c>null</c>, uses the default timeout.</param>
    /// <param name="callerFilePath">Auto-captured caller file path. Do not pass explicitly.</param>
    /// <param name="callerLineNumber">Auto-captured caller line number. Do not pass explicitly.</param>
    public async Task WaitUntilAlternateScreenAsync(
        TimeSpan? timeout = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .WaitUntil(s => s.InAlternateScreen, effectiveTimeout, "alternate screen mode")
            .Build();

        await RunAndRecordAsync(
            sequence,
            "WaitUntilAlternateScreen()",
            default,
            callerFilePath,
            callerLineNumber);
    }

    // ========================================
    // Modifier prefixes
    // ========================================

    /// <summary>
    /// Adds Ctrl modifier to the next key or mouse action.
    /// </summary>
    public Hex1bTerminalAutomator Ctrl()
    {
        _pendingModifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Adds Shift modifier to the next key or mouse action.
    /// </summary>
    public Hex1bTerminalAutomator Shift()
    {
        _pendingModifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Adds Alt modifier to the next key or mouse action.
    /// </summary>
    public Hex1bTerminalAutomator Alt()
    {
        _pendingModifiers |= Hex1bModifiers.Alt;
        return this;
    }

    // ========================================
    // Key input
    // ========================================

    /// <summary>
    /// Sends a key press event.
    /// </summary>
    public async Task KeyAsync(
        Hex1bKey key,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var modifiers = _pendingModifiers;
        _pendingModifiers = Hex1bModifiers.None;

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .Key(key, modifiers)
            .Build();

        var desc = modifiers != Hex1bModifiers.None
            ? $"Key({FormatModifiers(modifiers)}{key})"
            : $"Key({key})";

        await RunAndRecordAsync(sequence, desc, ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Sends a key press event with explicit modifiers.
    /// </summary>
    public async Task KeyAsync(
        Hex1bKey key,
        Hex1bModifiers modifiers,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var combinedModifiers = _pendingModifiers | modifiers;
        _pendingModifiers = Hex1bModifiers.None;

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .Key(key, combinedModifiers)
            .Build();

        var desc = combinedModifiers != Hex1bModifiers.None
            ? $"Key({FormatModifiers(combinedModifiers)}{key})"
            : $"Key({key})";

        await RunAndRecordAsync(sequence, desc, ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Types text quickly (no delay between keystrokes).
    /// </summary>
    public async Task TypeAsync(
        string text,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .Type(text)
            .Build();

        var displayText = text.Length > 30 ? text[..27] + "..." : text;
        await RunAndRecordAsync(sequence, $"Type(\"{displayText}\")", ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Types text slowly with a delay between keystrokes.
    /// </summary>
    public async Task SlowTypeAsync(
        string text,
        TimeSpan? delay = null,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var builder = new Hex1bTerminalInputSequenceBuilder().WithOptions(_options);
        var sequence = delay.HasValue
            ? builder.SlowType(text, delay.Value).Build()
            : builder.SlowType(text).Build();

        var displayText = text.Length > 30 ? text[..27] + "..." : text;
        await RunAndRecordAsync(sequence, $"SlowType(\"{displayText}\")", ct, callerFilePath, callerLineNumber);
    }

    // ========================================
    // Common key shortcuts
    // ========================================

    /// <summary>Sends Enter key.</summary>
    public Task EnterAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Enter, "Key(Enter)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Tab key.</summary>
    public Task TabAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Tab, "Key(Tab)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Escape key.</summary>
    public Task EscapeAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Escape, "Key(Escape)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Backspace key.</summary>
    public Task BackspaceAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Backspace, "Key(Backspace)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Delete key.</summary>
    public Task DeleteAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Delete, "Key(Delete)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Space key.</summary>
    public Task SpaceAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Spacebar, "Key(Space)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Up arrow key.</summary>
    public Task UpAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.UpArrow, "Key(UpArrow)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Down arrow key.</summary>
    public Task DownAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.DownArrow, "Key(DownArrow)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Left arrow key.</summary>
    public Task LeftAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.LeftArrow, "Key(LeftArrow)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Right arrow key.</summary>
    public Task RightAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.RightArrow, "Key(RightArrow)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Home key.</summary>
    public Task HomeAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.Home, "Key(Home)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends End key.</summary>
    public Task EndAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.End, "Key(End)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Page Up key.</summary>
    public Task PageUpAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.PageUp, "Key(PageUp)", ct, callerFilePath, callerLineNumber);

    /// <summary>Sends Page Down key.</summary>
    public Task PageDownAsync(CancellationToken ct = default, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => RunCachedKeyAsync(Hex1bKey.PageDown, "Key(PageDown)", ct, callerFilePath, callerLineNumber);

    // ========================================
    // Mouse input
    // ========================================

    /// <summary>
    /// Moves the mouse to an absolute position.
    /// </summary>
    public async Task MouseMoveToAsync(
        int x,
        int y,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _mouseX = x;
        _mouseY = y;

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .MouseMoveTo(x, y)
            .Build();

        await RunAndRecordAsync(sequence, $"MouseMoveTo({x}, {y})", ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Performs a click at the specified position.
    /// </summary>
    public async Task ClickAtAsync(
        int x,
        int y,
        MouseButton button = MouseButton.Left,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _mouseX = x;
        _mouseY = y;

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .ClickAt(x, y, button)
            .Build();

        await RunAndRecordAsync(sequence, $"ClickAt({x}, {y})", ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Performs a double-click at the specified position.
    /// </summary>
    public async Task DoubleClickAtAsync(
        int x,
        int y,
        MouseButton button = MouseButton.Left,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _mouseX = x;
        _mouseY = y;

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .DoubleClickAt(x, y, button)
            .Build();

        await RunAndRecordAsync(sequence, $"DoubleClickAt({x}, {y})", ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Performs a drag from one position to another.
    /// </summary>
    public async Task DragAsync(
        int fromX,
        int fromY,
        int toX,
        int toY,
        MouseButton button = MouseButton.Left,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _mouseX = toX;
        _mouseY = toY;

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .Drag(fromX, fromY, toX, toY, button)
            .Build();

        await RunAndRecordAsync(
            sequence, $"Drag({fromX},{fromY} → {toX},{toY})", ct, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Scrolls up at the current mouse position.
    /// </summary>
    public async Task ScrollUpAsync(
        int ticks = 1,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .MouseMoveTo(_mouseX, _mouseY)
            .ScrollUp(ticks);

        await RunAndRecordAsync(
            builder.Build(),
            ticks == 1 ? "ScrollUp()" : $"ScrollUp({ticks})",
            ct,
            callerFilePath,
            callerLineNumber);
    }

    /// <summary>
    /// Scrolls down at the current mouse position.
    /// </summary>
    public async Task ScrollDownAsync(
        int ticks = 1,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .MouseMoveTo(_mouseX, _mouseY)
            .ScrollDown(ticks);

        await RunAndRecordAsync(
            builder.Build(),
            ticks == 1 ? "ScrollDown()" : $"ScrollDown({ticks})",
            ct,
            callerFilePath,
            callerLineNumber);
    }

    // ========================================
    // Timing
    // ========================================

    /// <summary>
    /// Pauses for the specified duration.
    /// </summary>
    public async Task WaitAsync(
        TimeSpan duration,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(_options)
            .Wait(duration)
            .Build();

        await RunAndRecordAsync(
            sequence,
            $"Wait({duration.TotalMilliseconds:F0}ms)",
            ct,
            callerFilePath,
            callerLineNumber);
    }

    /// <summary>
    /// Pauses for the specified number of milliseconds.
    /// </summary>
    public Task WaitAsync(
        int milliseconds,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
        => WaitAsync(TimeSpan.FromMilliseconds(milliseconds), ct, callerFilePath, callerLineNumber);

    // ========================================
    // Composability
    // ========================================

    /// <summary>
    /// Builds and runs an input sequence inline.
    /// The sequence is tracked as a single step in the automator's history.
    /// </summary>
    /// <param name="configure">Action to configure the sequence builder.</param>
    /// <param name="description">Description for error messages and step history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="callerFilePath">Auto-captured caller file path. Do not pass explicitly.</param>
    /// <param name="callerLineNumber">Auto-captured caller line number. Do not pass explicitly.</param>
    public async Task SequenceAsync(
        Action<Hex1bTerminalInputSequenceBuilder> configure,
        string? description = null,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var builder = new Hex1bTerminalInputSequenceBuilder().WithOptions(_options);
        configure(builder);
        var sequence = builder.Build();

        await RunAndRecordAsync(
            sequence,
            description ?? "Sequence",
            ct,
            callerFilePath,
            callerLineNumber);
    }

    /// <summary>
    /// Runs a pre-built input sequence.
    /// The sequence is tracked as a single step in the automator's history.
    /// </summary>
    /// <param name="sequence">The pre-built sequence to run.</param>
    /// <param name="description">Description for error messages and step history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="callerFilePath">Auto-captured caller file path. Do not pass explicitly.</param>
    /// <param name="callerLineNumber">Auto-captured caller line number. Do not pass explicitly.</param>
    public async Task SequenceAsync(
        Hex1bTerminalInputSequence sequence,
        string? description = null,
        CancellationToken ct = default,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        await RunAndRecordAsync(
            sequence,
            description ?? "Sequence",
            ct,
            callerFilePath,
            callerLineNumber);
    }

    // ========================================
    // Internal execution
    // ========================================

    private async Task RunCachedKeyAsync(
        Hex1bKey key,
        string description,
        CancellationToken ct,
        string? callerFilePath,
        int callerLineNumber)
    {
        if (_pendingModifiers != Hex1bModifiers.None)
        {
            // Modifiers present — can't use cache, build fresh
            await KeyAsync(key, ct, callerFilePath, callerLineNumber);
            return;
        }

        if (!_cachedKeySequences.TryGetValue(key, out var sequence))
        {
            sequence = new Hex1bTerminalInputSequenceBuilder()
                .WithOptions(_options)
                .Key(key)
                .Build();
            _cachedKeySequences[key] = sequence;
        }

        await RunAndRecordAsync(sequence, description, ct, callerFilePath, callerLineNumber);
    }

    private async Task RunAndRecordAsync(
        Hex1bTerminalInputSequence sequence,
        string description,
        CancellationToken ct,
        string? callerFilePath = null,
        int callerLineNumber = 0)
    {
        var stepIndex = _completedSteps.Count + 1;
        var sw = Stopwatch.StartNew();
        try
        {
            await sequence.ApplyAsync(_terminal, ct);
            sw.Stop();
            _completedSteps.Add(new AutomationStepRecord(
                stepIndex, description, sw.Elapsed, callerFilePath, callerLineNumber));
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Get terminal snapshot from the inner exception if available, otherwise capture fresh
            var snapshot = (ex as WaitUntilTimeoutException)?.TerminalSnapshot
                ?? TryCreateSnapshot();

            throw new Hex1bAutomationException(
                failedStepIndex: stepIndex,
                failedStepDescription: description,
                completedSteps: _completedSteps.ToList(),
                failedStepElapsed: sw.Elapsed,
                terminalSnapshot: snapshot,
                callerFilePath: callerFilePath,
                callerLineNumber: callerLineNumber,
                innerException: ex);
        }
    }

    private Hex1bTerminalSnapshot? TryCreateSnapshot()
    {
        try
        {
            return _terminal.CreateSnapshot();
        }
        catch
        {
            return null;
        }
    }

    private static string FormatModifiers(Hex1bModifiers modifiers)
    {
        var parts = new List<string>(3);
        if ((modifiers & Hex1bModifiers.Control) != 0) parts.Add("Ctrl+");
        if ((modifiers & Hex1bModifiers.Alt) != 0) parts.Add("Alt+");
        if ((modifiers & Hex1bModifiers.Shift) != 0) parts.Add("Shift+");
        return string.Join("", parts);
    }
}
