# Guide

Hex1b is a comprehensive .NET terminal application stack. Whether you're building rich text-based user interfaces, need a programmable terminal emulator, want to test terminal applications, or integrate AI agents with terminal sessions, Hex1b has you covered.

## Architecture

Hex1b is built around **Hex1bTerminal**, a pluggable terminal emulator at the core of the stack. On one side, **workload adapters** connect the terminal to data sources—whether that's a real shell with PTY, a child process, a network stream, or Hex1b's own TUI framework. On the other side, **presentation adapters** connect to display targets like native terminals, xterm.js in the browser, or a headless buffer for testing.

Sitting alongside the terminal core are tools for automation: **input sequencers** send scripted keystrokes with wait conditions, while **pattern matchers** search the 2D screen buffer for text, colors, and attributes—think regex, but for terminal cells. **Filters** intercept the data flow for tasks like render optimization or recording sessions to Asciinema files.

Hover over the diagram below to explore each component.

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
    <linearGradient id="boxGradHover" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" style="stop-color:#2a2a4e;stop-opacity:1" />
      <stop offset="100%" style="stop-color:#1a1a2e;stop-opacity:1" />
    </linearGradient>
    <filter id="glow">
      <feGaussianBlur stdDeviation="2" result="coloredBlur"/>
      <feMerge>
        <feMergeNode in="coloredBlur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
    <filter id="glowHover">
      <feGaussianBlur stdDeviation="4" result="coloredBlur"/>
      <feMerge>
        <feMergeNode in="coloredBlur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  
  <!-- Top row: Presentation Adapter outputs (y=10, height=50) -->
  <g class="arch-tile" data-component="console">
    <rect x="65" y="10" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="120" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Console</text>
    <text x="120" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">Native Terminal</text>
  </g>
  
  <g class="arch-tile" data-component="web">
    <rect x="205" y="10" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="260" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Web</text>
    <text x="260" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">xterm.js</text>
  </g>
  
  <g class="arch-tile" data-component="headless">
    <rect x="345" y="10" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="400" y="35" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Headless</text>
    <text x="400" y="50" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">Testing</text>
  </g>
  
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
  <g class="arch-tile" data-component="presentation-adapters">
    <rect x="60" y="110" width="400" height="60" rx="10" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.7)" stroke-width="1.5" class="tile-bg"/>
    <text x="260" y="138" text-anchor="middle" fill="#fff" font-family="monospace" font-size="15" font-weight="bold">Presentation Adapters</text>
    <text x="260" y="156" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="10">IHex1bTerminalPresentationAdapter</text>
  </g>
  
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
  <g class="arch-tile" data-component="hex1b-terminal">
    <rect x="135" y="220" width="250" height="80" rx="12" fill="url(#boxGrad)" stroke="#4ecdc4" stroke-width="2" filter="url(#glow)" class="tile-bg tile-bg-core"/>
    <text x="260" y="255" text-anchor="middle" fill="#4ecdc4" font-family="monospace" font-size="18" font-weight="bold">Hex1bTerminal</text>
    <text x="260" y="280" text-anchor="middle" fill="rgba(255,255,255,0.6)" font-family="sans-serif" font-size="12">Terminal Emulator Core</text>
  </g>
  
  <!-- Input Sequencer (top left of Terminal) -->
  <g class="arch-tile" data-component="input-sequencer">
    <rect x="5" y="200" width="100" height="45" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="55" y="220" text-anchor="middle" fill="#fff" font-family="monospace" font-size="9" font-weight="bold">Input Sequencer</text>
    <text x="55" y="233" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="7">Keyboard/Mouse</text>
  </g>
  
  <!-- Pattern Searcher (bottom left of Terminal) -->
  <g class="arch-tile" data-component="pattern-searcher">
    <rect x="5" y="275" width="100" height="45" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="55" y="295" text-anchor="middle" fill="#fff" font-family="monospace" font-size="9" font-weight="bold">Pattern Searcher</text>
    <text x="55" y="308" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="7">Cell Content</text>
  </g>
  
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
  <g class="arch-tile" data-component="workload-adapters">
    <rect x="60" y="350" width="400" height="60" rx="10" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.7)" stroke-width="1.5" class="tile-bg"/>
    <text x="260" y="378" text-anchor="middle" fill="#fff" font-family="monospace" font-size="15" font-weight="bold">Workload Adapters</text>
    <text x="260" y="396" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="10">IHex1bTerminalWorkloadAdapter</text>
  </g>
  
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
  <g class="arch-tile" data-component="hex1b-app">
    <rect x="65" y="460" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="120" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Hex1bApp</text>
    <text x="120" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">TUI Framework</text>
  </g>
  
  <!-- Shells Box -->
  <g class="arch-tile" data-component="shells">
    <rect x="205" y="460" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="260" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Shells</text>
    <text x="260" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">bash, pwsh, zsh</text>
  </g>
  
  <!-- Other Processes Box -->
  <g class="arch-tile" data-component="processes">
    <rect x="345" y="460" width="110" height="50" rx="8" fill="url(#boxGrad)" stroke="rgba(78,205,196,0.5)" stroke-width="1.5" class="tile-bg"/>
    <text x="400" y="485" text-anchor="middle" fill="#fff" font-family="monospace" font-size="12" font-weight="bold">Processes</text>
    <text x="400" y="500" text-anchor="middle" fill="rgba(255,255,255,0.5)" font-family="sans-serif" font-size="9">htop, vim, any CLI</text>
  </g>
