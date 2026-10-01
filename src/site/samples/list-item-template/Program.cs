using Hex1b;

var countries = new[]
{
    new Country("Australia", "Canberra", "🇦🇺"),
    new Country("Brazil", "Brasilia", "🇧🇷"),
    new Country("Japan", "Tokyo", "🇯🇵"),
    new Country("Norway", "Oslo", "🇳🇴"),
    new Country("Portugal", "Lisbon", "🇵🇹"),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.List(countries)
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
    ]).Title("Pick a Country"))
    .Build();

await terminal.RunAsync();

record Country(string Name, string Capital, string Flag);
