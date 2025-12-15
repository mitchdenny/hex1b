# ANSI & Terminal Rendering

Hex1b renders by writing ANSI escape sequences to the terminal. This deep dive explains how terminal rendering works under the hood.

## Terminal Basics

Terminals are character grids. Each cell can have:
- A character (including Unicode/emoji)
- Foreground color
- Background color
- Attributes (bold, italic, underline, etc.)

## ANSI Escape Sequences

ANSI sequences are special character sequences that control the terminal:

```
ESC [ <parameters> <command>
 ↑  ↑       ↑           ↑
\x1b [    numbers       letter
```

### Common Sequences

| Sequence | Effect |
|----------|--------|
| `\x1b[H` | Move cursor to home (0,0) |
| `\x1b[2J` | Clear entire screen |
| `\x1b[10;5H` | Move cursor to row 10, col 5 |
| `\x1b[K` | Clear from cursor to end of line |
| `\x1b[?25l` | Hide cursor |
| `\x1b[?25h` | Show cursor |

### Colors

```
\x1b[30m - \x1b[37m   Foreground colors (black to white)
\x1b[40m - \x1b[47m   Background colors
\x1b[90m - \x1b[97m   Bright foreground colors
\x1b[100m - \x1b[107m Bright background colors

\x1b[38;5;Nm         256-color foreground (N = 0-255)
\x1b[48;5;Nm         256-color background

\x1b[38;2;R;G;Bm     24-bit foreground (RGB)
\x1b[48;2;R;G;Bm     24-bit background
```

### Attributes

```
\x1b[0m   Reset all attributes
\x1b[1m   Bold
\x1b[2m   Dim
\x1b[3m   Italic
\x1b[4m   Underline
\x1b[5m   Blink
\x1b[7m   Reverse (swap fg/bg)
\x1b[9m   Strikethrough
```

## The Render Buffer

Hex1b uses a cell-based buffer:

```csharp
public struct Cell
{
    public string Character;    // Could be multi-codepoint (emoji)
    public Hex1bColor Foreground;
    public Hex1bColor Background;
    public CellAttributes Attributes;
}

public class RenderBuffer
{
    Cell[,] _cells;
    Cell[,] _previousCells;
    
    public void Write(int x, int y, string text, Style style)
    {
        // Write characters to buffer
        for (int i = 0; i < text.Length; i++)
        {
            _cells[y, x + i] = new Cell
            {
                Character = text[i].ToString(),
                Foreground = style.Foreground,
                Background = style.Background,
                Attributes = style.Attributes
            };
        }
    }
    
    public void Flush(IHex1bTerminal terminal)
    {
        // Only write cells that changed
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_cells[y, x] != _previousCells[y, x])
                {
                    terminal.MoveTo(x, y);
                    terminal.WriteCell(_cells[y, x]);
                }
            }
        }
        
        // Swap buffers
        (_cells, _previousCells) = (_previousCells, _cells);
    }
}
```

## Unicode & Grapheme Clusters

Not all characters are one column wide:

| Character | Display Width | Codepoints |
|-----------|---------------|------------|
| `a` | 1 | 1 |
| `中` | 2 | 1 (CJK) |
| `👨‍👩‍👧` | 2 | 7 (family emoji) |
| `é` | 1 | 1 or 2 (composed or combining) |

Hex1b handles this:

```csharp
public static int GetDisplayWidth(string text)
{
    int width = 0;
    foreach (var grapheme in GraphemeCluster.Enumerate(text))
    {
        width += GetCharWidth(grapheme);
    }
    return width;
}
```

## Alternate Screen Buffer

Terminals have two buffers:
- **Main buffer**: Normal shell history
- **Alternate buffer**: Full-screen apps

Hex1b switches to alternate on startup:
```
\x1b[?1049h  Enter alternate screen
\x1b[?1049l  Exit alternate screen
```

This keeps your shell clean when the app exits.

## Sixel Graphics

Sixel is a protocol for bitmap images in terminals:

```csharp
var sixel = SixelEncoder.Encode(image, width: 200, height: 100);
terminal.Write(sixel);
```

Sixel works by encoding 6 vertical pixels per character cell:
```
Each "sixel" character represents a 1x6 pixel column
Multiple columns form an image row
Rows stack vertically
```

Supported terminals: iTerm2, WezTerm, mlterm, Contour, foot

## Mouse Input (SGR Mode)

Hex1b can receive mouse events:

```
Enable:  \x1b[?1006h  (SGR mouse mode)
Disable: \x1b[?1006l

Mouse event format:
\x1b[<Cb;Cx;CyM   Button press
\x1b[<Cb;Cx;Cym   Button release

Where:
  Cb = button (0=left, 1=middle, 2=right, 32+=motion)
  Cx = column (1-based)
  Cy = row (1-based)
```

## Clipping

Nodes are clipped to their bounds during rendering:

```csharp
public override void Render(Hex1bRenderContext ctx)
{
    ctx.PushClip(Bounds);
    
    // All writes are clipped to Bounds
    foreach (var child in Children)
    {
        child.Render(ctx);
    }
    
    ctx.PopClip();
}
```

## Performance Optimizations

### 1. Minimal ANSI Output

Only emit color codes when they change:

```csharp
if (currentForeground != cell.Foreground)
{
    Emit(ColorCode(cell.Foreground));
    currentForeground = cell.Foreground;
}
```

### 2. Cursor Optimization

Jump to changed regions rather than redrawing everything:

```csharp
// Skip unchanged cells
if (AllUnchanged(y, startX, endX))
    continue;

// Move cursor only when needed
if (y != lastY || startX != lastX + 1)
    EmitMoveCursor(startX, y);
```

### 3. Batch Writes

Buffer all output before flushing:

```csharp
var builder = new StringBuilder();
// ... build all ANSI sequences
terminal.Write(builder.ToString());  // Single syscall
```

## Related

- [Render Loop](/deep-dives/render-loop) - The complete rendering cycle
- [Theming](/guide/theming) - Using colors effectively
