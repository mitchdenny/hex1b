using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Example demonstrating multiple DragBarPanels in an IDE-style layout.
/// </summary>
public class DragBarMultiPanelExample(ILogger<DragBarMultiPanelExample> logger) : Hex1bExample
{
    private readonly ILogger<DragBarMultiPanelExample> _logger = logger;

    public override string Id => "dragbar-multi-panel";
    public override string Title => "DragBarPanel - Multi-Panel Layout";
    public override string Description => "An IDE-style layout with multiple independently resizable DragBarPanels.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating multi-panel DragBarPanel example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(outer => [
                outer.HStack(main => [
                    // Left panel — handle auto-detected on right edge
                    main.DragBarPanel(
                        main.VStack(panel => [
                            panel.Text(" Explorer"),
                            panel.Text(" ────────"),
                            panel.Text(" 📁 src/"),
                            panel.Text("   📄 main.cs"),
                            panel.Text("   📄 app.cs"),
                            panel.Text(" 📁 tests/")
                        ])
                    )
                    .InitialSize(25)
                    .MinSize(15)
                    .MaxSize(40),

                    // Center content fills remaining space
                    main.Border(
                        main.VStack(center => [
                            center.Text("  Editor"),
                            center.Text(""),
                            center.Text("  Both sidebars are independently").Wrap(),
                            center.Text("  resizable. The center fills the").Wrap(),
                            center.Text("  remaining space.").Wrap()
                        ]),
                        title: "main.cs"
                    ).Fill(),

                    // Right panel — handle auto-detected on left edge
                    main.DragBarPanel(
                        main.VStack(panel => [
                            panel.Text(" Properties"),
                            panel.Text(" ──────────"),
                            panel.Text(" Name: main.cs"),
                            panel.Text(" Size: 2.4 KB"),
                            panel.Text(" Modified: Today")
                        ])
                    )
                    .InitialSize(22)
                    .MinSize(12)
                    .MaxSize(35)
                ]).Fill(),

                // Bottom panel — handle auto-detected on top edge
                outer.DragBarPanel(
                    outer.VStack(panel => [
                        panel.Text(" Terminal"),
                        panel.Text(" $ dotnet build"),
                        panel.Text(" Build succeeded."),
                        panel.Text(" 0 Warning(s), 0 Error(s)")
                    ])
                )
                .InitialSize(7)
                .MinSize(4)
                .MaxSize(15)
            ]);
        };
    }
}
