# Theming Demo

Run this standalone sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Source

Extracted from `src/content/guide/theming.md`, binding `TerminalDemo`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Complete logic from src/Hex1b.Website/Examples/ThemingExample.cs; removed ASP.NET/logger/base-class plumbing and used a normal console entry point. Replaced thread-local WebSocket session storage with ordinary instance state for this single standalone application.
