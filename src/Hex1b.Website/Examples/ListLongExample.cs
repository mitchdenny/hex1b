using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// List Widget Documentation: Long List with Scrolling
/// Demonstrates scrollable lists with many items in a constrained height container.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the longListCode sample in:
/// src/content/guide/widgets/list.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ListLongExample(ILogger<ListLongExample> logger) : Hex1bExample
{
    private readonly ILogger<ListLongExample> _logger = logger;

    public override string Id => "list-long";
    public override string Title => "List Widget - Long List with Scrolling";
    public override string Description => "Demonstrates scrollable lists with many items";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating list long example widget builder");

        // Generate a list of 50 countries
        var countries = new List<string>
        {
            "Argentina", "Australia", "Austria", "Belgium", "Brazil",
            "Canada", "Chile", "China", "Colombia", "Czech Republic",
            "Denmark", "Egypt", "Finland", "France", "Germany",
            "Greece", "Hungary", "India", "Indonesia", "Ireland",
            "Israel", "Italy", "Japan", "Kenya", "Malaysia",
            "Mexico", "Netherlands", "New Zealand", "Nigeria", "Norway",
            "Pakistan", "Peru", "Philippines", "Poland", "Portugal",
            "Romania", "Russia", "Saudi Arabia", "Singapore", "South Africa",
            "South Korea", "Spain", "Sweden", "Switzerland", "Thailand",
            "Turkey", "Ukraine", "United Kingdom", "United States", "Vietnam"
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Select a country (scroll with arrow keys or mouse wheel):"),
                    v.Text(""),
                    v.List(countries).FixedHeight(10)
                ])
            ]).Title("Country Selector");
        };
    }
}
