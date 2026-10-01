using Hex1b;
using Hex1b.Theming;

// Customize progress bar appearance via theme
var theme = new Hex1bTheme("Custom")
    .Set(ProgressTheme.FilledCharacter, '▓')
    .Set(ProgressTheme.EmptyCharacter, '░')
    .Set(ProgressTheme.FilledForegroundColor, Hex1bColor.Blue);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(
        options => options.Theme = theme,
        ctx => ctx.Progress(60))
    .Build();

await terminal.RunAsync();
