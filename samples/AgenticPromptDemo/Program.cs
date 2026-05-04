// AgenticPromptDemo
//
// Skeleton for an agentic-style chat UI:
//
//   ┌──────────────────────────────────────────┐
//   │ VScrollPanel                             │
//   │   └─ SelectionPanel  (pass-through today)│
//   │       └─ VStack of transcript entries    │
//   └──────────────────────────────────────────┘
//   ┌──────────────────────────────────────────┐
//   │ TextBox (pinned at the bottom)           │
//   └──────────────────────────────────────────┘
//
// The whole transcript content is wrapped in a single SelectionPanel so that
// future iterations can give the user a copy/select mode that snapshots all
// rendered cells inside the scroll viewport. Today the SelectionPanel is a
// pure pass-through wrapper — it has no behaviour of its own.
//
// Run with: dotnet run --project samples/AgenticPromptDemo

using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

var transcript = new List<TranscriptEntry>
{
    new(EntryRole.System, "Type a message below and press Enter to add it to the transcript. Ctrl+Q quits."),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v =>
        [
            v.VScrollPanel(sv =>
            [
                // ScrollPanel -> SelectionPanel -> Content
                sv.SelectionPanel(
                    sv.VStack(inner =>
                        transcript.Select(entry => RenderEntry(inner, entry)).ToArray()))
            ], showScrollbar: true)
            .Follow()
            .Fill(),

            v.Separator(),

            v.TextBox()
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
