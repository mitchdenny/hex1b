using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Attaches standard copy mode key and mouse bindings to a <see cref="TerminalWidgetHandle"/>.
/// Created by <see cref="TerminalExtensions.CopyModeBindings"/> and subscribes to the
/// handle's <see cref="TerminalWidgetHandle.CopyModeInput"/> event internally.
/// </summary>
internal sealed class TerminalCopyModeHelper
{
    private readonly TerminalWidgetHandle _handle;
    private readonly CopyModeBindingsOptions _options;

    public TerminalCopyModeHelper(TerminalWidgetHandle handle, CopyModeBindingsOptions options)
    {
        _handle = handle;
        _options = options;
        _handle.CopyModeInput += HandleInput;
        _handle.CopyModeChanged += OnCopyModeChanged;
    }

    /// <summary>
    /// Detaches from the handle's events.
    /// </summary>
    public void Detach()
    {
        _handle.CopyModeInput -= HandleInput;
        _handle.CopyModeChanged -= OnCopyModeChanged;
    }

    private void OnCopyModeChanged(bool active)
    {
        _handle.UpdateCopyModeState();
    }

    private bool HandleInput(Hex1bEvent inputEvent)
    {
        if (inputEvent is Hex1bMouseEvent mouse)
            return HandleMouseInput(mouse);

        if (inputEvent is not Hex1bKeyEvent key)
            return false;

        // Entry key (when not in copy mode)
        if (!_handle.IsInCopyMode)
        {
            if (MatchesAny(_options.EnterKeys, key))
            {
                _handle.EnterCopyMode();
                return true;
            }
            return false;
        }

        // Copy mode key handling — cancel/copy first
        if (MatchesAny(_options.CancelKeys, key))
        {
            _handle.ExitCopyMode();
            return true;
        }

        if (MatchesAny(_options.CopyKeys, key))
        {
            _handle.CopySelection();
            return true;
        }

        // Selection mode bindings (checked before navigation to avoid conflicts like V vs cursor keys)
        if (MatchesAny(_options.LineSelectionKeys, key))
        { _handle.StartOrToggleSelection(SelectionMode.Line); UpdateState(); return true; }
        if (MatchesAny(_options.BlockSelectionKeys, key))
        { _handle.StartOrToggleSelection(SelectionMode.Block); UpdateState(); return true; }
        if (MatchesAny(_options.CharacterSelectionKeys, key))
        { _handle.StartOrToggleSelection(SelectionMode.Character); UpdateState(); return true; }

        // Buffer top/bottom
        if (MatchesAny(_options.BufferTopKeys, key))
        { _handle.SetCopyModeCursorPosition(0, _handle.Selection!.Cursor.Column); UpdateState(); return true; }
        if (MatchesAny(_options.BufferBottomKeys, key))
        { _handle.SetCopyModeCursorPosition(_handle.VirtualBufferHeight - 1, _handle.Selection!.Cursor.Column); UpdateState(); return true; }

        // Navigation
        if (MatchesAny(_options.CursorUpKeys, key))
        { _handle.MoveCopyModeCursor(-1, 0); UpdateState(); return true; }
        if (MatchesAny(_options.CursorDownKeys, key))
        { _handle.MoveCopyModeCursor(1, 0); UpdateState(); return true; }
        if (MatchesAny(_options.CursorLeftKeys, key))
        { _handle.MoveCopyModeCursor(0, -1); UpdateState(); return true; }
        if (MatchesAny(_options.CursorRightKeys, key))
        { _handle.MoveCopyModeCursor(0, 1); UpdateState(); return true; }
        if (MatchesAny(_options.WordForwardKeys, key))
        { _handle.MoveWordForward(); UpdateState(); return true; }
        if (MatchesAny(_options.WordBackwardKeys, key))
        { _handle.MoveWordBackward(); UpdateState(); return true; }
        if (MatchesAny(_options.PageUpKeys, key))
        { _handle.MoveCopyModeCursor(-20, 0); UpdateState(); return true; }
        if (MatchesAny(_options.PageDownKeys, key))
        { _handle.MoveCopyModeCursor(20, 0); UpdateState(); return true; }
        if (MatchesAny(_options.LineStartKeys, key))
        { _handle.SetCopyModeCursorPosition(_handle.Selection!.Cursor.Row, 0); UpdateState(); return true; }
        if (MatchesAny(_options.LineEndKeys, key))
        { _handle.SetCopyModeCursorPosition(_handle.Selection!.Cursor.Row, _handle.Width - 1); UpdateState(); return true; }

        return true; // consume all keys in copy mode
    }
    
    private static bool MatchesAny(KeyBinding[] bindings, Hex1bKeyEvent key)
    {
        foreach (var binding in bindings)
        {
            if (binding.Matches(key))
                return true;
        }
        return false;
    }

    private bool HandleMouseInput(Hex1bMouseEvent mouse)
    {
        if (!_options.MouseEnabled) return false;

        if (mouse.Button == MouseButton.Left)
        {
            var mode = mouse.Modifiers switch
            {
                var m when m == _options.MouseLineModifier => SelectionMode.Line,
                var m when m == _options.MouseBlockModifier => SelectionMode.Block,
                _ => SelectionMode.Character
            };
            _handle.MouseSelect(mouse.X, mouse.Y, mouse.Action, mode);
            UpdateState();
            return true;
        }

        if (mouse.Button == _options.MouseCopyButton && mouse.Action == MouseAction.Down && _handle.IsInCopyMode)
        {
            _handle.CopySelection();
            return true;
        }

        return false;
    }

    private void UpdateState()
    {
        _handle.UpdateCopyModeState();
    }
}
