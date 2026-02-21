// main.js — runs on the main thread, owns xterm.js and spawns the Web Worker

let term = null;
let worker = null;

function sendResize() {
    if (!term || !worker) return;
    console.log(`[main] sendResize: ${term.cols}x${term.rows}`);
    worker.postMessage({ type: 'resize', cols: term.cols, rows: term.rows });
}

function init() {
    // Initialize xterm.js
    term = new Terminal({
        cursorBlink: true,
        convertEol: false,
        allowProposedApi: true,
        fontSize: 14,
        fontFamily: 'Menlo, Monaco, "Courier New", monospace',
        theme: {
            background: '#1e1e1e',
            foreground: '#d4d4d4'
        }
    });

    const fitAddon = new FitAddon.FitAddon();
    term.loadAddon(fitAddon);

    const container = document.getElementById('terminal');
    term.open(container);
    fitAddon.fit();

    // Spawn the Web Worker with initial dimensions so .NET starts at the right size
    worker = new Worker('./worker.js', { type: 'module' });

    // Don't send initial resize - the worker gets initial size from postMessage
    console.log(`[main] initial size: ${term.cols}x${term.rows}`);

    worker.onmessage = function (e) {
        const msg = e.data;
        if (msg.type === 'output') {
            // data is an ArrayBuffer of escape sequence bytes
            term.write(new Uint8Array(msg.data));
        } else if (msg.type === 'ready') {
            console.log(`Globe app ready (${msg.cols}x${msg.rows})`);
            // Send current size in case terminal was resized during startup
            sendResize();
        } else if (msg.type === 'workerReady') {
            console.log('Worker loaded, .NET runtime starting...');
        } else if (msg.type === 'error') {
            console.error('Worker error detail:', msg.message, msg.stack);
        }
    };

    worker.onerror = function (e) {
        console.error('Worker error:', e.message, e.filename, e.lineno);
    };

    // Forward xterm input to worker
    term.onData(function (data) {
        const bytes = new TextEncoder().encode(data);
        const base64 = btoa(String.fromCharCode(...bytes));
        worker.postMessage({ type: 'input', data: base64 });
    });

    term.onBinary(function (data) {
        const base64 = btoa(data);
        worker.postMessage({ type: 'input', data: base64 });
    });

    // Debounce resize
    let resizeTimer = null;
    term.onResize(function (size) {
        if (resizeTimer) clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            sendResize();
            resizeTimer = null;
        }, 100);
    });

    window.addEventListener('resize', () => {
        fitAddon.fit();
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
} else {
    init();
}
