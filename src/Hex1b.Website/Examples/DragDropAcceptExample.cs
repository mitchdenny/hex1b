using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drag &amp; Drop Documentation: Accept Predicates
/// Demonstrates type-safe drop filtering with visual rejection feedback.
/// </summary>
public class DragDropAcceptExample(ILogger<DragDropAcceptExample> logger) : Hex1bExample
{
    private readonly ILogger<DragDropAcceptExample> _logger = logger;

    public override string Id => "drag-drop-accept";
    public override string Title => "Drag & Drop - Accept Predicates";
    public override string Description => "Demonstrates type-safe drop filtering with visual rejection feedback";

    private record Fruit(string Name);
    private record Vegetable(string Name);

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drag-drop accept example widget builder");

        var fruitBasket = new List<string>();
        var vegBasket = new List<string>();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
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
                    .OnDrop(e => { if (e.DragData is Vegetable veg) vegBasket.Add(veg.Name); })
                    .Fill(),
                ]).Fill(),
            ]);
        };
    }
}
