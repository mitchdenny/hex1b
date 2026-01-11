# Guide

Hex1b is a comprehensive .NET terminal application stack. Whether you're building rich text-based user interfaces, need a programmable terminal emulator, want to test terminal applications, or integrate AI agents with terminal sessions, Hex1b has you covered.

## Architecture

Hex1b has a layered and extensible architecture. It includes everything you need for most scenarios, or it can be extended to support your specific scenarios.

### Hex1bTerminal

At the core of the stack is Hex1bTerminal which  is a pluggable terminal emulator.

### Workload Adapeters

Workload adapters allow you plug the Hex1bTerminal into any source. You can connect to a .NET process, or spawn a real shell with a PTY attached or even connect a network stream. For building TUI applications we have the Hex1bApp workload adapter which allows you to conenct Hex1b's own TUI framework to the Hex1bTerminal.

### Presentation Adapters
Presentation adapeters connect the terminal to the end user whether it is a real terminal emualtor such as GNOME Termina, Xterm, or Windows Terminal, or even remote terminals such as xterm.js.

### Input Sequencers & Pattern Matchers
fsdfs

<div class="architecture-diagram">
<svg viewBox="0 0 520 520" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <linearGradient id="terminalGrad" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" style="stop-color:#4ecdc4;stop-opacity:0.3" />
      <stop offset="100%" style="stop-color:#4ecdc4;stop-opacity:0.1" />
    </linearGradient>
    <linearGradient id="boxGrad" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" style="stop-color:#1a1a2e;stop-opacity:1" />
      <stop offset="100%" style="stop-color:#0d0d18;stop-opacity:1" />
    </linearGradient>
    <filter id="glow">
      <feGaussianBlur stdDeviation="2" result="coloredBlur"/>
      <feMerge>
        <feMergeNode in="coloredBlur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  
  <!-- Top row: Presentation Adapter outputs (y=10, height=50) -->
  <rect x="65" y="10" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="120" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Console</text>
  <text x="120" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">Native Terminal</text>
  
  <rect x="205" y="10" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="260" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Web</text>
  <text x="260" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">xterm.js</text>
  
  <rect x="345" y="10" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="400" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Headless</text>
  <text x="400" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">Testing</text>
  
  <!-- Lines from Presentation Adapters to top outputs (bidirectional) -->
  <g style="animation: adapterCycle 6s ease-in-out infinite;">
    <!-- Up arrow (left) -->
    <path d="M115 110 L115 60" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="115,60 110,68 120,68" fill="#4ecdc4"/>
    <!-- Down arrow (right) -->
    <path d="M125 60 L125 110" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="125,110 120,102 130,102" fill="#4ecdc4"/>
    <!-- Data flow dots up -->
    <circle cx="115" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
    <circle cx="115" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
    <circle cx="115" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
    <!-- Data flow dots down -->
    <circle cx="125" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
    <circle cx="125" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
    <circle cx="125" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  </g>
  <g style="animation: adapterCycle 6s ease-in-out infinite 2s;">
    <!-- Up arrow (left) -->
    <path d="M255 110 L255 60" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="255,60 250,68 260,68" fill="#4ecdc4"/>
    <!-- Down arrow (right) -->
    <path d="M265 60 L265 110" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="265,110 260,102 270,102" fill="#4ecdc4"/>
    <!-- Data flow dots up -->
    <circle cx="255" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
    <circle cx="255" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
    <circle cx="255" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
    <!-- Data flow dots down -->
    <circle cx="265" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
    <circle cx="265" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
    <circle cx="265" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  </g>
  <g style="animation: adapterCycle 6s ease-in-out infinite 4s;">
    <!-- Up arrow (left) -->
    <path d="M395 110 L395 60" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="395,60 390,68 400,68" fill="#4ecdc4"/>
    <!-- Down arrow (right) -->
    <path d="M405 60 L405 110" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="405,110 400,102 410,102" fill="#4ecdc4"/>
    <!-- Data flow dots up -->
    <circle cx="395" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
    <circle cx="395" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
    <circle cx="395" cy="85" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
    <!-- Data flow dots down -->
    <circle cx="405" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
    <circle cx="405" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
    <circle cx="405" cy="85" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  </g>
  
  <!-- Presentation Adapters Box (y=110, height=60) -->
  <rect x="60" y="110" width="400" height="60" rx="10" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.7)" stroke-width="1.5"/>
  <text x="260" y="138" text-anchor="middle" fill="#fff" font-family="monospace" font-size="15" font-weight="bold">Presentation Adapters</text>
  <text x="260" y="156" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="10">IHex1bTerminalPresentationAdapter</text>
  
  <!-- Connection lines between Terminal and Presentation Adapters (bidirectional) -->
  <!-- Up arrow (left) -->
  <path d="M250 220 L250 170" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
  <polygon points="250,170 245,178 255,178" fill="#4ecdc4"/>
  <!-- Down arrow (right) -->
  <path d="M270 170 L270 220" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
  <polygon points="270,220 265,212 275,212" fill="#4ecdc4"/>
  <!-- Data flow dots going up -->
  <circle cx="250" cy="195" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
  <circle cx="250" cy="195" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
  <circle cx="250" cy="195" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
  <!-- Data flow dots going down -->
  <circle cx="270" cy="195" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
  <circle cx="270" cy="195" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
  <circle cx="270" cy="195" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  
  <!-- Circle connector linking the three components (drawn first so boxes overlap it) -->
  <circle cx="140" cy="260" r="55" fill="none" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3"/>
  <!-- Animated dots rotating around the circle (16 dots evenly spaced) -->
  <g style="transform-origin: 140px 260px; animation: rotateClockwise 8s linear infinite;">
    <circle cx="140" cy="205" r="3" fill="#4ecdc4"/>
    <circle cx="161" cy="209" r="3" fill="#4ecdc4"/>
    <circle cx="179" cy="221" r="3" fill="#4ecdc4"/>
    <circle cx="191" cy="239" r="3" fill="#4ecdc4"/>
    <circle cx="195" cy="260" r="3" fill="#4ecdc4"/>
    <circle cx="191" cy="281" r="3" fill="#4ecdc4"/>
    <circle cx="179" cy="299" r="3" fill="#4ecdc4"/>
    <circle cx="161" cy="311" r="3" fill="#4ecdc4"/>
    <circle cx="140" cy="315" r="3" fill="#4ecdc4"/>
    <circle cx="119" cy="311" r="3" fill="#4ecdc4"/>
    <circle cx="101" cy="299" r="3" fill="#4ecdc4"/>
    <circle cx="89" cy="281" r="3" fill="#4ecdc4"/>
    <circle cx="85" cy="260" r="3" fill="#4ecdc4"/>
    <circle cx="89" cy="239" r="3" fill="#4ecdc4"/>
    <circle cx="101" cy="221" r="3" fill="#4ecdc4"/>
    <circle cx="119" cy="209" r="3" fill="#4ecdc4"/>
  </g>
  
  <!-- Central Terminal Box (y=220, height=80) - centered at x=260 -->
  <rect x="135" y="220" width="250" height="80" rx="12" fill="url(#boxGrad)" stroke="#4ecdc4" stroke-width="2" filter="url(#glow)"/>
  <text x="260" y="255" text-anchor="middle" fill="#4ecdc4" font-family="monospace" font-size="18" font-weight="bold">Hex1bTerminal</text>
  <text x="260" y="280" text-anchor="middle" fill="rgba(255,255,255,0.6)" font-family="sans-serif" font-size="12">Terminal Emulator Core</text>
  
  <!-- Input Sequencer (top left of Terminal) -->
  <rect x="5" y="200" width="100" height="45" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="55" y="220" text-anchor="middle" fill="#fff" font-family="monospace" font-size="9" font-weight="bold">Input Sequencer</text>
  <text x="55" y="233" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="7">Keyboard/Mouse</text>
  
  <!-- Pattern Searcher (bottom left of Terminal) -->
  <rect x="5" y="275" width="100" height="45" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="55" y="295" text-anchor="middle" fill="#fff" font-family="monospace" font-size="9" font-weight="bold">Pattern Searcher</text>
  <text x="55" y="308" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="7">Cell Content</text>
  
  <!-- Connection lines between Workload Adapters and Terminal (bidirectional) -->
  <!-- Up arrow (left) -->
  <path d="M250 350 L250 300" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
  <polygon points="250,300 245,308 255,308" fill="#4ecdc4"/>
  <!-- Down arrow (right) -->
  <path d="M270 300 L270 350" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
  <polygon points="270,350 265,342 275,342" fill="#4ecdc4"/>
  <!-- Data flow dots going up -->
  <circle cx="250" cy="325" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.2s;"/>
  <circle cx="250" cy="325" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.7s;"/>
  <circle cx="250" cy="325" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1.2s;"/>
  <!-- Data flow dots going down -->
  <circle cx="270" cy="325" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite;"/>
  <circle cx="270" cy="325" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.5s;"/>
  <circle cx="270" cy="325" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1s;"/>
  
  <!-- Workload Adapters Box (y=350, height=60) -->
  <rect x="60" y="350" width="400" height="60" rx="10" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.7)" stroke-width="1.5"/>
  <text x="260" y="378" text-anchor="middle" fill="#fff" font-family="monospace" font-size="15" font-weight="bold">Workload Adapters</text>
  <text x="260" y="396" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="10">IHex1bTerminalWorkloadAdapter</text>
  
  <!-- Connection lines from bottom to Workload Adapters (bidirectional) -->
  <g style="animation: adapterCycle 6s ease-in-out infinite 3s;">
    <!-- Up arrow (left) -->
    <path d="M115 460 L115 410" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="115,410 110,418 120,418" fill="#4ecdc4"/>
    <!-- Down arrow (right) -->
    <path d="M125 410 L125 460" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="125,460 120,452 130,452" fill="#4ecdc4"/>
    <!-- Data flow dots up -->
    <circle cx="115" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
    <circle cx="115" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
    <circle cx="115" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
    <!-- Data flow dots down -->
    <circle cx="125" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
    <circle cx="125" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
    <circle cx="125" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  </g>
  <g style="animation: adapterCycle 6s ease-in-out infinite 5s;">
    <!-- Up arrow (left) -->
    <path d="M255 460 L255 410" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="255,410 250,418 260,418" fill="#4ecdc4"/>
    <!-- Down arrow (right) -->
    <path d="M265 410 L265 460" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="265,460 260,452 270,452" fill="#4ecdc4"/>
    <!-- Data flow dots up -->
    <circle cx="255" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
    <circle cx="255" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
    <circle cx="255" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
    <!-- Data flow dots down -->
    <circle cx="265" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
    <circle cx="265" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
    <circle cx="265" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  </g>
  <g style="animation: adapterCycle 6s ease-in-out infinite 1s;">
    <!-- Up arrow (left) -->
    <path d="M395 460 L395 410" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="395,410 390,418 400,418" fill="#4ecdc4"/>
    <!-- Down arrow (right) -->
    <path d="M405 410 L405 460" stroke="rgba(78,205,196,0.5)" stroke-width="2" stroke-dasharray="4,3" fill="none"/>
    <polygon points="405,460 400,452 410,452" fill="#4ecdc4"/>
    <!-- Data flow dots up -->
    <circle cx="395" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite;"/>
    <circle cx="395" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 0.5s;"/>
    <circle cx="395" cy="435" r="3" fill="#4ecdc4" style="animation: flowUp 1.5s ease-in-out infinite 1s;"/>
    <!-- Data flow dots down -->
    <circle cx="405" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.25s;"/>
    <circle cx="405" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 0.75s;"/>
    <circle cx="405" cy="435" r="3" fill="#4ecdc4" style="animation: flowDown 1.5s ease-in-out infinite 1.25s;"/>
  </g>
  
  <!-- Hex1bApp Box (y=460, height=50) -->
  <rect x="65" y="460" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="120" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Hex1bApp</text>
  <text x="120" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">TUI Framework</text>
  
  <!-- Shells Box -->
  <rect x="205" y="460" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="260" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Shells</text>
  <text x="260" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">bash, pwsh, zsh</text>
  
  <!-- Other Processes Box -->
  <rect x="345" y="460" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="400" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Processes</text>
  <text x="400" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">htop, vim, any CLI</text>
