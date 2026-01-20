using Hex1b;

// Subscribe to title changes from the child terminal
bashHandle.WindowTitleChanged += title =>
{
    // Update your UI to reflect the new title
    app.Invalidate();
};

// Use the title in your widget tree
ctx.Border(
    ctx.Terminal(bashHandle),
    title: bashHandle.WindowTitle ?? "Terminal"
);
