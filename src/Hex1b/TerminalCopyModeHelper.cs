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
            if (key.Modifiers == Hex1bModifiers.None && _options.EnterKeys.Contains(key.Key))
            {
                _handle.EnterCopyMode();
                return true;
            }
            return false;
        }

        // Copy mode key handling
        if (key.Modifiers == Hex1bModifiers.None && _options.CancelKeys.Contains(key.Key))
        {
            _handle.ExitCopyMode();
            return true;
        }

        if (key.Modifiers == Hex1bModifiers.None && _options.CopyKeys.Contains(key.Key))
        {
            _handle.CopySelection();
            return true;
        }

        // Navigation (no modifiers)
        if (key.Modifiers == Hex1bModifiers.None)
        {
            if (_options.CursorUpKeys.Contains(key.Key))
            { _handle.MoveCopyModeCursor(-1, 0); UpdateState(); return true; }
            if (_options.CursorDownKeys.Contains(key.Key))
            { _handle.MoveCopyModeCursor(1, 0); UpdateState(); return true; }
            if (_options.CursorLeftKeys.Contains(key.Key))
            { _handle.MoveCopyModeCursor(0, -1); UpdateState(); return true; }
            if (_options.CursorRightKeys.Contains(key.Key))
            { _handle.MoveCopyModeCursor(0, 1); UpdateState(); return true; }
            if (_options.WordForwardKeys.Contains(key.Key))
            { _handle.MoveWordForward(); UpdateState(); return true; }
            if (_options.WordBackwardKeys.Contains(key.Key))
            { _handle.MoveWordBackward(); UpdateState(); return true; }
            if (_options.PageUpKeys.Contains(key.Key))
            { _handle.MoveCopyModeCursor(-20, 0); UpdateState(); return true; }
            if (_options.PageDownKeys.Contains(key.Key))
            { _handle.MoveCopyModeCursor(20, 0); UpdateState(); return true; }
            if (_options.LineStartKeys.Contains(key.Key))
            { _handle.SetCopyModeCursorPosition(_handle.Selection!.Cursor.Row, 0); UpdateState(); return true; }
            if (_options.LineEndKeys.Contains(key.Key))
            { _handle.SetCopyModeCursorPosition(_handle.Selection!.Cursor.Row, _handle.Width - 1); UpdateState(); return true; }

            // Character selection (no modifier)
            if (_options.CharacterSelectionKeys.Contains(key.Key))
            { _handle.StartOrToggleSelection(SelectionMode.Character); UpdateState(); return true; }
        }

        // Keys with modifiers (buffer top/bottom, line/block selection)
        foreach (var (k, m) in _options.BufferTopKeys)
        {
            if (key.Key == k && key.Modifiers == m)
            { _handle.SetCopyModeCursorPosition(0, _handle.Selection!.Cursor.Column); UpdateState(); return true; }
        }
        foreach (var (k, m) in _options.BufferBottomKeys)
        {
            if (key.Key == k && key.Modifiers == m)
            { _handle.SetCopyModeCursorPosition(_handle.VirtualBufferHeight - 1, _handle.Selection!.Cursor.Column); UpdateState(); return true; }
        }
        foreach (var (k, m) in _options.LineSelectionKeys)
        {
            if (key.Key == k && key.Modifiers == m)
            { _handle.StartOrToggleSelection(SelectionMode.Line); UpdateState(); return true; }
        }
        foreach (var (k, m) in _options.BlockSelectionKeys)
        {
            if (key.Key == k && key.Modifiers == m)
            { _handle.StartOrToggleSelection(SelectionMode.Block); UpdateState(); return true; }
        }

        return true; // consume all keys in copy mode
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
