ctx.ThemingPanel(
    theme => theme
        .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
        .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Yellow)
        .Set(ListTheme.SelectedIndicator, "→ ")
        .Set(ListTheme.UnselectedIndicator, "  "),
    ctx.List(new[] { "Option A", "Option B", "Option C" })
)