</svg>

<div class="arch-info-panel" id="arch-info-panel">
  <div class="arch-info-default">
    <span class="arch-info-hint">👆 Hover over any component to learn more, click to lock</span>
  </div>
  <div class="arch-info-content" id="arch-info-content" style="display: none;">
    <h4 id="arch-info-title"></h4>
    <p id="arch-info-description"></p>
    <a id="arch-info-link" href="#" class="arch-info-link">Learn more →</a>
  </div>
</div>
</div>

<script setup>
import { onMounted } from 'vue'

const componentInfo = {
  'console': {
    title: 'Console Presentation',
    description: 'Connects Hex1bTerminal to native terminal emulators like GNOME Terminal, Windows Terminal, or iTerm2. Uses standard input/output streams and ANSI escape sequences for rendering.',
    link: '/guide/terminal-emulator#console-adapter'
  },
  'web': {
    title: 'Web Presentation (xterm.js)',
    description: 'Enables browser-based terminal interfaces using xterm.js. Perfect for building web-based developer tools, SSH clients, or cloud IDE terminal panels.',
    link: '/guide/terminal-emulator#web-adapter'
  },
  'headless': {
    title: 'Headless Presentation',
    description: 'A virtual terminal for testing and automation. Captures all output without displaying it, enabling programmatic assertions on terminal content in unit tests.',
    link: '/guide/testing'
  },
  'presentation-adapters': {
    title: 'Presentation Adapters',
    description: 'The abstraction layer that connects Hex1bTerminal to any display target. Implement IHex1bTerminalPresentationAdapter to create custom presentation targets like web sockets, SSH connections, or file logging.',
    link: '/guide/terminal-emulator#presentation-adapters'
  },
  'hex1b-terminal': {
    title: 'Hex1bTerminal',
    description: 'The core terminal emulator that processes ANSI escape sequences, manages screen buffer state, cursor positioning, colors, and text attributes. This is the heart of the Hex1b stack.',
    link: '/guide/terminal-emulator'
  },
  'input-sequencer': {
    title: 'Input Sequencer',
    description: 'Automates keyboard and mouse input to the terminal. Define sequences of keystrokes with wait conditions to script complex terminal interactions for testing or automation.',
    link: '/guide/testing#input-sequencer'
  },
  'pattern-searcher': {
    title: 'Pattern Searcher',
    description: 'A 2D pattern matching engine for terminal screens. Search for text, colors, attributes, or complex patterns across the terminal buffer. Think regex, but for terminal cells.',
    link: '/guide/testing#pattern-matching'
  },
  'workload-adapters': {
    title: 'Workload Adapters',
    description: 'Connect any process or data source to the terminal. Implement IHex1bTerminalWorkloadAdapter to pipe data from shells, child processes, network streams, or the Hex1bApp TUI framework.',
    link: '/guide/terminal-emulator#workload-adapters'
  },
  'hex1b-app': {
    title: 'Hex1bApp TUI Framework',
    description: 'A React-inspired declarative framework for building terminal user interfaces. Compose widgets, handle input, manage focus, and render rich layouts with a familiar component model.',
    link: '/guide/tui'
  },
  'shells': {
    title: 'Shell Processes',
    description: 'Run real shell processes (bash, PowerShell, zsh) through Hex1bTerminal with full PTY support. Capture output, send input, and automate shell interactions.',
    link: '/guide/terminal-emulator#child-processes'
  },
  'processes': {
    title: 'External Processes',
    description: 'Host any CLI application—htop, vim, tmux, or custom tools—inside Hex1bTerminal. Full support for cursor-addressed applications and alternate screen buffers.',
    link: '/guide/terminal-emulator#child-processes'
  }
}

