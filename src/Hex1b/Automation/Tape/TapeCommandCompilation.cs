using Hex1b.Input;

namespace Hex1b.Automation;

internal static class TapeCommandCompilation
{
    internal static TapeCommandResult Compile(TapeCommand command, TapeCompilationState state,
        List<TapeDiagnostic> diagnostics)
    {
        var builder = new Hex1bTerminalInputSequenceBuilder().WithOptions(state.Context.SequenceOptions);
        switch (command)
        {
            case TapeTypeCommand type:
                if (!Duration(type.Delay?.Value, state.TypingSpeed, out var typingDelay))
                    return InvalidDuration(type.Delay?.Value);
                builder.SlowType(type.Text, typingDelay);
                break;
            case TapeSleepCommand sleep:
                if (!Duration(sleep.Duration.Value, TimeSpan.Zero, out var pause))
                    return InvalidDuration(sleep.Duration.Value);
                builder.Wait(pause);
                break;
            case TapeKeyCommand key:
                var keyEvent = TapeKeyCompilation.CreateEvent(key.Key);
                if (keyEvent is null)
                    return Reject($"The '{key.Key}' key/viewport operation is not supported. Viewport scrolling is not mouse-wheel input.");
                if (!TapeRuntimeLiteral.TryDuration(key.Delay?.Value ?? "", out var keyDelay))
                {
                    keyDelay = state.TypingSpeed;
                    if (key.Delay is not null)
                        Warn("An invalid key delay uses TypingSpeed, matching VHS.");
                }
                if (!TapeRuntimeLiteral.TryRepeat(key.Count?.Value, out var count))
                    Warn("An invalid or overflowing key count uses one repetition, matching VHS.");
                builder._steps.Add(new TapeRepeatedKeyStep(keyEvent.Key, keyEvent.Text, keyEvent.Modifiers,
                    count, keyDelay));
                break;
            case TapeChordCommand chord:
                var modifiers = chord.Modifier switch
                {
                    "Ctrl" => Hex1bModifiers.Control,
                    "Alt" => Hex1bModifiers.Alt,
                    "Shift" => Hex1bModifiers.Shift,
                    _ => Hex1bModifiers.None
                };
                if (modifiers == Hex1bModifiers.None)
                    return Reject($"Unknown modifier '{chord.Modifier}'.");
                foreach (var part in chord.Parts)
                {
                    if (part.Any(c => c > 127))
                        return Reject("Non-ASCII modifier operands cannot be represented faithfully by this input backend.");
                    if (chord.Modifier == "Ctrl" && part is "Alt" or "Shift")
                    {
                        modifiers |= part == "Alt" ? Hex1bModifiers.Alt : Hex1bModifiers.Shift;
                        continue;
                    }
                    var chordKey = TapeKeyCompilation.CreateEvent(part, modifiers);
                    if (chordKey is not null)
                        builder._steps.Add(new KeyInputStep(chordKey.Key, chordKey.Text, chordKey.Modifiers));
                    else if (chord.Modifier is "Alt" or "Shift" && part.All(c => c is >= ' ' and <= '~'))
                    {
                        foreach (var character in part)
                        {
                            var characterKey = TapeKeyCompilation.CreateEvent(character.ToString(), modifiers)!;
                            builder._steps.Add(new KeyInputStep(characterKey.Key, characterKey.Text, characterKey.Modifiers));
                        }
                    }
                    else
                        return Reject($"The chord operand '{part}' cannot be represented faithfully by this input backend.");
                }
                break;
            case TapeWaitCommand wait:
                if (wait.Scope is not (TapeWaitScope.Line or TapeWaitScope.Screen))
                    return Reject("Wait scope must be Line or Screen.");
                if (!Duration(wait.Timeout?.Value, state.WaitTimeout, out var timeout) || timeout <= TimeSpan.Zero)
                    return Reject("Wait requires a strictly positive timeout.");
                if (!TapeRuntimeRegex.TryCompile(wait.Pattern ?? state.WaitPattern, out var regex, out var regexError))
                    return Reject(regexError!);
                builder.WaitUntil(snapshot => regex!.IsMatch(wait.Scope == TapeWaitScope.Line
                        ? TapeTextSnapshot.TrimRow(snapshot.GetLine(snapshot.CursorY))
                        : TapeTextSnapshot.BufferText(snapshot.Terminal)),
                    timeout, $"Tape Wait /{wait.Pattern ?? state.WaitPattern}/",
                    callerFilePath: wait.Span.SourceName, callerLineNumber: wait.Span.Line);
                break;
            case TapeSetCommand set:
                if (set.Setting is not (TapeSetting.TypingSpeed or TapeSetting.WaitTimeout or TapeSetting.WaitPattern))
                    return Reject($"Set {set.Setting} requires an unsupported presentation or execution capability.");
                if (!state.InPreamble && set.Setting != TapeSetting.TypingSpeed)
                {
                    Warn($"Late Set {set.Setting} is ignored, matching VHS's setup ordering.");
                    return TapeCommandResult.NoInput(skip: true);
                }
                if (set.Setting == TapeSetting.WaitPattern)
                {
                    if (!TapeRuntimeRegex.TryCompile(set.Value.Value, out _, out var error))
                        return Reject(error!);
                    state.WaitPattern = set.Value.Value;
                }
                else
                {
                    if (!Duration(set.Value.Value, TimeSpan.Zero, out var duration))
                        return InvalidDuration(set.Value.Value);
                    if (set.Setting == TapeSetting.TypingSpeed)
                        state.TypingSpeed = duration;
                    else if (duration > TimeSpan.Zero)
                        state.WaitTimeout = duration;
                    else
                        return Reject("WaitTimeout must be strictly positive.");
                }
                return TapeCommandResult.NoInput();
            case TapeHideCommand:
                return TapeCommandResult.NoInput(captureControl: TapeCaptureControl.Hide);
            case TapeShowCommand:
                return TapeCommandResult.NoInput(captureControl: TapeCaptureControl.Show);
            default:
                return Reject($"No implementation is registered for {command.GetType().Name}.");
        }
        return TapeCommandResult.Accept(builder);

        bool Duration(string? text, TimeSpan fallback, out TimeSpan duration)
        {
            if (text is null)
            {
                duration = fallback;
                return true;
            }
            return TapeRuntimeLiteral.TryDuration(text, out duration);
        }

        TapeCommandResult InvalidDuration(string? text) =>
            Reject($"Duration '{text}' is invalid or exceeds this runtime's supported timer range.");

        TapeCommandResult Reject(string message) => TapeCommandResult.Unsupported(message);

        void Warn(string message) => diagnostics.Add(new("TAPE_VHS_BEHAVIOR", TapeDiagnosticSeverity.Warning,
            TapeDiagnosticStage.Compilation, message, command.Span));
    }
}
