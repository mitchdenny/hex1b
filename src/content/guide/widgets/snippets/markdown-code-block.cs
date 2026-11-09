ctx.Markdown("""
    ```csharp
    await using var terminal = Hex1bTerminal.CreateBuilder().WithHex1bApp(ctx =>
        ctx.Text("Hello, World!")
    ).Build();
    await terminal.RunAsync();
    ```
    """)