onMounted(() => {
  const tiles = document.querySelectorAll('.arch-tile')
  const infoPanel = document.getElementById('arch-info-panel')
  const infoContent = document.getElementById('arch-info-content')
  const infoDefault = document.querySelector('.arch-info-default')
  const infoTitle = document.getElementById('arch-info-title')
  const infoDescription = document.getElementById('arch-info-description')
  const infoLink = document.getElementById('arch-info-link')
  const diagram = document.querySelector('.architecture-diagram')

  if (!tiles.length || !infoPanel) return

  let lockedComponent = null

  function showInfo(component, tile) {
    const info = componentInfo[component]
    if (info) {
      infoTitle.textContent = info.title
      infoDescription.textContent = info.description
      infoLink.href = info.link
      infoDefault.style.display = 'none'
      infoContent.style.display = 'block'
      infoPanel.classList.add('active')
      
      tiles.forEach(t => t.classList.remove('highlighted'))
      tile.classList.add('highlighted')
    }
  }

  tiles.forEach(tile => {
    tile.addEventListener('mouseenter', function() {
      if (lockedComponent) return
      showInfo(this.dataset.component, this)
    })

    tile.addEventListener('mouseleave', function() {
      if (lockedComponent) return
      this.classList.remove('highlighted')
    })

    tile.addEventListener('click', function() {
      const component = this.dataset.component
      
      if (lockedComponent === component) {
        // Clicking the locked tile unlocks it
        lockedComponent = null
        tiles.forEach(t => t.classList.remove('locked'))
        infoPanel.classList.remove('locked')
      } else {
        // Lock to this tile
        lockedComponent = component
        tiles.forEach(t => t.classList.remove('locked', 'highlighted'))
        this.classList.add('locked', 'highlighted')
        infoPanel.classList.add('locked')
        showInfo(component, this)
      }
    })
  })

  if (diagram) {
    diagram.addEventListener('mouseleave', function() {
      if (lockedComponent) return
      infoDefault.style.display = 'block'
      infoContent.style.display = 'none'
      infoPanel.classList.remove('active')
      tiles.forEach(t => t.classList.remove('highlighted'))
    })
  }
})
</script>

## Features

- **[Terminal User Interfaces](/guide/tui)** — Build rich, interactive TUIs with a React-inspired declarative API for dashboards, dev tools, and CLI experiences.
- **[Terminal Emulator](/guide/terminal-emulator)** — Embed a programmable terminal emulator in your .NET applications to host shells and run commands.
- **[Automation & Testing](/guide/testing)** — Test terminal applications programmatically with input sequencing, pattern matching, and CI/CD integration.
- **[MCP Server](/guide/mcp-server)** — Expose terminal sessions to AI agents via the Model Context Protocol for LLM-driven automation.

## Quick Start

New to Hex1b? Start here:

1. **[Your First App](/guide/getting-started)** — Install Hex1b and build your first app
2. **[Widgets & Nodes](/guide/widgets-and-nodes)** — Understand the core architecture
3. **[Widget Reference](/guide/widgets/)** — Explore all available widgets

## Building TUIs

Once you're comfortable with the basics, dive deeper:

- **[Layout System](/guide/layout)** — Master constraint-based layouts with `HStack`, `VStack`, and more
- **[Input Handling](/guide/input)** — Keyboard navigation, focus management, and shortcuts
- **[Theming](/guide/theming)** — Customize colors, borders, and styles

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

/* Interactive tile styles */
.arch-tile {
  cursor: pointer;
  transition: all 0.2s ease;
}

.arch-tile .tile-bg {
  transition: all 0.2s ease;
}

.arch-tile:hover .tile-bg,
.arch-tile.highlighted .tile-bg {
  fill: url(#boxGradHover);
  stroke: #4ecdc4;
  stroke-width: 2;
}

.arch-tile.locked .tile-bg {
  fill: url(#boxGradHover);
  stroke: #4ecdc4;
  stroke-width: 2.5;
}

.arch-tile:hover .tile-bg-core,
.arch-tile.highlighted .tile-bg-core {
  filter: url(#glowHover);
  stroke-width: 3;
}

.arch-tile.locked .tile-bg-core {
  filter: url(#glowHover);
  stroke-width: 3.5;
}

/* Info panel styles */
.arch-info-panel {
  margin-top: 16px;
  padding: 16px 20px;
  background: rgba(26, 26, 46, 0.8);
  border-radius: 12px;
  border: 1px solid rgba(78, 205, 196, 0.3);
  min-height: 80px;
  transition: all 0.3s ease;
}

.arch-info-panel.active {
  border-color: rgba(78, 205, 196, 0.6);
  background: rgba(26, 26, 46, 0.95);
}

.arch-info-panel.locked {
  border-color: #4ecdc4;
  box-shadow: 0 0 12px rgba(78, 205, 196, 0.3);
}

.arch-info-panel.locked .arch-info-content h4::after {
  content: ' 🔒';
  font-size: 12px;
}

.arch-info-default {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 48px;
}

.arch-info-hint {
  color: rgba(255, 255, 255, 0.5);
  font-size: 14px;
  font-style: italic;
}

.arch-info-content h4 {
  margin: 0 0 8px 0;
  color: #4ecdc4;
  font-family: monospace;
  font-size: 16px;
  font-weight: bold;
}

.arch-info-content p {
  margin: 0 0 12px 0;
  color: rgba(255, 255, 255, 0.8);
  font-size: 14px;
  line-height: 1.6;
}

.arch-info-link {
  display: inline-block;
  color: #4ecdc4;
  font-size: 14px;
  font-weight: 500;
  text-decoration: none;
  transition: all 0.2s ease;
}

.arch-info-link:hover {
  color: #fff;
  text-decoration: none;
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
</style>