</svg>
</div>

## Feature Areas

<div class="feature-cards">

<a href="/guide/tui" class="feature-card">
  <div class="feature-icon">🖥️</div>
  <h3>Terminal User Interfaces</h3>
  <p>Build rich, interactive TUIs with a React-inspired declarative API. Create dashboards, dev tools, and CLI experiences that go beyond simple text output.</p>
</a>

<a href="/guide/terminal-emulator" class="feature-card">
  <div class="feature-icon">⌨️</div>
  <h3>Terminal Emulator</h3>
  <p>Embed a fully-featured terminal emulator in your .NET applications. Host shells, run commands, and build developer tools with complete terminal control.</p>
</a>

<a href="/guide/testing" class="feature-card">
  <div class="feature-icon">🧪</div>
  <h3>Automation & Testing</h3>
  <p>Test terminal applications programmatically. Assert on screen content, simulate user input, and integrate with your CI/CD pipeline.</p>
</a>

<a href="/guide/mcp-server" class="feature-card">
  <div class="feature-icon">🤖</div>
  <h3>MCP Server</h3>
  <p>Expose terminal sessions to AI agents via the Model Context Protocol. Let LLMs interact with your terminal applications programmatically.</p>
</a>

</div>

## Quick Start

New to Hex1b? Start here:

