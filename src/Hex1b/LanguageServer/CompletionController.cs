using Hex1b.Documents;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Theming;

namespace Hex1b.LanguageServer;

/// <summary>
/// Manages the lifecycle of an LSP completion popup: requesting items, rendering
/// the overlay, handling keyboard navigation, and inserting accepted items.
/// </summary>
internal sealed class CompletionController
{
    private const string OverlayId = "lsp-completion";
    private const int MaxVisibleItems = 10;

    private IEditorSession? _session;
    private CompletionItem[] _items = [];
    private int _selectedIndex;
    private DocumentPosition _triggerPosition;

    /// <summary>Whether the completion popup is currently showing.</summary>
    public bool IsActive => _items.Length > 0 && _session != null;

    /// <summary>Binds the controller to an editor session.</summary>
    public void Attach(IEditorSession session) => _session = session;

    /// <summary>Detaches from the current session.</summary>
    public void Detach()
    {
        Dismiss();
        _session = null;
    }

    /// <summary>
    /// Shows the completion popup with the given items at the specified position.
    /// </summary>
    public void Show(CompletionItem[] items, DocumentPosition triggerPosition)
    {
        if (items.Length == 0 || _session == null)
        {
            Dismiss();
            return;
        }

        _items = items;
        _selectedIndex = 0;
        _triggerPosition = triggerPosition;
        UpdateOverlay();
    }

    /// <summary>Moves the selection down one item.</summary>
    public void SelectNext()
    {
        if (!IsActive) return;
        _selectedIndex = (_selectedIndex + 1) % _items.Length;
        UpdateOverlay();
    }

    /// <summary>Moves the selection up one item.</summary>
    public void SelectPrev()
    {
        if (!IsActive) return;
        _selectedIndex = (_selectedIndex - 1 + _items.Length) % _items.Length;
        UpdateOverlay();
    }

    /// <summary>
    /// Accepts the currently selected item and inserts its text into the document.
    /// Returns the text that was inserted, or null if nothing was active.
    /// </summary>
    public string? Accept()
    {
        if (!IsActive || _selectedIndex >= _items.Length) return null;

        var item = _items[_selectedIndex];
        var insertText = item.InsertText ?? item.Label;

        Dismiss();
        return insertText;
    }

    /// <summary>Dismisses the completion popup without inserting anything.</summary>
    public void Dismiss()
    {
        if (_items.Length > 0)
        {
            _items = [];
            _selectedIndex = 0;
            _session?.DismissOverlay(OverlayId);
        }
    }

    private void UpdateOverlay()
    {
        if (_session == null || _items.Length == 0) return;

        // Calculate visible window (scroll if more items than max)
        var totalItems = _items.Length;
        var visibleCount = Math.Min(totalItems, MaxVisibleItems);

        // Center the selection in the visible window
        var scrollOffset = 0;
        if (totalItems > MaxVisibleItems)
        {
            scrollOffset = Math.Max(0, _selectedIndex - MaxVisibleItems / 2);
            scrollOffset = Math.Min(scrollOffset, totalItems - MaxVisibleItems);
        }

        var lines = new List<OverlayLine>(visibleCount + (totalItems > MaxVisibleItems ? 1 : 0));

        for (var i = scrollOffset; i < scrollOffset + visibleCount && i < totalItems; i++)
        {
            var item = _items[i];
            var kindIcon = GetKindIcon(item.Kind);
            var isSelected = i == _selectedIndex;

            var fg = isSelected ? Hex1bColor.FromRgb(255, 255, 255) : Hex1bColor.FromRgb(200, 200, 200);
            var bg = isSelected ? Hex1bColor.FromRgb(50, 50, 140) : Hex1bColor.FromRgb(40, 40, 40);

            var detail = item.Detail != null ? $"  {item.Detail}" : "";
            var text = $" {kindIcon} {item.Label}{detail} ";

            // Truncate long lines
            if (text.Length > 60)
                text = text[..57] + "...";

            lines.Add(new OverlayLine(text, fg, bg));
        }

        // Show scroll indicator if there are hidden items
        if (totalItems > MaxVisibleItems)
        {
            var indicator = $" {scrollOffset + 1}-{scrollOffset + visibleCount} of {totalItems} ";
            lines.Add(new OverlayLine(indicator, Hex1bColor.FromRgb(120, 120, 120), Hex1bColor.FromRgb(30, 30, 30)));
        }

        _session.PushOverlay(new EditorOverlay(
            Id: OverlayId,
            AnchorPosition: _triggerPosition,
            Placement: OverlayPlacement.Below,
            Content: lines,
            DismissOnCursorMove: false)); // We handle dismiss ourselves
    }

    /// <summary>The overlay ID used by the completion controller.</summary>
    internal static string CompletionOverlayId => OverlayId;

    private static string GetKindIcon(int? kind) => kind switch
    {
        CompletionItemKind.Method or CompletionItemKind.Function => "ƒ",
        CompletionItemKind.Variable => "𝑥",
        CompletionItemKind.Class => "C",
        CompletionItemKind.Interface => "I",
        CompletionItemKind.Property => "P",
        CompletionItemKind.Field => "F",
        CompletionItemKind.Keyword => "K",
        CompletionItemKind.Snippet => "S",
        CompletionItemKind.Module => "M",
        CompletionItemKind.Enum => "E",
        CompletionItemKind.Constant => "#",
        CompletionItemKind.TypeParameter => "T",
        _ => "·",
    };
}
