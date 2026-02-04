ctx.TabPanel(tp => [
    tp.Tab("Tab 1", t => [t.Text("Content 1")]).Selected(),
    tp.Tab("Tab 2", t => [t.Text("Content 2")]),
    tp.Tab("Tab 3", t => [t.Text("Content 3")])
]).Compact()
