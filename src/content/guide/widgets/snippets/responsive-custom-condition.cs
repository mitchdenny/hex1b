ctx.Responsive(r => [
    r.When((w, h) => w >= 100 && h >= 20, r =>
        r.Text("Large screen (100x20+)")
    ),
    r.When((w, h) => w >= 60 || h >= 15, r =>
        r.Text("Medium screen (60+ wide OR 15+ tall)")
    ),
    r.Otherwise(r => r.Text("Small screen"))
])
