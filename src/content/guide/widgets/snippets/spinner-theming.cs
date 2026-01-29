ctx.ThemePanel(
    theme =>
    {
        theme.Set(SpinnerTheme.Style, SpinnerStyle.Circle);
        theme.Set(SpinnerTheme.ForegroundColor, Hex1bColor.Cyan);
        return theme;
    },
    t => [
        t.HStack(h => [
            h.Spinner(),
            h.Text(" Themed spinner")
        ])
    ]
)
