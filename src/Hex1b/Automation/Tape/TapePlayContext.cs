namespace Hex1b.Automation;

/// <summary>Provides the effective playback settings while preparing a command's result.</summary>
/// <remarks>
/// Both validation and playback invoke command callbacks with this context before any input is sent.
/// Return a <see cref="TapeCommandResult"/> containing a builder or an error; do not execute input in the callback.
/// </remarks>
public sealed class TapePlayContext
{
    private readonly Func<TapeCommandResult>? _prepareBuiltIn;

    internal TapePlayContext(TimeSpan typingSpeed, TimeSpan waitTimeout, TimeProvider timeProvider,
        Func<TapeCommandResult>? prepareBuiltIn = null)
    {
        _prepareBuiltIn = prepareBuiltIn;
        TypingSpeed = typingSpeed;
        WaitTimeout = waitTimeout;
        SequenceOptions = new()
        {
            SlowTypeDelay = typingSpeed,
            PollInterval = TimeSpan.FromMilliseconds(10),
            TimeProvider = timeProvider
        };
    }

    /// <summary>Gets the effective delay between typed characters.</summary>
    public TimeSpan TypingSpeed { get; }

    /// <summary>Gets the effective default wait timeout.</summary>
    public TimeSpan WaitTimeout { get; }

    /// <summary>Gets immutable timing options for generated input sequences.</summary>
    public Hex1bTerminalInputSequenceOptions SequenceOptions { get; }

    internal TapeCommandResult PrepareBuiltIn() => _prepareBuiltIn?.Invoke()
        ?? throw new InvalidOperationException("This context cannot prepare a built-in command.");
}
