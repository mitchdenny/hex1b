# WasmDemo

A 3D globe rendered with braille characters, running as a .NET WASM app in the browser using xterm.js.

This demo uses raw WebAssembly (not Blazor) with a Web Worker to run the .NET runtime off the main thread.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A static file server that supports `Cross-Origin-Opener-Policy` and `Cross-Origin-Embedder-Policy` headers (required for `SharedArrayBuffer` used by WASM threading)

## Build & Publish

```bash
dotnet publish -c Release
```

The published output will be in `bin/Release/net10.0/browser-wasm/AppBundle/`.

## Serve

The site requires COOP/COEP headers for WASM threading. A simple Node.js server:

```js
// serve.mjs
import http from 'http';
import fs from 'fs';
import path from 'path';

const dir = process.argv[2] || '.';
const port = process.argv[3] || 8080;
const mimeTypes = {
  '.html': 'text/html', '.js': 'application/javascript', '.mjs': 'application/javascript',
  '.wasm': 'application/wasm', '.json': 'application/json', '.css': 'text/css',
  '.dat': 'application/octet-stream', '.blat': 'application/octet-stream',
  '.dll': 'application/octet-stream', '.pdb': 'application/octet-stream',
};

http.createServer((req, res) => {
  const filePath = path.join(dir, req.url === '/' ? 'index.html' : req.url.split('?')[0]);
  const ext = path.extname(filePath);
  fs.readFile(filePath, (err, data) => {
    if (err) { res.writeHead(404); res.end('Not found'); return; }
    res.writeHead(200, {
      'Content-Type': mimeTypes[ext] || 'application/octet-stream',
      'Cross-Origin-Opener-Policy': 'same-origin',
      'Cross-Origin-Embedder-Policy': 'require-corp',
    });
    res.end(data);
  });
}).listen(port, () => console.log(`Serving ${dir} on http://localhost:${port}`));
```

Then run:

```bash
node serve.mjs bin/Release/net10.0/browser-wasm/AppBundle/ 8080
```

Open http://localhost:8080 in your browser. Click and drag to rotate the globe.
