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
    /// Whether this button has secondary actions (shows dropdown arrow).
    /// </summary>
    private bool HasSecondaryActions => SecondaryActions.Count > 0;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Primary action on Enter/Space/Click
        if (PrimaryAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(PrimaryAction, "Activate");
            bindings.Key(Hex1bKey.Spacebar).Action(PrimaryAction, "Activate");
            bindings.Mouse(MouseButton.Left).Action(HandleClick, "Click");
        }

        // Down arrow opens dropdown if there are secondary actions
        if (HasSecondaryActions)
        {
            bindings.Key(Hex1bKey.DownArrow).Action(OpenDropdown, "Open menu");
        }
    }

    private Task HandleClick(InputBindingActionContext ctx)
    {
        // Check if click is on the dropdown arrow area
        if (HasSecondaryActions && ctx.MouseX >= 0)
        {
            var dropdownX = Bounds.X + Bounds.Width - 3; // Arrow is in last 2 chars + bracket
            if (ctx.MouseX >= dropdownX)
            {
                return OpenDropdown(ctx);
            }
        }

        // Otherwise trigger primary action
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
        // Build a simple list of buttons for secondary actions
        var items = SecondaryActions.Select(action =>
            new ButtonWidget(action.Label)
                .OnClick(async e =>
                {
                    // Close dropdown first
                    e.Context.Popups.Pop();
                    IsDropdownOpen = false;
                    MarkDirty();

                    // Execute the action
                    if (SourceWidget != null)
                    {
                        var args = new SplitButtonClickedEventArgs(SourceWidget, this, e.Context);
                        await action.Handler(args);
                    }
                }) as Hex1bWidget
        ).ToList();

        return new BorderWidget(new VStackWidget(items));
    }

    public override Size Measure(Constraints constraints)
    {
        // Renders as "[ Label ▼ ]" or "[ Label ]" if no secondary actions
        var width = PrimaryLabel.Length + 4; // brackets and spaces
        if (HasSecondaryActions)
        {
            width += 2; // space + arrow
        }
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(ButtonTheme.LeftBracket);
        var rightBracket = theme.Get(ButtonTheme.RightBracket);
        var resetToGlobal = theme.GetResetToGlobalCodes();

        var arrow = HasSecondaryActions ? $" {DropdownArrow}" : "";
        var content = $"{PrimaryLabel}{arrow}";

        string output;
        if (IsFocused || IsDropdownOpen)
        {
            var fg = theme.Get(ButtonTheme.FocusedForegroundColor);
            var bg = theme.Get(ButtonTheme.FocusedBackgroundColor);
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{content}{rightBracket}{resetToGlobal}";
        }
        else if (IsHovered)
        {
            var fg = theme.Get(ButtonTheme.HoveredForegroundColor);
            var bg = theme.Get(ButtonTheme.HoveredBackgroundColor);
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{leftBracket}{content}{rightBracket}{resetToGlobal}";
        }
        else
        {
            var fg = theme.Get(ButtonTheme.ForegroundColor);
            var bg = theme.Get(ButtonTheme.BackgroundColor);
            var fgCode = fg.IsDefault ? theme.GetGlobalForeground().ToForegroundAnsi() : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? theme.GetGlobalBackground().ToBackgroundAnsi() : bg.ToBackgroundAnsi();
            output = $"{fgCode}{bgCode}{leftBracket}{content}{rightBracket}{resetToGlobal}";
        }

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
