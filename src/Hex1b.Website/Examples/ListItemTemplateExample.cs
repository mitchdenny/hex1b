using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// TypedList / ItemTemplate Documentation: Custom row templates
/// Demonstrates a Spectre-style selection list where each row is rendered as a
/// custom multi-line widget tree via ItemTemplate.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the templateCode sample in:
/// src/content/guide/widgets/list.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ListItemTemplateExample(ILogger<ListItemTemplateExample> logger) : Hex1bExample
{
    private readonly ILogger<ListItemTemplateExample> _logger = logger;

    public override string Id => "list-item-template";
    public override string Title => "List Widget - Item Template";
    public override string Description => "Renders each row with a custom widget tree using TypedList + ItemTemplate";

    private sealed record Country(string Name, string Capital, string Flag);

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating list item-template example widget builder");

        var countries = new[]
        {
            new Country("Australia", "Canberra", "🇦🇺"),
            new Country("Brazil", "Brasilia", "🇧🇷"),
            new Country("Japan", "Tokyo", "🇯🇵"),
            new Country("Norway", "Oslo", "🇳🇴"),
            new Country("Portugal", "Lisbon", "🇵🇹"),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.TypedList(countries)
                    .ItemHeight(2)
                    .ItemKey(c => c.Name)
                    .ItemTemplate(context =>
                    {
                        var prefix = context.IsSelected ? "▶ " : "  ";
                        return context.VStack(v => [
                            v.Text($"{prefix}{context.Item.Flag}  {context.Item.Name}"),
                            v.Text($"     {context.Item.Capital}")
                        ]);
                    })
            ]).Title("Pick a Country");
        };
    }
}
