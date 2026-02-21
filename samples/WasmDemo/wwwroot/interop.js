// interop.js — JS interop functions called by .NET [JSImport] from the Worker
// This module is imported by .NET via JSHost.ImportAsync

const inputChunks = [];

let _frameCount = 0;
let _burstStart = 0;
let _burstCount = 0;
let _lastTime = 0;

export function postTerminalOutput(data) {
    const now = performance.now();
    _frameCount++;
    const gap = now - _lastTime;
    _lastTime = now;
    
    if (gap > 30) {
        // End of a gap — report the previous burst and the gap
        if (_burstCount > 0) {
            const burstDuration = (now - gap) - _burstStart;
            console.log(`[perf] burst: ${_burstCount} writes in ${burstDuration.toFixed(0)}ms, then ${gap.toFixed(0)}ms idle (total frames: ${_frameCount})`);
        }
        _burstStart = now;
        _burstCount = 1;
    } else {
        _burstCount++;
    }
    
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
    } else if (msg.type === 'resize') {
        pendingResize = msg.cols + ',' + msg.rows;
    }
}

// Drain any messages that arrived before this module was loaded
// (none expected with current architecture, but defensive)

// Set up handler for future messages
self.onmessage = function (e) {
    handleMessage(e.data);
};
