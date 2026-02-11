// React to scroll position changes with OnScroll event
ctx.VStack(v => [
    v.Text($"Position: {currentOffset}/{maxOffset}"),
    v.Text($"Content: {contentSize} lines"),
    v.Border(
        v.VScroll(
            inner => [
                inner.Text("Content line 1"),
                inner.Text("Content line 2"),
                inner.Text("Content line 3"),
                inner.Text("Content line 4"),
                inner.Text("Content line 5"),
                inner.Text("Content line 6"),
                inner.Text("Content line 7"),
                inner.Text("Content line 8")
            ]
        ).OnScroll(args => {
            currentOffset = args.Offset;
            maxOffset = args.MaxOffset;
            contentSize = args.ContentSize;
        })
    ).Title("Scrollable Area")
])
