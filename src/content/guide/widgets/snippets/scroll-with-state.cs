var scrollState = new ScrollState();

ctx.VStack(v => [
    v.Text($"Scroll Position: {scrollState.Offset}/{scrollState.MaxOffset}"),
    v.Text(""),
    v.VScroll(
        inner => [
            inner.Text("Content line 1"),
            inner.Text("Content line 2"),
            inner.Text("Content line 3")
        ],
        scrollState
    ).FixedHeight(5)
])
