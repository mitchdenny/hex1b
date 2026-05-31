// interop.js — JS interop functions called by .NET [JSImport] from the Worker
// This module is imported by .NET via JSHost.ImportAsync

const inputChunks = [];

export function postTerminalOutput(data) {
    // data is a Uint8Array view over WASM memory (JSType.MemoryView marshaling).
    // We MUST copy to a fresh Uint8Array before postMessage: structured-cloning
    // the view would clone the entire underlying WASM heap ArrayBuffer, not
    // just the view's range. Copying explicitly also lets us transfer ownership
    // of the small buffer instead of cloning it cross-thread.
    const copy = new Uint8Array(data.length);
    copy.set(data);
    self.postMessage({ type: 'output', data: copy.buffer }, [copy.buffer]);
}

export function notifyReady(cols, rows) {
    self.postMessage({ type: 'ready', cols, rows });
}

// Drain all queued input into a single byte array
export function pollAllInput() {
    if (inputChunks.length === 0) return null;
    let totalLen = 0;
    for (const chunk of inputChunks) totalLen += chunk.length;
    const result = new Uint8Array(totalLen);
    let offset = 0;
    for (const chunk of inputChunks) {
        result.set(chunk, offset);
        offset += chunk.length;
    }
    inputChunks.length = 0;
    return result;
}

let pendingResize = '';
export function pollResize() {
    const r = pendingResize;
    pendingResize = '';
    return r;
}

function handleMessage(msg) {
    if (msg.type === 'input') {
        const bytes = Uint8Array.from(atob(msg.data), c => c.charCodeAt(0));
        inputChunks.push(bytes);
        // Wake ReadInputAsync immediately instead of letting it poll on a timer.
        // The export is undefined until worker.js finishes hooking it up post-create;
        // the .NET-side poll fallback handles the brief startup window.
        if (self.__hex1bSignalInput) self.__hex1bSignalInput();
    } else if (msg.type === 'resize') {
        pendingResize = msg.cols + ',' + msg.rows;
        if (self.__hex1bSignalInput) self.__hex1bSignalInput();
    }
}

// Drain any messages that arrived before this module was loaded
// (none expected with current architecture, but defensive)

// Set up handler for future messages
self.onmessage = function (e) {
    handleMessage(e.data);
};
