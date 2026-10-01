# Terminal Emulator — Configured Docker Container

After satisfying the prerequisites below, explicitly run this source-only sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Prerequisites and source-only policy

Requires the Docker CLI and a running Linux-container daemon, plus ubuntu:24.04 locally or access to pull it. Before running, replace /host/data with an existing absolute host directory that you explicitly want mounted read-only at /container/data, and review the /app container working directory. No host data or services are provisioned by the build.

This project is compiled and exported for source browsing and cloning only. Site builds and smoke tests must never execute it; there are intentionally no recording or playback controls.

Build safely without starting the application:

```sh
dotnet build
```

## Source

Extracted from `src/content/guide/terminal-emulator.md`, binding `fence-4`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Added the namespace imports omitted by the surrounding guide; the application logic is unchanged.
