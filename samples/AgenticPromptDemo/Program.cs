// AgenticPromptDemo
//
// Skeleton for an agentic-style chat UI:
//
//   ┌──────────────────────────────┬──────────────────┐
//   │ VScrollPanel                 │ Editor           │
//   │   └─ SelectionPanel          │ (read-only;      │
//   │       └─ VStack of entries   │  shows snapshot  │
//   ├──────────────────────────────┤  / copied text)  │
//   │ TextBox  (pinned bottom)     │                  │
//   └──────────────────────────────┴──────────────────┘
//
// The whole transcript content is wrapped in a single SelectionPanel. As a
// proof-of-concept of the eventual copy-mode flow, pressing Ctrl+Shift+S
// snapshots the text inside the panel and replaces the right-pane editor's
// document with it. Everything else is plain pass-through so far.
//
// Run with: dotnet run --project samples/AgenticPromptDemo

using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

var transcript = new List<TranscriptEntry>
{
    new(EntryRole.System, "Type a message below and press Enter to add it to the transcript. F12 snapshots the panel content into the editor on the right. Ctrl+Q quits."),
};

// Read-only editor on the right shows the most recent SelectionPanel snapshot.
var clipboardDoc = new Hex1bDocument(
    "(Press F12 to snapshot the panel on the left into this editor.)");
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
                            .OnSnapshot(text =>
                            {
                                var range = new DocumentRange(
                                    new DocumentOffset(0),
                                    new DocumentOffset(clipboardDoc.Length));
                                clipboardDoc.Apply(new ReplaceOperation(range, text));
                                clipboardEditorState.ClampAllCursors();
                                app.Invalidate();
                            })
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

                // RIGHT — read-only editor for inspecting copied / snapshotted text.
                v.Border(
                    v.Editor(clipboardEditorState).Fill()
                ).Title("Copied text"),

                leftWidth: 60
            ).Fill(),

            // Bottom info bar — hints + transcript counter.
            v.InfoBar(s =>
            [
                s.Section("AgenticPromptDemo"),
                s.Section("Enter: Send"),
                s.Section("Tab/Shift+Tab: Focus"),
                s.Section("F12: Snapshot"),
                s.Section("Ctrl+Q: Quit"),
                s.Spacer(),
                s.Section($"{transcript.Count} entr{(transcript.Count == 1 ? "y" : "ies")}"),
            ]).Divider(" | "),
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
