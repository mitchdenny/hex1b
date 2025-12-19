# Hex1b Browser WASM Spike

This is an experimental proof-of-concept showing how Hex1b terminal apps could run entirely in the browser via WebAssembly, without requiring a server-side backend.

## 🎯 Goal

Replace the current WebSocket-based architecture (where .NET runs on the server and communicates with xterm.js via WebSockets) with a pure client-side architecture where .NET WASM runs directly in the browser.

## 📐 Architecture

### Current (WebSocket-based)
```
┌─────────────────┐         WebSocket          ┌──────────────────┐
│   Browser       │◄─────────────────────────►│   Server         │
│                 │                            │                  │
│  ┌───────────┐  │   stdin/stdout over WS    │  ┌────────────┐  │
│  │ xterm.js  │◄─┼──────────────────────────►│──│  Hex1bApp  │  │
│  └───────────┘  │                            │  └────────────┘  │
└─────────────────┘                            └──────────────────┘
```

### Proposed (WASM-based)
```
┌──────────────────────────────────────────────────────────────────┐
│   Browser (everything runs client-side)                          │
│                                                                  │
│   ┌───────────────┐    JSImport/JSExport    ┌─────────────────┐ │
│   │   xterm.js    │◄───────────────────────►│  .NET WASM      │ │
│   │ (Terminal UI) │                          │  (Hex1bApp)     │ │
│   └───────────────┘                          └─────────────────┘ │
│          │                                          │            │
│          ▼                                          ▼            │
│   ┌───────────────┐                          ┌─────────────────┐ │
│   │   main.js     │◄────────────────────────►│ WasmHex1bTerminal│ │
│   │  (Interop)    │                          │ (IHex1bTerminal)│ │
│   └───────────────┘                          └─────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

## 🔧 Key Components

### 1. `Program.cs` - .NET Entry Point
- Standard `Main()` entry point that initializes Hex1bApp
- Exports methods via `[JSExport]` for JavaScript to call:
  - `OnKeyInput(string data)` - Receives keyboard input from xterm.js
  - `OnResize(int cols, int rows)` - Handles terminal resize
  - `GetPendingOutput()` - Returns buffered terminal output

### 2. `WasmHex1bTerminal` - Terminal Adapter
- Implements `IHex1bTerminal` for the WASM environment
- Buffers output and pushes to JavaScript via `[JSImport]`
- Receives input from JavaScript and queues as `Hex1bEvent`s

### 3. `main.js` - JavaScript Interop
- Initializes the .NET WASM runtime
- Sets up xterm.js terminal
- Bridges events between xterm.js and .NET:
  - Terminal input → `OnKeyInput()`
  - Terminal resize → `OnResize()`
  - .NET output → `term.write()`

### 4. `index.html` - Host Page
- Loads xterm.js from CDN
- Provides terminal container element
- Loads the .NET WASM app via ES module

## 🚧 Challenges & Trade-offs

### Control Flow Inversion
Traditional console apps block on input (`Console.ReadKey()`). WASM apps must be **event-driven** - JavaScript calls into .NET when events occur.

**Solution:** The Hex1bApp needs a `RenderOnceAsync()` method instead of running a blocking event loop. Each JS event triggers:
1. Process input
2. Update state
3. Re-render

### Threading Limitations
WASM doesn't support blocking threads. We can't use `Thread.Sleep()` or blocking `Channel.Reader.ReadAsync()`.

**Solution:** Input comes via JS interop calls, not from polling a channel.

### Bundle Size
.NET WASM apps are larger than pure JS apps. The runtime + assemblies can be 5-20MB.

**Mitigations:**
- Enable IL trimming
- Use AOT compilation for better startup (but larger size)
- Consider lazy loading for rarely-used features

### Async Considerations
`[JSExport]` methods can be `async Task` but synchronous calls are often simpler for event handlers.

## 📋 Prerequisites

```bash
# Install the wasm-experimental workload
dotnet workload install wasm-experimental

# Or install from NuGet templates
dotnet new install Microsoft.NET.Runtime.WebAssembly.Templates
```

## 🚀 Running the Spike

```bash
# Build the project
dotnet build

# Publish for WASM
dotnet publish -c Release

# Serve the output (using any static file server)
# Option 1: dotnet serve
dotnet tool install -g dotnet-serve
dotnet serve -d bin/Release/net10.0/publish/wwwroot

# Option 2: Python
cd bin/Release/net10.0/publish/wwwroot
python -m http.server 8000

# Then open http://localhost:8000 in your browser
```

## 🔮 Future Considerations

### 1. Streaming Compilation
For better startup, enable streaming WASM compilation:
```js
const { create } = await dotnet.withStartupMemoryCache(true);
```

### 2. Worker Threads
Run .NET in a Web Worker for better responsiveness:
- Main thread handles UI
- Worker thread runs .NET
- `SharedArrayBuffer` or `postMessage` for communication

### 3. Progressive Loading
Split assemblies and load on-demand to reduce initial load time.

### 4. Service Worker Caching
Cache WASM files for offline use and faster subsequent loads.

## 📊 Comparison

| Aspect | WebSocket (Current) | WASM (This Spike) |
|--------|---------------------|-------------------|
| Server Required | ✅ Yes | ❌ No |
| Latency | ~10-50ms round-trip | <1ms |
| Offline Support | ❌ No | ✅ Yes |
| Initial Load | Fast | Slower (WASM download) |
| Bundle Size | Small (just xterm) | Larger (+.NET runtime) |
| Scalability | Limited by server | Unlimited (client-side) |
| Development | Simpler | More complex |

## 🎓 Learning Resources

- [.NET WASM documentation](https://learn.microsoft.com/aspnet/core/client-side/dotnet-interop/wasm-browser-app)
- [JSImport/JSExport interop](https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/import-export-interop)
- [xterm.js documentation](https://xtermjs.org/)
- [.NET WASM runtime configuration](https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md)
