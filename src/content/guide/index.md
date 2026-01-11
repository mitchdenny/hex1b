# Guide

Hex1b is a comprehensive .NET terminal application stack. Whether you're building rich text-based user interfaces, need a programmable terminal emulator, want to test terminal applications, or integrate AI agents with terminal sessions, Hex1b has you covered.

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
  text-decoration: none;
  transition: all 0.2s ease;
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
