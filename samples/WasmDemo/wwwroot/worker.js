// worker.js — runs inside a Web Worker, boots .NET WASM runtime
import { dotnet } from './_framework/dotnet.js';

try {
    await dotnet.create();
    self.postMessage({ type: 'workerReady' });
    await dotnet.run();
} catch (err) {
    self.postMessage({ type: 'error', message: err.toString(), stack: err.stack });
}
