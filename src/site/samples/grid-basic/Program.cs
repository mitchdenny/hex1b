using Hex1b;
using Hex1b.Layout;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx =>
        ctx.Grid(g =>
        {
            g.Columns.Add(SizeHint.Fixed(22));
            g.Columns.Add(SizeHint.Fill);

            g.Rows.Add(SizeHint.Fixed(3));
            g.Rows.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));

            return [
                g.Cell(c => c.Border(b => [
                    b.VStack(v => [
                        v.Text("📂 Files"),
                        v.Text("📊 Dashboard"),
                        v.Text("⚙️ Settings"),
                    ])
                ]).Title("Navigation")).RowSpan(0, 3).Column(0),

                g.Cell(c => c.Border(b => [
                    b.Text("Grid Layout Demo"),
                ]).Title("Header")).Row(0).Column(1),

                g.Cell(c => c.Border(b => [
                    b.Text("Main content area")
                ]).Title("Content")).Row(1).Column(1),

                g.Cell(c => c.Text(" Status: Ready")).Row(2).Column(1),
            ];
        }))
    .Build();

await terminal.RunAsync();
