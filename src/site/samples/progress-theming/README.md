# Progress — theming

Run this standalone sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Source

Extracted from `src/content/guide/widgets/progress.md`, binding `themingSnippet`.
Imported source: `src/content/guide/widgets/snippets/progress-theming.cs`.
The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Added Hex1b and Hex1b.Theming imports and replaced the removed Hex1bThemeBuilder with new Hex1bTheme("Custom").
