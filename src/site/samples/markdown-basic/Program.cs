using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VScrollPanel(
        ctx.Markdown("""
            # Welcome to Hex1b

            Render **rich markdown** content in your terminal UI with full support
            for headings, *emphasis*, `inline code`, and more.

            ## Features

            - **Bold** and *italic* text formatting
            - Fenced code blocks with line numbers
            - Tables, lists, and block quotes
            - Interactive links with Tab navigation
            - Embedded images via Kitty Graphics Protocol

            ## Code Example

            \`\`\`csharp
            var app = new Hex1bApp(ctx =>
                ctx.Markdown("# Hello, World!")
            );
            await app.RunAsync();
            \`\`\`

            > The MarkdownWidget parses CommonMark-compatible
            > markdown and renders it as a composed widget tree.

            ---

            | Feature        | Status  |
            |:---------------|:-------:|
            | Headings       | ✅ Done |
            | Inline styles  | ✅ Done |
            | Code blocks    | ✅ Done |
            | Tables         | ✅ Done |
            | Links          | ✅ Done |
            """)
    ))
    .Build();

await terminal.RunAsync();
