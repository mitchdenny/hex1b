// AgenticPromptDemo
//
// Skeleton for an agentic-style chat UI:
//
//   ┌──────────────────────────────┬──────────────────┐
//   │ VScrollPanel                 │ Editor           │
//   │   └─ SelectionPanel          │ (read-only;      │
//   │       └─ VStack of entries   │  shows copied    │
//   ├──────────────────────────────┤  selections)     │
//   │ TextBox  (pinned bottom)     │                  │
//   └──────────────────────────────┴──────────────────┘
//
// The whole transcript content is wrapped in a single SelectionPanel so that
// future iterations can give the user a copy/select mode that snapshots all
// rendered cells inside the scroll viewport. Today the SelectionPanel is a
// pure pass-through wrapper — it has no behaviour of its own.
//
// The right-hand pane is a read-only Editor that will eventually show the
// text the user copies out of the SelectionPanel. Until copy mode is
// implemented it just displays a placeholder message.
//
// Run with: dotnet run --project samples/AgenticPromptDemo

using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

var transcript = new List<TranscriptEntry>
{
    new(EntryRole.System, "Type a message below and press Enter to add it to the transcript. Ctrl+Q quits."),
};

// Read-only editor on the right shows whatever the user copies from the
// SelectionPanel. No copy plumbing exists yet — when SelectionPanel grows a
// TextCopied event (or similar), wire it into this document like
// TerminalSelectionDemo does:
//
//     panel.TextCopied += text =>
//     {
//         var range = new DocumentRange(new DocumentOffset(0),
//                                       new DocumentOffset(clipboardDoc.Length));
//         clipboardDoc.Apply(new ReplaceOperation(range, text));
//         clipboardEditorState.ClampAllCursors();
//         app?.Invalidate();
//     };
//
// TODO: wire SelectionPanel.TextCopied here
var clipboardDoc = new Hex1bDocument(
    "(Copied selections will appear here once SelectionPanel copy mode is implemented.)");
var clipboardEditorState = new EditorState(clipboardDoc) { IsReadOnly = true };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v =>
        [
            v.HSplitter(
                // LEFT — existing demo content: scrollable transcript + prompt.
                v.VStack(left =>
                [
                    left.VScrollPanel(sv =>
                    [
                        // ScrollPanel -> SelectionPanel -> Content
                        sv.SelectionPanel(
                            sv.VStack(inner =>
                                transcript.Select(entry => RenderEntry(inner, entry)).ToArray()))
                    ], showScrollbar: true)
                    .Follow()
                    .Fill(),

                    left.Separator(),

                    left.TextBox()
                        .OnSubmit(e =>
                        {
                            var text = e.Text?.Trim();
                            if (string.IsNullOrEmpty(text))
                            {
                                return;
                            }

                            transcript.Add(new TranscriptEntry(EntryRole.User, text));
                            transcript.Add(new TranscriptEntry(
                                EntryRole.Assistant,
                                $"(echo) You said: {text}"));
                            e.Node.Text = "";
                        }),
                ]),

                // RIGHT — read-only editor for inspecting copied text.
                v.Border(
                    v.Editor(clipboardEditorState).Fill()
                ).Title("Copied text"),

                leftWidth: 60
            ).Fill(),
        ])
        .WithInputBindings(b =>
        {
            b.Ctrl().Key(Hex1bKey.Q).Action(c => { c.RequestStop(); return Task.CompletedTask; }, "Quit");
        });
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

static Hex1bWidget RenderEntry(WidgetContext<VStackWidget> ctx, TranscriptEntry entry)
{
    var title = entry.Role switch
    {
        EntryRole.User      => "You",
        EntryRole.Assistant => "Assistant",
        EntryRole.System    => "System",
        _ => "?"
    };

    return ctx.Border(
        ctx.Markdown(entry.Text).FillWidth()
    ).Title(title);
}

internal enum EntryRole
{
    User,
    Assistant,
    System,
}

internal sealed record TranscriptEntry(EntryRole Role, string Text);
