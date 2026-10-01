using Hex1b;

var lastActivated = "";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.VScrollPanel(
            v.Markdown("""
                # Focusable Links Demo

                Use **Tab** and **Shift+Tab** to navigate between links.
                Press **Enter** to activate a focused link.

                ## Navigation

                - [Hex1b on GitHub](https://github.com/mitchdenny/hex1b)
                - [Getting Started](/guide/getting-started)
                - [Widget Documentation](/guide/widgets/)

                ## Intra-Document Links

                Jump to the [Navigation](#navigation) section above,
                or go to [Resources](#resources) below.

                ## Resources

                Check out the [API Reference](/reference/) for details.
                """)
                .Focusable(children: true)
                .OnLinkActivated(args =>
                {
                    lastActivated = $"{args.Kind}: {args.Url}";
                    args.Handled = true;
                })
        ),
        v.Text(string.IsNullOrEmpty(lastActivated)
            ? "Press Tab to focus a link, Enter to activate"
            : $"Activated → {lastActivated}")
    ]))
    .Build();

await terminal.RunAsync();
