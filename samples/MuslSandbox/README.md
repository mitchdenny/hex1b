# Musl sandbox

An interactive Alpine Linux environment for experimenting with Hex1b samples
against musl. This sandbox is for manual use only; it is not connected to CI.

## Build and open a shell

Run these commands from the repository root:

```bash
docker build -f samples/MuslSandbox/Dockerfile -t hex1b-musl-sandbox .
docker run --rm -it hex1b-musl-sandbox
```

The container opens Bash in `/repo`, with the repository sources, .NET 10 SDK,
native C build tools, Git, Python, and terminal definitions installed. It does
not install glibc compatibility packages.

Host `bin`/`obj` directories and prebuilt Hex1b native libraries are excluded
from the image. The first `dotnet run` restores dependencies and builds the
native interop library against musl. Internet access is required for restore.

## Run samples

Inside the container:

```bash
dotnet --info
dotnet run --project samples/PickerDemo
dotnet run --project samples/EmbeddedTerminalDemo
```

In `EmbeddedTerminalDemo`, press Ctrl+N to start a Bash shell hosted in a real
PTY. Its PowerShell and Windows command-shell options are not installed in this
image.

For `SubProcessDemo`, change into its directory so its child scripts resolve:

```bash
cd /repo/samples/SubProcessDemo
dotnet run
```

Graphics samples still require support in your host terminal (for example,
kitty graphics or Sixel). Samples needing external services or additional tools
require their own setup; the sandbox does not run Aspire or expose the host
Docker socket.

## Architecture and source changes

By default, Docker builds for its host architecture: ARM64 on Apple Silicon,
or x64 on an x64 Docker host. To experiment with x64 on an ARM64 host, specify
the platform for both commands:

```bash
docker build --platform linux/amd64 \
  -f samples/MuslSandbox/Dockerfile -t hex1b-musl-sandbox-x64 .
docker run --rm -it --platform linux/amd64 hex1b-musl-sandbox-x64
```

Use `linux/arm64` for an explicit ARM64 image. Running a different architecture
requires Docker emulation and can behave differently from a native runner.

The image contains a snapshot of the checkout at build time. Rebuild it after
changing sources. Files created or edited inside the container do not change
your checkout and are discarded when the `--rm` container exits.
