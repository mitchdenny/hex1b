ctx.VStack(v => [
    // Primary action area
    v.ThemingPanel(
        theme => theme
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
            .Set(BorderTheme.BorderColor, Hex1bColor.Green),
        v.Border(b => [ b.Button("Confirm") ], title: "Primary")
    ),
    
    // Destructive action area  
    v.ThemingPanel(
        theme => theme
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
            .Set(BorderTheme.BorderColor, Hex1bColor.Red),
        v.Border(b => [ b.Button("Delete") ], title: "Danger")
    )
])
