// Customize progress bar appearance via theme
var theme = new Hex1bThemeBuilder()
    .Set(ProgressTheme.FilledCharacter, '▓')
    .Set(ProgressTheme.EmptyCharacter, '░')
    .Set(ProgressTheme.FilledForegroundColor, Hex1bColor.Blue)
    .Build();

var app = new Hex1bApp(
    ctx => ctx.Progress(60),
    new Hex1bAppOptions { Theme = theme }
);
