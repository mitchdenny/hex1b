using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="SplitButtonWidget"/>.
/// Displays a primary action button with an optional dropdown for secondary actions.
/// </summary>
public sealed class SplitButtonNode : Hex1bNode
{
    /// <summary>
    /// The label for the primary action.
    /// </summary>
    public string PrimaryLabel { get; set; } = "";

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public SplitButtonWidget? SourceWidget { get; set; }

    /// <summary>
    /// The async action to execute when the primary button is clicked.
    /// </summary>
    public Func<InputBindingActionContext, Task>? PrimaryAction { get; set; }

    /// <summary>
    /// The secondary actions shown in the dropdown.
    /// </summary>
    public IReadOnlyList<SplitButtonAction> SecondaryActions { get; set; } = [];

    /// <summary>
    /// Callback invoked when the dropdown menu is opened.
    /// </summary>
    public Action? DropdownOpenedCallback { get; set; }

    /// <summary>
    /// Whether the dropdown menu is currently open.
    /// </summary>
    public bool IsDropdownOpen { get; set; }

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    /// <summary>
    /// The dropdown arrow character.
    /// </summary>
    private const string DropdownArrow = "▼";

    /// <summary>
    /// The divider glyph rendered between the primary label and the dropdown
    /// arrow when secondary actions exist. Visually anchors the start of the
    /// secondary affordance region.
    /// </summary>
    private const string Divider = "│";

    /// <summary>
    /// Number of trailing cells the secondary affordance occupies when
    /// secondary actions exist: <c>│ ▼ </c> — divider, space, arrow,
    /// trailing pad. Drives both the render layout and the click hit-test.
    /// </summary>
    private const int ArrowRegionWidth = 4;

    /// <summary>
    /// Whether this button has secondary actions (shows dropdown arrow).
    /// </summary>
    private bool HasSecondaryActions => SecondaryActions.Count > 0;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Primary action on Enter/Space/Click
        if (PrimaryAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Triggers(SplitButtonWidget.ActivateActionId, PrimaryAction, "Activate");
            bindings.Key(Hex1bKey.Spacebar).Triggers(SplitButtonWidget.ActivateActionId);
            bindings.Mouse(MouseButton.Left).Triggers(SplitButtonWidget.ActivateActionId, HandleClick, "Click");
        }

