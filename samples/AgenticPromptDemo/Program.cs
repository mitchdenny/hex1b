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
// Right-click also commits a copy. Esc or Q cancels.
//
// Mouse: left-drag inside the transcript to select. Hold Ctrl for line
// (Shift also works in terminals that pass Shift through — Windows
// Terminal, GNOME Terminal etc. consume Shift+drag for OS-level native
// selection so Ctrl is the cross-platform reliable modifier). Hold Alt
// for block. Releasing keeps copy mode active so you can refine with
// the keyboard then press Y / Enter / right-click to commit.
//
// Each assistant reply also gets thumbs-up / thumbs-down / copy buttons
// so you can verify mouse-click routing through SelectionPanel +
// ScrollPanel + HSplitter still works for normal interactables. Type
// "/picker" for a reply with a row of action buttons that append
// follow-up entries when clicked.
//
// The OnCopy handler receives a SelectionPanelCopyEventArgs payload
// — text plus geometry plus a per-node breakdown — so the InfoBar can
// show how many entries were touched by the last copy and how many of
// those were fully (vs partially) selected. The right-hand "Selection
// nodes" debug panel underneath the editor renders the per-node
// breakdown as a Tree (parented by closest selected ancestor) with a
// [FULL] / [PART] tag on each row, so you can see both the structural
// nesting and the partial-vs-full status at a glance.
//
// Run with: dotnet run --project samples/AgenticPromptDemo

using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

var transcript = new List<TranscriptEntry>
{
    new(EntryRole.System,
        "Type a message below and press Enter to add it to the transcript. " +
        "Try \"/picker\" for a reply with action buttons. " +
        "Press F12 to enter copy mode: arrows or hjkl move the cursor, V/Shift+V/Alt+V " +
        "starts a character/line/block selection, Y / Enter / right-click copies into the editor on the " +
        "right, Esc cancels. Ctrl+Q quits."),
};

// Read-only editor on the right shows the most recent SelectionPanel copy
// (or the text from a per-entry Copy button).
var clipboardDoc = new Hex1bDocument(
    "(Press F12 or drag to enter copy mode, then V/Shift+V/Alt+V to select. Y / Enter / right-click commits the copy. The text appears here.)");
var clipboardEditorState = new EditorState(clipboardDoc) { IsReadOnly = true };

// Last-copy summary populated by the OnCopy callback below — surfaced
// in the InfoBar to demonstrate the richer SelectionPanelCopyEventArgs.
string lastCopySummary = "no copies yet";