1. **[Getting Started](/guide/getting-started)** — Install Hex1b and build your first app
2. **[Your First App](/guide/first-app)** — A step-by-step walkthrough
3. **[Widgets & Nodes](/guide/widgets-and-nodes)** — Understand the core architecture

## Core Concepts

Once you're comfortable with the basics, dive deeper:

- **[Layout System](/guide/layout)** — Master constraint-based layouts with `HStack`, `VStack`, and more
- **[Input Handling](/guide/input)** — Keyboard navigation, focus management, and shortcuts
- **[Theming](/guide/theming)** — Customize colors, borders, and styles
- **[Widget Reference](/guide/widgets/align)** — Explore all available widgets

## API Reference

Looking for detailed API documentation?

- **[API Reference](/reference/)** — Complete type and method documentation generated from source

<style>
.architecture-diagram {
  margin: 32px 0;
  padding: 24px;
  background: linear-gradient(180deg, #0d0d18 0%, #0a0a12 100%);
  border-radius: 16px;
  border: 1px solid rgba(78, 205, 196, 0.2);
}

.architecture-diagram svg {
  width: 100%;
  max-width: 600px;
  height: auto;
  display: block;
  margin: 0 auto;
}

@keyframes rotateClockwise {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

@keyframes adapterCycle {
  0%, 33% { opacity: 1; }
  34%, 100% { opacity: 0; }
}

@keyframes flowUp {
  0% { transform: translateY(20px); }
  100% { transform: translateY(-20px); }
}

@keyframes flowDown {
  0% { transform: translateY(-20px); }
  100% { transform: translateY(20px); }
}

.feature-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
  margin: 24px 0;
}

.feature-card {
  display: block;
  padding: 24px;
  border-radius: 12px;
  border: 1px solid var(--vp-c-divider);
  background: var(--vp-c-bg-soft);
  text-decoration: none !important;
  transition: all 0.2s ease;
}

.feature-card,
.feature-card *,
.feature-card h3,
.feature-card p {
  text-decoration: none !important;
}

.feature-card:hover {
  border-color: var(--vp-c-brand-1);
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.1);
}

.dark .feature-card:hover {
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
}

.feature-icon {
  font-size: 2rem;
  margin-bottom: 12px;
}

.feature-card h3 {
  color: var(--vp-c-text-1);
  margin: 0 0 8px 0;
  font-size: 1.1rem;
}

.feature-card p {
  color: var(--vp-c-text-2);
  margin: 0;
  font-size: 0.9rem;
  line-height: 1.5;
}
</style>
