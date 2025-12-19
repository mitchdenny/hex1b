// main.js - JavaScript module that bridges xterm.js to .NET WASM
// 
// This module connects xterm.js (terminal emulator in the browser) to the 
// .NET WASM runtime. It exposes a `writeTerminal` function that .NET can call
// via [JSImport], and calls .NET's [JSExport] methods for input and resize events.

import { dotnet } from './_framework/dotnet.js';

// xterm.js terminal instance
let term = null;
let fitAddon = null;

// .NET exports (BrowserTerminal static methods)
let dotnetExports = null;

// Function exposed to .NET via [JSImport("writeTerminal", "main.js")]
// .NET calls this directly to write to the terminal
function writeTerminal(text) {
    if (term) {
        term.write(text);
    }
}

async function main() {
    console.log('[JS] Starting .NET WASM runtime...');
    
    // Create and configure the .NET runtime
    const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
        .withDiagnosticTracing(false)
        .create();
    
    // Expose writeTerminal to .NET via JSImport
    setModuleImports('main.js', {
        writeTerminal: writeTerminal
    });
    
    // Get the .NET exports (BrowserTerminal's static methods)
    const config = getConfig();
    dotnetExports = await getAssemblyExports(config.mainAssemblyName);
    
    console.log('[JS] .NET exports:', Object.keys(dotnetExports));
    
    // Initialize xterm.js
    await initializeTerminal();
    
    // Run .NET Main - this will call Hex1bApp.RunAsync() which awaits on input
    console.log('[JS] Running .NET Main...');
    
    // Don't await runMain - it will block waiting for terminal input
    // Instead, let it run in the background while JS pumps events
    runMain().catch(err => {
        if (err.message !== 'OperationCanceledException') {
            console.error('[JS] .NET Main error:', err);
        }
    });
    
    console.log('[JS] .NET app started (running in background)');
}

async function initializeTerminal() {
    console.log('[JS] Initializing xterm.js...');
    
    // Wait for xterm to be loaded from CDN
    while (!window.Terminal || !window.FitAddon) {
        console.log('[JS] Waiting for xterm.js...');
        await new Promise(r => setTimeout(r, 100));
    }
    
    // Create terminal with styling
    term = new window.Terminal({
        cursorBlink: true,
        cursorStyle: 'block',
        fontFamily: '"Cascadia Code", "Fira Code", "JetBrains Mono", monospace',
        fontSize: 14,
        theme: {
            background: '#1e1e1e',
            foreground: '#d4d4d4',
            cursor: '#d4d4d4',
            cursorAccent: '#1e1e1e',
            selection: '#264f78',
            black: '#1e1e1e',
            red: '#f44747',
            green: '#6a9955',
            yellow: '#dcdcaa',
            blue: '#569cd6',
            magenta: '#c586c0',
            cyan: '#4ec9b0',
            white: '#d4d4d4',
            brightBlack: '#808080',
            brightRed: '#f44747',
            brightGreen: '#6a9955',
            brightYellow: '#dcdcaa',
            brightBlue: '#569cd6',
            brightMagenta: '#c586c0',
            brightCyan: '#4ec9b0',
            brightWhite: '#ffffff'
        }
    });
    
    // Fit addon for auto-sizing
    fitAddon = new window.FitAddon.FitAddon();
    term.loadAddon(fitAddon);
    
    // Open in container
    const container = document.getElementById('terminal-container');
    term.open(container);
    fitAddon.fit();
    
    // Forward keyboard input to .NET -> BrowserTerminal.OnInput
    term.onData((data) => {
        if (dotnetExports?.BrowserWasm?.BrowserTerminal?.OnInput) {
            dotnetExports.BrowserWasm.BrowserTerminal.OnInput(data);
        }
    });
    
    // Forward resize events to .NET -> BrowserTerminal.OnResize
    const sendResize = () => {
        fitAddon.fit();
        const dims = fitAddon.proposeDimensions();
        if (dims && dotnetExports?.BrowserWasm?.BrowserTerminal?.OnResize) {
            dotnetExports.BrowserWasm.BrowserTerminal.OnResize(dims.cols, dims.rows);
        }
    };
    
    const resizeObserver = new ResizeObserver(sendResize);
    resizeObserver.observe(container);
    
    // Send initial size
    sendResize();
    
    // Hide loading indicator
    const loading = document.getElementById('loading');
    if (loading) loading.style.display = 'none';
    
    console.log('[JS] xterm.js initialized');
}

// Start
main().catch(err => {
    console.error('[JS] Startup error:', err);
    document.body.innerHTML = `<pre style="color: red;">Error: ${err.message}\n${err.stack}</pre>`;
});
