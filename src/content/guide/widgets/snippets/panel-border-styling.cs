ctx.ThemingPanel(
    theme => theme
        .Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
        .Set(BorderTheme.TitleColor, Hex1bColor.White)
        .Set(BorderTheme.TopLeftCorner, "╔")
        .Set(BorderTheme.TopRightCorner, "╗")
        .Set(BorderTheme.BottomLeftCorner, "╚")
        .Set(BorderTheme.BottomRightCorner, "╝")
        .Set(BorderTheme.HorizontalLine, "═")
        .Set(BorderTheme.VerticalLine, "║"),
    ctx.Border(b => [ b.Text("Double-line border") ], title: "Fancy")
)
