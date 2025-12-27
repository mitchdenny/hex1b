ctx.Responsive(r => [
    r.WhenMinWidth(100, r => r.Text("Wide layout")),
    r.Otherwise(r => r.Text("Narrow layout"))
])
