ctx.ThemePanel(
    outer => outer.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
    ctx.VStack(v => [
        v.Text("Outer theme applies here"),
        v.ThemePanel(
            inner => inner.Clone()
                .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139)),
            v.Text("Both themes combined")
        ),
        v.Text("Only outer theme here")
    ])
)
