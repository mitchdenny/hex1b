# Pie Chart

Run this standalone sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Source

Extracted from `src/content/guide/widgets/charts.md`, binding `donutPieCode`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Decoded HTML entities in the displayed C# (for example &gt; to >). Updated WithHex1bApp to the separate eager options/configure-app callbacks in Hex1b 0.166.0.
