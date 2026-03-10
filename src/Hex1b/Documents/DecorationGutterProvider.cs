using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// A gutter provider that renders <see cref="GutterDecoration"/> icons pushed via
/// <see cref="IEditorSession.PushGutterDecorations"/>. Displays a single-column
/// gutter showing icons for lines that have decorations.
/// </summary>
public sealed class DecorationGutterProvider : IGutterProvider
{
    private IReadOnlyList<GutterDecoration> _decorations = [];

    /// <summary>
    /// Updates the decorations to display.
    /// </summary>
    public void SetDecorations(IReadOnlyList<GutterDecoration> decorations)
    {
        _decorations = decorations;
    }

    /// <summary>
    /// Clears all decorations.
    /// </summary>
    public void Clear()
    {
        _decorations = [];
    }

    /// <inheritdoc />
    public int GetWidth(IHex1bDocument document) => _decorations.Count > 0 ? 1 : 0;

    /// <inheritdoc />
    public void RenderLine(Hex1bRenderContext context, Hex1bTheme theme, int screenX, int screenY, int docLine, int width)
    {
        if (width == 0) return;

        var bg = theme.Get(GutterTheme.BackgroundColor);
        if (bg.IsDefault) bg = theme.Get(EditorTheme.BackgroundColor);

        // Find the first decoration for this line (highest priority kind wins)
        GutterDecoration? decoration = null;
        foreach (var dec in _decorations)
        {
            if (dec.Line == docLine)
            {
                decoration = dec;
                break;
            }
        }

        if (decoration != null)
        {
            var fg = decoration.Foreground ?? ResolveKindColor(decoration.Kind, theme);
            context.WriteClipped(screenX, screenY,
                $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{decoration.Character}");
        }
        else
        {
            context.WriteClipped(screenX, screenY,
                $"{bg.ToBackgroundAnsi()} ");
        }
    }

    private static Hex1bColor ResolveKindColor(GutterDecorationKind kind, Hex1bTheme theme) => kind switch
    {
        GutterDecorationKind.Error => theme.Get(GutterDecorationTheme.ErrorIconColor),
        GutterDecorationKind.Warning => theme.Get(GutterDecorationTheme.WarningIconColor),
        GutterDecorationKind.Info => theme.Get(GutterDecorationTheme.InfoIconColor),
        _ => theme.Get(GutterDecorationTheme.DefaultIconColor)
    };
}
