ctx.ThemingPanel(
    theme => theme
        .Set(ThemingPanelTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139))
        .Set(ThemingPanelTheme.ForegroundColor, Hex1bColor.White),
    ctx.Text("White text on dark blue")
)
