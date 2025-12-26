ctx.Responsive(r => [
    r.WhenMinWidth(120, r => r.Text("Extra wide: 120+ columns")),
    r.WhenMinWidth(80, r => r.Text("Wide: 80-119 columns")),
    r.WhenMinWidth(50, r => r.Text("Medium: 50-79 columns")),
    r.Otherwise(r => r.Text("Narrow: < 50 columns"))
])
