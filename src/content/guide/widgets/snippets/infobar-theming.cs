ctx.InfoBar(s => [
    s.Section("Normal"),
    s.Divider(" │ "),
    s.Section("Warning").Theme(t => t
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow)),
    s.Divider(" │ "),
    s.Section("Error").Theme(t => t
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Red))
])
