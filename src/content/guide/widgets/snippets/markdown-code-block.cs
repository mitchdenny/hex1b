ctx.Markdown("""
    ```csharp
    var app = new Hex1bApp(ctx =>
        ctx.Text("Hello, World!")
    );
    await app.RunAsync();
    ```
    """)