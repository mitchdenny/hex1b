using Hex1b;

await using var terminal = Hex1b.Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.Grid(g =>
        {
            g.Columns.Add(Hex1b.Layout.SizeHint.Fixed(25));
            g.Columns.Add(Hex1b.Layout.SizeHint.Fill);

            g.Rows.Add(Hex1b.Layout.SizeHint.Fixed(3));
            g.Rows.Add(Hex1b.Layout.SizeHint.Fill);
            g.Rows.Add(Hex1b.Layout.SizeHint.Fixed(1));

            return [
                // Sidebar spans all 3 rows
                g.Cell(c => c.Border(b => [
                    b.VStack(v => [
                        v.Text("📂 Files"),
                        v.Text("📊 Dashboard"),
                        v.Text("⚙️ Settings"),
                        v.Text(""),
                        v.Text("Ctrl+C to exit"),
                    ])
                ]).Title("Navigation")).RowSpan(0, 3).Column(0),

                // Header bar
                g.Cell(c => c.Border(b => [
                    b.Text("Grid Layout Demo — Sidebar Layout"),
                ]).Title("Header")).Row(0).Column(1),

                // Main content area
                g.Cell(c => c.Border(b => [
                    b.VStack(v => [
                        v.Text("This is the main content area."),
                        v.Text("It fills the remaining space."),
                        v.Text(""),
                        v.Text("The grid has:"),
                        v.Text("  • 2 columns (Fixed 25, Fill)"),
                        v.Text("  • 3 rows (Fixed 3, Fill, Fixed 1)"),
                        v.Text("  • Navigation spans all 3 rows"),
                    ])
                ]).Title("Content")).Row(1).Column(1),

                // Status bar
                g.Cell(c => c.Text(" Status: Ready")).Row(2).Column(1),
            ];
        });
    })
    .Build();

await terminal.RunAsync();
