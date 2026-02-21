// interop.js — xterm.js management + fast [JSImport] targets via globalThis

(function () {
    let term = null;
    let fitAddon = null;
    const inputChunks = [];
    let pendingResize = '';

    // Burst timing instrumentation
    let _frameCount = 0;
    let _burstStart = 0;
    let _burstCount = 0;
    let _lastTime = 0;

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
            });

            term.onBinary(function (data) {
                const bytes = new Uint8Array(data.length);
                for (let i = 0; i < data.length; i++) bytes[i] = data.charCodeAt(i);
                inputChunks.push(bytes);
            });

            // Debounced resize
            let resizeTimer = null;
            window.addEventListener('resize', () => { fitAddon.fit(); });
            term.onResize(function (size) {
                if (resizeTimer) clearTimeout(resizeTimer);
                resizeTimer = setTimeout(() => {
                    pendingResize = size.cols + ',' + size.rows;
                    resizeTimer = null;
                }, 100);
            });

            console.log(`[blazor] xterm initialized: ${term.cols}x${term.rows}`);
            return `${term.cols},${term.rows}`;
        },

        // [JSImport] target — write output bytes directly to xterm
        postOutput: function (data) {
            if (!term) return;

            // Burst timing
            const now = performance.now();
            _frameCount++;
            const gap = now - _lastTime;
            _lastTime = now;
            if (gap > 30) {
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
        }
    };
})();