// Per-node breakdown from the most recent OnCopy event — rendered in
// the right-hand debug panel under the editor so we can eyeball which
// widgets the SelectionPanel reported as part of the selection.
IReadOnlyList<SelectionPanelSelectedNode> lastSelectedNodes = Array.Empty<SelectionPanelSelectedNode>();

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
                            .OnCopy(args =>
                            {
                                CopyToClipboard(args.Text);
                                lastCopySummary = SummarizeCopy(args);
                                lastSelectedNodes = args.Nodes;
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

                            HandleSubmittedText(text, transcript);
                            e.Node.Text = "";
                        }),
                ]),

                // RIGHT — read-only editor for inspecting copied text on
                // top, plus a per-node debug Tree underneath that mirrors
                // the structural relationships among the
                // SelectionPanelSelectedNode entries from the most recent
                // OnCopy event.
                v.VSplitter(
                    v.Border(
                        v.Editor(clipboardEditorState).Fill()
                    ).Title("Copied text"),
                    v.Border(
                        BuildSelectionDebugTree(v, lastSelectedNodes).Fill()
                    ).Title($"Selection nodes ({lastSelectedNodes.Count})"),
                    topHeight: 12
                ),

                leftWidth: 60
            ).Fill(),

            // Bottom info bar — hints + transcript counter + last-copy summary.
            v.InfoBar(s =>
            [
                s.Section("AgenticPromptDemo"),
                s.Section("Enter: Send"),
                s.Section("F12 / Drag: Copy mode"),
                s.Section("Ctrl+Drag: Line"),
                s.Section("Alt+Drag: Block"),
                s.Section("V/⇧V/⌥V: Select"),
                s.Section("Y / RClick: Copy"),
                s.Section("Esc: Cancel"),
                s.Section("Ctrl+Q: Quit"),
                s.Spacer(),
                s.Section($"last copy: {lastCopySummary}"),
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

// Human-readable summary of the rich SelectionPanelCopyEventArgs payload
// to surface in the InfoBar — focuses on the per-node breakdown to show
// the API delivers more than just text.
static string SummarizeCopy(SelectionPanelCopyEventArgs args)
{
    int chars = args.Text.Length;

    // Filter to BorderNodes — those are the per-entry frames in the
    // transcript so they make for a meaningful "entries touched" count.
    int borderTotal = 0;
    int borderFull = 0;
    foreach (var entry in args.Nodes)
    {
        if (entry.Node is BorderNode)
        {
            borderTotal++;
            if (entry.IsFullySelected) borderFull++;
        }
    }

    var modeLabel = args.Mode switch
    {
        SelectionMode.Line => "line",
        SelectionMode.Block => "block",
        _ => "char",
    };

    return $"{chars}ch {modeLabel} • {borderTotal} entr{(borderTotal == 1 ? "y" : "ies")} ({borderFull} full)";
}

// Build a Tree showing the structural relationships among the selected
// nodes. Each entry's parent in the tree is its closest ancestor that
// also appears in the selection (computed by walking
// Hex1bNode.Parent), so selected leaves nest under their nearest
// selected container — making it easy to see, e.g., which TextBlocks
// belong to which BorderNode entry of the transcript.
static Hex1bWidget BuildSelectionDebugTree(
    WidgetContext<VStackWidget> ctx,
    IReadOnlyList<SelectionPanelSelectedNode> nodes)
{
    if (nodes.Count == 0)
    {
        return new TextBlockWidget("(no selection captured yet — press F12 then V/Y to copy)");
    }

    // Group selected entries by their closest selected ancestor so we
    // can render a tree whose edges mirror the live widget hierarchy
    // restricted to the selection.
    var selectedSet = new HashSet<Hex1bNode>(nodes.Select(n => n.Node));
    var roots = new List<SelectionPanelSelectedNode>();
    var children = new Dictionary<Hex1bNode, List<SelectionPanelSelectedNode>>();
    foreach (var entry in nodes)
    {
        var ancestor = entry.Node.Parent;
        while (ancestor is not null && !selectedSet.Contains(ancestor))
        {
            ancestor = ancestor.Parent;
        }
        if (ancestor is null)
        {
            roots.Add(entry);
        }
        else
        {
            if (!children.TryGetValue(ancestor, out var list))
            {
                children[ancestor] = list = new List<SelectionPanelSelectedNode>();
            }
            list.Add(entry);
        }
    }

    return ctx.Tree(
        roots,
        labelSelector: FormatNodeLabel,
        childrenSelector: n => children.TryGetValue(n.Node, out var c)
            ? c
            : Array.Empty<SelectionPanelSelectedNode>(),
        isExpandedSelector: _ => true);
}

// One-line summary of a SelectionPanelSelectedNode for the debug tree:
// "[FULL] BorderNode @ [x=0, y=0, w=58, h=4]" or
// "[PART] TextBlockNode @ [x=0, y=0, w=56, h=1]". Coordinates are
// widget-relative (node-local — (0, 0) is the top-left of the node)
// rather than absolute terminal coordinates so the rect describes
// "what part of THIS widget is selected" instead of where on the
// screen the selection happens to land.
static string FormatNodeLabel(SelectionPanelSelectedNode entry)
{
    var status = entry.IsFullySelected ? "[FULL]" : "[PART]";
    var typeName = entry.Node.GetType().Name;
    var r = entry.IntersectionInNode;
    return $"{status} {typeName} @ [x={r.X}, y={r.Y}, w={r.Width}, h={r.Height}]";
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
