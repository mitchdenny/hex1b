ctx.ThemePanel(
    theme => theme.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
    ctx.VStack(v => [
        v.Text("✓ Operation completed successfully"),
        v.Text("All files have been saved.")
    ])
)
