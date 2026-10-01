using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var fruitBasket = new List<string>();
var vegBasket = new List<string>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text(" Type-Safe Drop Targets"),
        v.Separator(),

        // Draggable items
        v.HStack(h => [
            h.Draggable(new Fruit("Apple"), dc => dc.Text(" 🍎 Apple")),
            h.Text("  "),
            h.Draggable(new Fruit("Banana"), dc => dc.Text(" 🍌 Banana")),
            h.Text("  "),
            h.Draggable(new Vegetable("Carrot"), dc => dc.Text(" 🥕 Carrot")),
        ]),
        v.Text(""),

        v.HStack(h => [
            // Only accepts Fruit
            h.Droppable(dc => dc.Border(b => [
                b.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor,
                        dc.IsHoveredByDrag
                            ? (dc.CanAcceptDrag ? Hex1bColor.Green : Hex1bColor.Red)
                            : Hex1bColor.White),
                    b.Text(dc.IsHoveredByDrag && !dc.CanAcceptDrag
                        ? " ✗ Fruits only!"
                        : $" Fruit Basket ({fruitBasket.Count})")),
            ]))
            .Accept(data => data is Fruit)
            .OnDrop(e => { if (e.DragData is Fruit f) fruitBasket.Add(f.Name); })
            .Fill(),

            // Only accepts Vegetable
            h.Droppable(dc => dc.Border(b => [
                b.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor,
                        dc.IsHoveredByDrag
                            ? (dc.CanAcceptDrag ? Hex1bColor.Green : Hex1bColor.Red)
                            : Hex1bColor.White),
                    b.Text(dc.IsHoveredByDrag && !dc.CanAcceptDrag
                        ? " ✗ Veggies only!"
                        : $" Veggie Basket ({vegBasket.Count})")),
            ]))
            .Accept(data => data is Vegetable)
            .OnDrop(e => { if (e.DragData is Vegetable v2) vegBasket.Add(v2.Name); })
            .Fill(),
        ]).Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();

record Fruit(string Name);
record Vegetable(string Name);
