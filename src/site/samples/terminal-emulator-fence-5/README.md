# Terminal Emulator — Dockerfile Workload

After satisfying the prerequisites below, explicitly run this source-only sample with .NET 10 SDK or later:

```sh
dotnet run
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

## Prerequisites and source-only policy

Requires the Docker CLI, a running Linux-container daemon, and a Dockerfile at ./test-env/Dockerfile relative to the working directory. Supply your own Dockerfile and build-context files; the SDK_VERSION=10.0 argument is passed to that Dockerfile. Running this application builds an image and starts a container; dotnet build only compiles the application.

This project is compiled and exported for source browsing and cloning only. Site builds and smoke tests must never execute it; there are intentionally no recording or playback controls.

Build safely without starting the application:

```sh
dotnet build
```

## Source

Extracted from `src/content/guide/terminal-emulator.md`, binding `fence-5`.

The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

Added the namespace imports omitted by the surrounding guide; the application logic is unchanged.
