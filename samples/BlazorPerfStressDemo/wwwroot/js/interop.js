// interop.js — xterm.js management + fast [JSImport] targets via globalThis

(function () {
    let term = null;
    let fitAddon = null;
    const inputChunks = [];
    let pendingResize = '';

    window.termInterop = {
        // Initialize xterm.js — called from Blazor component
        init: function () {
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

            fitAddon = new FitAddon.FitAddon();
            term.loadAddon(fitAddon);

            const container = document.getElementById('terminal');
            term.open(container);
            fitAddon.fit();

            // Forward xterm input to queue, throttling mouse-move events
            let lastMouseMoveTime = 0;
            const MOUSE_THROTTLE_MS = 50; // ~20Hz max for mouse moves

            term.onData(function (data) {
                // Detect SGR mouse-move sequences (button 35 = motion with button held)
                // Format: \x1b[<35;X;Y[Mm]
                if (data.indexOf('\x1b[<35;') !== -1) {
                    const now = performance.now();
                    if (now - lastMouseMoveTime < MOUSE_THROTTLE_MS) return; // drop
                    lastMouseMoveTime = now;
                }
                const bytes = new TextEncoder().encode(data);
                inputChunks.push(bytes);
                // Wake .NET's ReadInputAsync immediately (set by App.razor once exports load).
                if (window.__hex1bSignalInput) window.__hex1bSignalInput();
            });

            term.onBinary(function (data) {
                const bytes = new Uint8Array(data.length);
                for (let i = 0; i < data.length; i++) bytes[i] = data.charCodeAt(i);
                inputChunks.push(bytes);
                if (window.__hex1bSignalInput) window.__hex1bSignalInput();
            });

            // Debounced resize
            let resizeTimer = null;
            window.addEventListener('resize', () => { fitAddon.fit(); });
            term.onResize(function (size) {
                if (resizeTimer) clearTimeout(resizeTimer);
                resizeTimer = setTimeout(() => {
                    pendingResize = size.cols + ',' + size.rows;
                    if (window.__hex1bSignalInput) window.__hex1bSignalInput();
                    resizeTimer = null;
                }, 100);
            });

            console.log(`[blazor] xterm initialized: ${term.cols}x${term.rows}`);
            return `${term.cols},${term.rows}`;
        },

        // [JSImport] target — write output bytes directly to xterm
        postOutput: function (data) {
            if (!term) return;

            // data is a Uint8Array view over WASM memory (JSType.MemoryView).
            // Copy bounded to the view's range — never clone the underlying
            // WASM heap by passing the view directly to consumers that hold on
            // to it past the synchronous call.
            const copy = new Uint8Array(data.length);
            copy.set(data);
            term.write(copy);
        },

        // [JSImport] target — drain all queued input
        pollAllInput: function () {
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
        },

        // [JSImport] target — poll pending resize
        pollResize: function () {
            const r = pendingResize;
            pendingResize = '';
            return r;
        },

        // Looks up the C# [JSExport] SignalInputAvailable on the running .NET
        // runtime and stashes a callable reference so the input/resize handlers
        // can wake ReadInputAsync immediately. Falls back to a no-op if the
        // runtime API isn't available (e.g. older Blazor build); the .NET-side
        // poll fallback covers that case at the cost of 50ms latency.
        attachSignal: async function (assemblyName) {
            try {
                const runtime = globalThis.getDotnetRuntime ? globalThis.getDotnetRuntime(0) : null;
                if (!runtime || !runtime.getAssemblyExports) {
                    console.warn('[blazor] dotnet runtime API unavailable; staying on poll fallback');
                    return;
                }
                const exports = await runtime.getAssemblyExports(assemblyName);
                window.__hex1bSignalInput =
                    exports?.[assemblyName]?.BlazorPresentationAdapter?.SignalInputAvailable;
                if (!window.__hex1bSignalInput) {
                    console.warn('[blazor] SignalInputAvailable export not found');
                }
            } catch (e) {
                console.warn('[blazor] attachSignal failed:', e);
            }
        }
    };
})();
