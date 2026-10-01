# Using the Emulator — Browser Terminal Host

After satisfying the prerequisites below, explicitly run this source-only sample with .NET 10 SDK or later:

```sh
dotnet run -- --urls http://127.0.0.1:5000
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Prerequisites and source-only policy

Requires the .NET 10 SDK/ASP.NET Core runtime, bash on PATH, and a separately supplied browser terminal client compatible with WebSocketPresentationAdapter at /ws/terminal. Put client assets in wwwroot if serving them from this app. This unauthenticated demonstration executes a local shell: bind only to loopback and never expose it publicly. The site build compiles but never starts the host.

This project is compiled and exported for source browsing and cloning only. Site builds and smoke tests must never execute it; there are intentionally no recording or playback controls.

Build safely without starting the application:

```sh
dotnet build
```

## Source

Extracted from `src/content/guide/using-the-emulator.md`, binding `fence-1`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Uses Microsoft.NET.Sdk.Web to supply ASP.NET Core framework references and implicit imports; the displayed program is copied unchanged. Hex1b remains pinned to 0.166.0.