        // Down arrow opens dropdown if there are secondary actions
        if (HasSecondaryActions)
        {
            bindings.Key(Hex1bKey.DownArrow).Triggers(SplitButtonWidget.OpenMenuActionId, OpenDropdown, "Open menu");
        }
    }

    private Task HandleClick(InputBindingActionContext ctx)
    {
        // Clicks on the secondary affordance region (divider + arrow + pads)
        // open the dropdown; everything else triggers the primary action.
        if (HasSecondaryActions && ctx.MouseX >= 0)
        {
            var arrowRegionX = Bounds.X + Bounds.Width - ArrowRegionWidth;
            if (ctx.MouseX >= arrowRegionX)
            {
                return OpenDropdown(ctx);
            }
        }

        return PrimaryAction?.Invoke(ctx) ?? Task.CompletedTask;
    }

    private Task OpenDropdown(InputBindingActionContext ctx)
    {
        if (IsDropdownOpen || SecondaryActions.Count == 0)
        {
            return Task.CompletedTask;
        }

        IsDropdownOpen = true;
        MarkDirty();

        // Invoke callback (e.g., to cancel notification timeout)
        DropdownOpenedCallback?.Invoke();

        // Build and push the dropdown menu
        ctx.Popups.PushAnchored(this, AnchorPosition.Below, BuildDropdownContent,
            focusRestoreNode: this,
            onDismiss: () =>
            {
                IsDropdownOpen = false;
                MarkDirty();
            });

        return Task.CompletedTask;
    }

    private Hex1bWidget BuildDropdownContent()
    {
        // Use a ListWidget so the popup naturally responds to UpArrow /
        // DownArrow / Enter / Escape, mirroring how PickerNode opens its
        // popup. Building this as a VStack of ButtonWidgets — as an earlier
        // implementation did — left the popup with no built-in keyboard
        // navigation, so focus would stay stuck on the first secondary
        // action.
        var labels = SecondaryActions.Select(a => a.Label).ToList();
        var list = new ListWidget(labels)
            .OnItemActivated(async e =>
            {
                // Close the dropdown — onDismiss (set by OpenDropdown) takes
                // care of clearing IsDropdownOpen and marking dirty.
                e.Context.Popups.Pop();
                IsDropdownOpen = false;
                MarkDirty();

                // Execute the matching action.
                if (SourceWidget != null && e.ActivatedIndex >= 0 && e.ActivatedIndex < SecondaryActions.Count)
                {
                    var action = SecondaryActions[e.ActivatedIndex];
                    var args = new SplitButtonClickedEventArgs(SourceWidget, this, e.Context);
                    await action.Handler(args);
                }
            });

        // Wrap the bordered list in a BackgroundPanel so every cell inside
        // the popup is opaquely painted — otherwise BorderNode's inner fill
        // writes spaces with no background ANSI (when GlobalBackground is
        // Default), letting whatever was painted underneath bleed through.
        return new BackgroundPanelWidget(
            OverlayTheme.BackgroundColor,
            new BorderWidget(list));
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Without secondary actions the chip is " Label " (Label.Length + 2).
        // With secondary actions the chip becomes " Label │ ▼ " — the
        // ArrowRegionWidth trailing cells render the divider + space + arrow
        // + trailing pad.
        var width = PrimaryLabel.Length + 2;
        if (HasSecondaryActions)
        {
            width += ArrowRegionWidth;
        }
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();

        var primarySegment = $" {PrimaryLabel} ";
        var arrowSegment = HasSecondaryActions ? $"{Divider} {DropdownArrow} " : "";

        // Resolve the colour quad for the current visual state. The arrow
        // region uses its own colour pair so the divider/arrow read as
        // distinct from the primary chip while staying inside the same
        // overall focus state.
        Hex1bColor primaryFg, primaryBg, arrowFg, arrowBg;
        if (IsFocused || IsDropdownOpen)
        {
            primaryFg = theme.Get(ButtonTheme.FocusedForegroundColor);
            primaryBg = theme.Get(ButtonTheme.FocusedBackgroundColor);
            arrowFg = theme.Get(SplitButtonTheme.FocusedArrowForegroundColor);
            arrowBg = theme.Get(SplitButtonTheme.FocusedArrowBackgroundColor);
        }
        else if (IsHovered)
        {
            primaryFg = theme.Get(ButtonTheme.HoveredForegroundColor);
            primaryBg = theme.Get(ButtonTheme.HoveredBackgroundColor);
            arrowFg = theme.Get(SplitButtonTheme.HoveredArrowForegroundColor);
            arrowBg = theme.Get(SplitButtonTheme.HoveredArrowBackgroundColor);
        }
        else
        {
            primaryFg = theme.Get(ButtonTheme.ForegroundColor);
            primaryBg = theme.Get(ButtonTheme.BackgroundColor);
            arrowFg = theme.Get(SplitButtonTheme.ArrowForegroundColor);
            arrowBg = theme.Get(SplitButtonTheme.ArrowBackgroundColor);
        }

        // Default-colour fall-through: a Default arrow colour inherits the
        // primary chip colour so users can opt out of the secondary tint and
        // get a uniform chip without re-stating both colour values.
        if (arrowFg.IsDefault) arrowFg = primaryFg;
        if (arrowBg.IsDefault) arrowBg = primaryBg;

        var primaryFgCode = primaryFg.IsDefault
            ? theme.GetGlobalForeground().ToForegroundAnsi()
            : primaryFg.ToForegroundAnsi();
        var primaryBgCode = primaryBg.IsDefault
            ? theme.GetGlobalBackground().ToBackgroundAnsi()
            : primaryBg.ToBackgroundAnsi();
        var arrowFgCode = arrowFg.IsDefault
            ? theme.GetGlobalForeground().ToForegroundAnsi()
            : arrowFg.ToForegroundAnsi();
        var arrowBgCode = arrowBg.IsDefault
            ? theme.GetGlobalBackground().ToBackgroundAnsi()
            : arrowBg.ToBackgroundAnsi();

        var output = HasSecondaryActions
            ? $"{primaryFgCode}{primaryBgCode}{primarySegment}{arrowFgCode}{arrowBgCode}{arrowSegment}{resetToGlobal}"
            : $"{primaryFgCode}{primaryBgCode}{primarySegment}{resetToGlobal}";

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}
