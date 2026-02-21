# WasmDemo

A 3D globe rendered with braille characters, running as a .NET WASM app in the browser using xterm.js.

This demo uses raw WebAssembly (not Blazor) with a Web Worker to run the .NET runtime off the main thread.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- WASM workloads:
  ```bash
  dotnet workload install wasm-tools wasm-experimental
  ```

## Run

```bash
dotnet run
```

This launches a local web server with the correct COOP/COEP headers. Open the URL shown in the console output in your browser. Click and drag to rotate the globe.
