# QrCodeWidget — example 3

Run this standalone sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Source

Extracted from `src/content/guide/widgets/qrcode.md`, binding `fence-3`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Updated WithHex1bApp to the separate eager options/configure-app callbacks in Hex1b 0.166.0. Replaced ThemeElement with GlobalTheme and the removed ThemePanel convenience API with VStack; the same green foreground and black background are applied by the configured global theme.
