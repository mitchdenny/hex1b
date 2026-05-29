// worker.js — runs inside a Web Worker, boots .NET WASM runtime
import { dotnet } from './_framework/dotnet.js';

try {
    const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
    self.postMessage({ type: 'workerReady' });

    // Grab the C# [JSExport] surface BEFORE Main runs so interop.js can wake
    // ReadInputAsync immediately when input or a resize arrives, instead of
    // waiting for a poll tick. Setting this before runMain is fine — interop.js
    // only calls it from message handlers, which can't fire until the event loop
    // returns to the worker.
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);
    // Adapter is in namespace GlobeDemoWasm, class WasmPresentationAdapter.
    self.__hex1bSignalInput = exports.GlobeDemoWasm.WasmPresentationAdapter.SignalInputAvailable;

    await runMain();
} catch (err) {
    self.postMessage({ type: 'error', message: err.toString(), stack: err.stack });
}
