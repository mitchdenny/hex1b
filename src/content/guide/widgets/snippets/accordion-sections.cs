ctx.Accordion(a => [
    a.Section(s => [
        s.Text("  src/"),
        s.Text("    Program.cs"),
        s.Text("    Utils.cs"),
    ]).Title("EXPLORER"),

    a.Section(s => [
        s.Text("  ▸ Properties"),
        s.Text("  ▸ Methods"),
    ]).Title("OUTLINE"),

    a.Section(s => [
        s.Text("  main"),
        s.Text("  develop"),
    ]).Title("SOURCE CONTROL"),
])
