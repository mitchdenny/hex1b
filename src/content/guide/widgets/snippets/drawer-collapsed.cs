ctx.Drawer(
    isExpanded: false,
    onToggle: expanded => { /* handle toggle */ },
    header: ctx.Text("📁 Files"),
    content: ctx.VStack(v => [
        v.Text("Documents"),
        v.Text("Downloads"),
        v.Text("Pictures")
    ])
)
