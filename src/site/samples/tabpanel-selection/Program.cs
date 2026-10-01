using Hex1b;

var state = new TabState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text($"Current tab: {state.SelectedTab}"),
        v.Text(""),
        v.TabPanel(tp => [
            tp.Tab("Documents", t => [
                t.Text("Your documents appear here")
            ]).Selected(state.SelectedTab == "Documents"),
            tp.Tab("Downloads", t => [
                t.Text("Your downloads appear here")
            ]).Selected(state.SelectedTab == "Downloads"),
            tp.Tab("Pictures", t => [
                t.Text("Your pictures appear here")
            ]).Selected(state.SelectedTab == "Pictures")
        ])
        .OnSelectionChanged(e => state.SelectedTab = e.SelectedTitle)
        .Selector()
        .Fill()
    ]))
    .Build();

await terminal.RunAsync();

class TabState
{
    public string SelectedTab { get; set; } = "Documents";
}
