#pragma warning disable HEX1B_SIXEL // This sample demonstrates the experimental Sixel widget.

using Hex1b;
using Hex1b.Layout;
using HwtRecordingSample;

var state = new GalleryState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(context => context.VStack(root =>
    [
        root.Text("HEX1B / PIXEL POSTCARDS"),
        root.Text("Two terminal graphics protocols, one ordinary interactive .NET app."),
        root.Text(""),
        root.Text($"Count: {state.Count}    Scene: {state.SceneName}"),
        root.HStack(row =>
        [
            row.Button("Next postcard").OnClick(_ => state.Next()),
            row.Button("Quit").OnClick(e => e.Context.RequestStop())
        ]).ContentHeight(),
        root.Text(""),
        root.HStack(row =>
        [
            row.Border(
                    row.KgpImage(state.Landscape, GalleryArtwork.Width, GalleryArtwork.Height,
                            fallback => fallback.Text("KGP unavailable in this terminal."))
                        .Width(32).Height(9))
                .Title("KGP / mountain light"),
            row.Border(
                    row.Sixel(state.Waves,
                            fallback => fallback.Text("Sixel unavailable in this terminal."))
                        .Width(32).Height(9))
                .Title("Sixel / ocean currents")
        ]).ContentHeight(),
        root.Text(""),
        root.Text("Tab selects a button; Enter activates it. Ctrl+C also exits."),
        root.Text("Images are generated locally; no image files or network needed.")
    ]))
    .Build();

await terminal.RunAsync();
