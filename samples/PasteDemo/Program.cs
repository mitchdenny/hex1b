using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// Track paste state
int pasteCount = 0;
long totalCharsReceived = 0;
string lastPastePreview = "(no paste yet)";
string textBoxContent = "";
string customPasteResult = "";
var pasteLines = new List<string>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.Text("Bracketed Paste Demo"),
            v.Separator(),

            // Stats section
            v.Text($"  Paste count:  {pasteCount}"),
            v.Text($"  Total chars:  {totalCharsReceived:N0}"),
            v.Text($"  Last preview: {lastPastePreview}"),
            v.Text(""),

            // TextBox with default paste behavior
            v.Text("TextBox (default paste - inserts at cursor):"),
            v.TextBox(textBoxContent)
                .OnTextChanged(e => { textBoxContent = e.NewText; }),
            v.Text(""),

            // TextBox with custom OnPaste handler
            v.Text("TextBox (custom OnPaste - uppercases):"),
            v.TextBox(customPasteResult)
                .OnPaste(async paste =>
                {
                    var text = await paste.ReadToEndAsync();
                    customPasteResult = text.ToUpperInvariant();
                    app.Invalidate();
                })
                .OnTextChanged(e => { customPasteResult = e.NewText; }),
            v.Text(""),

            // Pastable container with streaming handler
            v.Text("Pastable zone (streams lines):"),
            v.Pastable(v2 => [
                v2.Border(v3 => [
                    .. pasteLines.Count > 0
                        ? pasteLines.TakeLast(5).Select(l => v3.Text($"  {l}")).ToArray()
                        : [v3.Text("  (paste multi-line text here)")],
                ])
            ])
            .OnPaste(async paste =>
            {
                pasteCount++;
                pasteLines.Clear();
                await foreach (var line in paste.ReadLinesAsync())
                {
                    totalCharsReceived += line.Length;
                    pasteLines.Add(line);
                    lastPastePreview = line.Length > 40 ? line[..40] + "..." : line;
                    app.Invalidate();
                }
            })
            .WithMaxSize(1_000_000),

            v.Text(""),
            v.Separator(),
            v.Text("Press Ctrl+C or q to exit."),
        ]);
    })
    .Build();

await terminal.RunAsync();
