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
// The whole transcript content is wrapped in a single SelectionPanel.
// Press F12 to enter copy mode — a cursor appears (inverted cell). Move
// it with arrows / hjkl / PageUp/Down / Home/End / G / Shift+G; press V
// (character), Shift+V (line) or Alt+V (block) to start a selection;
// Y or Enter copies the highlighted text into the editor on the right;
// Esc or Q cancels.
//
// Mouse: left-drag inside the transcript to select. Hold Ctrl for line
// (Shift also works in terminals that pass Shift through — Windows
// Terminal, GNOME Terminal etc. consume Shift+drag for OS-level native
// selection so Ctrl is the cross-platform reliable modifier). Hold Alt
// for block. Releasing keeps copy mode active so you can refine with
// the keyboard then press Y / Enter to commit.
//
// Each assistant reply also gets thumbs-up / thumbs-down / copy buttons
// so you can verify mouse-click routing through SelectionPanel +
// ScrollPanel + HSplitter still works for normal interactables. Type
// "/picker" for a reply with a row of action buttons that append
// follow-up entries when clicked.
//
// Run with: dotnet run --project samples/AgenticPromptDemo

using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

var transcript = new List<TranscriptEntry>
{
    new(EntryRole.System,
        "Type a message below and press Enter to add it to the transcript. " +
        "Try \"/picker\" for a reply with action buttons. " +
        "Press F12 to enter copy mode: arrows or hjkl move the cursor, V/Shift+V/Alt+V " +
        "starts a character/line/block selection, Y or Enter copies into the editor on the " +
        "right, Esc cancels. Ctrl+Q quits."),
};

// Read-only editor on the right shows the most recent SelectionPanel copy
// (or the text from a per-entry Copy button).
var clipboardDoc = new Hex1bDocument(
    "(Press F12 to enter copy mode, then V/Shift+V/Alt+V to select and Y to copy. The text appears here.)");
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
                                transcript.Select(entry => RenderEntry(inner, entry, app, CopyToClipboard)).ToArray()))
                            .OnCopy(CopyToClipboard)
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

                            HandleSubmittedText(text, transcript);
                            e.Node.Text = "";
                        }),
                ]),

                // RIGHT — read-only editor for inspecting copied text.
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
                s.Section("F12 / Drag: Copy mode"),
                s.Section("Ctrl+Drag: Line"),
                s.Section("Alt+Drag: Block"),
                s.Section("V/⇧V/⌥V: Select"),
                s.Section("Y: Copy"),
                s.Section("Esc: Cancel"),
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

// Append clipped text to the read-only editor on the right.
void CopyToClipboard(string text)
{
    var range = new DocumentRange(
        new DocumentOffset(0),
        new DocumentOffset(clipboardDoc.Length));
    clipboardDoc.Apply(new ReplaceOperation(range, text));
    clipboardEditorState.ClampAllCursors();
}

// Slash-commands let us inject richer assistant replies that exercise
// interactable widgets nested inside SelectionPanel + ScrollPanel +
// HSplitter — useful for verifying mouse routing isn't broken.
static void HandleSubmittedText(string text, List<TranscriptEntry> transcript)
{
    transcript.Add(new TranscriptEntry(EntryRole.User, text));

    if (text.Equals("/picker", StringComparison.OrdinalIgnoreCase))
    {
        transcript.Add(new TranscriptEntry(
            EntryRole.Assistant,
            "Pick a follow-up — clicking a button appends a new user entry:",
            actions:
            [
                new ActionButton("Tell me more",
                    () => transcript.Add(new TranscriptEntry(EntryRole.User, "Tell me more about that."))),
                new ActionButton("Summarize",
                    () => transcript.Add(new TranscriptEntry(EntryRole.User, "Summarize that for me."))),
                new ActionButton("Cite sources",
                    () => transcript.Add(new TranscriptEntry(EntryRole.User, "Can you cite sources?"))),
            ]));
        return;
    }

    transcript.Add(new TranscriptEntry(
        EntryRole.Assistant,
        $"(echo) You said: {text}"));
}

static Hex1bWidget RenderEntry(
    WidgetContext<VStackWidget> ctx,
    TranscriptEntry entry,
    Hex1bApp app,
    Action<string> copyToClipboard)
{
    var title = entry.Role switch
    {
        EntryRole.User      => "You",
        EntryRole.Assistant => "Assistant",
        EntryRole.System    => "System",
        _ => "?"
    };

    return ctx.Border(
        ctx.VStack(stack =>
        {
            var rows = new List<Hex1bWidget>
            {
                stack.Markdown(entry.Text).FillWidth(),
            };

            // Action buttons declared by the entry (e.g. /picker reply).
            if (entry.Actions.Count > 0)
            {
                rows.Add(stack.HStack(actions =>
                    entry.Actions.Select(action =>
                        (Hex1bWidget)actions.Button(action.Label).OnClick(_ =>
                        {
                            action.OnClick();
                            app.Invalidate();
                        })).ToArray()));
            }

            // Per-assistant interactable footer: thumbs-up / thumbs-down /
            // copy-this-entry. These exist so we can verify a normal
            // mouse click on a button nested inside SelectionPanel +
            // ScrollPanel + HSplitter still routes correctly.
            if (entry.Role == EntryRole.Assistant)
            {
                rows.Add(stack.HStack(footer =>
                [
                    footer.Button($"👍 {entry.Likes}").OnClick(_ =>
                    {
                        entry.Likes++;
                        app.Invalidate();
                    }),
                    footer.Button($"👎 {entry.Dislikes}").OnClick(_ =>
                    {
                        entry.Dislikes++;
                        app.Invalidate();
                    }),
                    footer.Button("Copy text").OnClick(_ =>
                    {
                        copyToClipboard(entry.Text);
                        app.Invalidate();
                    }),
                ]));
            }

            return rows.ToArray();
        }).FillWidth()
    ).Title(title);
}

internal enum EntryRole
{
    User,
    Assistant,
    System,
}

internal sealed class TranscriptEntry
{
    public EntryRole Role { get; }
    public string Text { get; }
    public IReadOnlyList<ActionButton> Actions { get; }
    public int Likes { get; set; }
    public int Dislikes { get; set; }

    public TranscriptEntry(EntryRole role, string text, IReadOnlyList<ActionButton>? actions = null)
    {
        Role = role;
        Text = text;
        Actions = actions ?? Array.Empty<ActionButton>();
    }
}

internal sealed record ActionButton(string Label, Action OnClick);
