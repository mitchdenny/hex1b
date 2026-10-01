# Terminal Emulator — Headless Build Command

After satisfying the prerequisites below, explicitly run this source-only sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Prerequisites and source-only policy

Requires the .NET 10 SDK and a project or solution in the current working directory for the child dotnet build command. The workload uses a headless presentation, so there is no meaningful terminal playback. Running it from this sample directory builds this sample; running with --project from another project directory builds that directory instead.

This project is compiled and exported for source browsing and cloning only. Site builds and smoke tests must never execute it; there are intentionally no recording or playback controls.

Build safely without starting the application:

```sh
dotnet build
```

## Source

Extracted from `src/content/guide/terminal-emulator.md`, binding `fence-2`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Added the namespace imports omitted by the surrounding guide; the application logic is unchanged.
