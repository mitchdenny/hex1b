# Guide

Hex1b is a comprehensive .NET terminal application stack. Whether you're building rich text-based user interfaces, need a programmable terminal emulator, want to test terminal applications, or integrate AI agents with terminal sessions, Hex1b has you covered.

## Architecture

<div class="architecture-diagram">
<svg viewBox="0 0 500 520" xmlns="http://www.w3.org/2000/svg">
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
  <rect x="20" y="10" width="130" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="85" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Console</text>
  <text x="85" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">Native Terminal</text>
  
  <rect x="185" y="10" width="130" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="250" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Web</text>
  <text x="250" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">xterm.js</text>
  
  <rect x="350" y="10" width="130" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="415" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Headless</text>
  <text x="415" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">Testing</text>
  
  <!-- Lines from Presentation Adapters to top outputs -->
  <path d="M130 110 L130 95 Q130 85 110 85 L85 85 L85 60" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  <path d="M250 110 L250 60" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  <path d="M370 110 L370 95 Q370 85 390 85 L415 85 L415 60" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  
  <!-- Arrow heads pointing up (at y=60, top boxes end) -->
  <polygon points="85,60 80,68 90,68" fill="#4ecdc4"/>
  <polygon points="250,60 245,68 255,68" fill="#4ecdc4"/>
  <polygon points="415,60 410,68 420,68" fill="#4ecdc4"/>
  
  <!-- Presentation Adapters Box (y=110, height=60) -->
  <rect x="50" y="110" width="400" height="60" rx="10" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.7)" stroke-width="1.5"/>
  <text x="250" y="138" text-anchor="middle" fill="#fff" font-family="monospace" font-size="15" font-weight="bold">Presentation Adapters</text>
  <text x="250" y="156" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="10">IHex1bTerminalPresentationAdapter</text>
  
  <!-- Connection line from Terminal to Presentation Adapters -->
  <path d="M250 220 L250 170" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  <polygon points="250,170 245,178 255,178" fill="#4ecdc4"/>
  
  <!-- Central Terminal Box (y=220, height=80) -->
  <rect x="125" y="220" width="250" height="80" rx="12" fill="url(#boxGrad)" stroke="#4ecdc4" stroke-width="2" filter="url(#glow)"/>
  <text x="250" y="255" text-anchor="middle" fill="#4ecdc4" font-family="monospace" font-size="18" font-weight="bold">Hex1bTerminal</text>
  <text x="250" y="280" text-anchor="middle" fill="rgba(255,255,255,0.6)" font-family="sans-serif" font-size="12">Terminal Emulator Core</text>
  
  <!-- Connection line from Workload Adapters to Terminal -->
  <path d="M250 350 L250 300" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  <polygon points="250,300 245,308 255,308" fill="#4ecdc4"/>
  
  <!-- Workload Adapters Box (y=350, height=60) -->
  <rect x="50" y="350" width="400" height="60" rx="10" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.7)" stroke-width="1.5"/>
  <text x="250" y="378" text-anchor="middle" fill="#fff" font-family="monospace" font-size="15" font-weight="bold">Workload Adapters</text>
  <text x="250" y="396" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="10">IHex1bTerminalWorkloadAdapter</text>
  
  <!-- Connection lines from bottom to Workload Adapters -->
  <path d="M85 460 L85 445 Q85 435 105 435 L130 435 L130 410" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  <path d="M250 460 L250 410" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  <path d="M415 460 L415 445 Q415 435 395 435 L370 435 L370 410" stroke="#4ecdc4" stroke-width="2" fill="none"/>
  
  <!-- Arrow heads pointing to Workload Adapters (at y=410, workload adapters end) -->
  <polygon points="130,410 125,418 135,418" fill="#4ecdc4"/>
  <polygon points="250,410 245,418 255,418" fill="#4ecdc4"/>
  <polygon points="370,410 365,418 375,418" fill="#4ecdc4"/>
  
  <!-- Hex1bApp Box (y=460, height=50) -->
  <rect x="20" y="460" width="130" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="85" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Hex1bApp</text>
  <text x="85" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">TUI Framework</text>
  
  <!-- Shells Box -->
  <rect x="185" y="460" width="130" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="250" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Shells</text>
  <text x="250" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">bash, pwsh, zsh</text>
  
  <!-- Other Processes Box -->
  <rect x="350" y="460" width="130" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5"/>
  <text x="415" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Processes</text>
  <text x="415" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">htop, vim, any CLI</text>
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
