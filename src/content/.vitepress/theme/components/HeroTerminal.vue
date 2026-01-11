<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

// Typing animation state
const lines = [
  { text: '$ dotnet new console -n MyApp', type: 'command', delay: 0 },
  { text: '$ cd MyApp && dotnet add package Hex1b', type: 'command', delay: 800 },
  { text: '', type: 'blank', delay: 1600 },
  { text: '// Program.cs', type: 'comment', delay: 1800 },
  { text: 'using Hex1b;', type: 'code', delay: 2000 },
  { text: '', type: 'blank', delay: 2200 },
  { text: 'var app = new Hex1bApp(ctx =>', type: 'code', delay: 2400 },
  { text: '    ctx.Border(b => [', type: 'code', delay: 2600 },
  { text: '        b.Text("Hello, Terminal!"),', type: 'code', delay: 2800 },
  { text: '        b.Button("Click me!")', type: 'code', delay: 3000 },
  { text: '    ], title: "My App")', type: 'code', delay: 3200 },
  { text: ');', type: 'code', delay: 3400 },
  { text: '', type: 'blank', delay: 3600 },
  { text: 'await app.RunAsync();', type: 'code', delay: 3800 },
]

const visibleLines = ref<typeof lines>([])
const showCursor = ref(true)
let timeouts: number[] = []
let cursorInterval: number | null = null

onMounted(() => {
  // Animate lines appearing
  lines.forEach((line, index) => {
    const timeout = window.setTimeout(() => {
      visibleLines.value = [...visibleLines.value, line]
    }, line.delay)
    timeouts.push(timeout)
  })
  
  // Blinking cursor
  cursorInterval = window.setInterval(() => {
    showCursor.value = !showCursor.value
  }, 530)
})

onUnmounted(() => {
  timeouts.forEach(t => clearTimeout(t))
  if (cursorInterval) clearInterval(cursorInterval)
})
</script>

<template>
  <div class="hero-terminal">
    <div class="terminal-chrome">
      <div class="terminal-header">
        <div class="terminal-buttons">
          <span class="btn close"></span>
          <span class="btn minimize"></span>
          <span class="btn maximize"></span>
        </div>
        <div class="terminal-title">hex1b — bash</div>
        <div class="terminal-spacer"></div>
      </div>
      <div class="terminal-body">
        <div class="terminal-content">
          <div 
            v-for="(line, index) in visibleLines" 
            :key="index"
            class="terminal-line"
            :class="line.type"
          >
            <span v-if="line.type === 'command'" class="prompt">{{ line.text }}</span>
            <span v-else-if="line.type === 'comment'" class="comment">{{ line.text }}</span>
            <span v-else-if="line.type === 'code'" class="code">{{ line.text }}</span>
            <span v-else>&nbsp;</span>
          </div>
          <div class="terminal-line cursor-line">
            <span class="prompt">$</span>
            <span class="cursor" :class="{ visible: showCursor }">▊</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hero-terminal {
  max-width: 600px;
  margin: 32px auto 0;
  perspective: 1000px;
}

.terminal-chrome {
  background: linear-gradient(180deg, #2d2d3a 0%, #1e1e2a 100%);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 
    0 25px 50px -12px rgba(0, 0, 0, 0.5),
    0 0 0 1px rgba(255, 255, 255, 0.1),
    inset 0 1px 0 rgba(255, 255, 255, 0.1);
  transform: rotateX(2deg);
  transition: transform 0.3s ease;
}

.terminal-chrome:hover {
  transform: rotateX(0deg) translateY(-4px);
}

.terminal-header {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  background: linear-gradient(180deg, #3d3d4a 0%, #2d2d3a 100%);
  border-bottom: 1px solid rgba(0, 0, 0, 0.3);
}

.terminal-buttons {
  display: flex;
  gap: 8px;
}

.terminal-buttons .btn {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  transition: opacity 0.2s;
}

.terminal-buttons .btn.close {
  background: linear-gradient(180deg, #ff6058 0%, #e74c3c 100%);
  box-shadow: inset 0 -1px 0 rgba(0, 0, 0, 0.2);
}

.terminal-buttons .btn.minimize {
  background: linear-gradient(180deg, #ffbd2e 0%, #f39c12 100%);
  box-shadow: inset 0 -1px 0 rgba(0, 0, 0, 0.2);
}

.terminal-buttons .btn.maximize {
  background: linear-gradient(180deg, #28ca42 0%, #27ae60 100%);
  box-shadow: inset 0 -1px 0 rgba(0, 0, 0, 0.2);
}

.terminal-title {
  flex: 1;
  text-align: center;
  font-size: 13px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.6);
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.terminal-spacer {
  width: 52px;
}

.terminal-body {
  background: #0a0a12;
  padding: 16px 20px;
  min-height: 280px;
}

.terminal-content {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', 'SF Mono', Menlo, monospace;
  font-size: 13px;
  line-height: 1.6;
}

.terminal-line {
  white-space: pre;
  min-height: 1.6em;
}

.terminal-line .prompt {
  color: #4ecdc4;
}

.terminal-line .comment {
  color: #6a6a8a;
  font-style: italic;
}

.terminal-line .code {
  color: #e0e0e0;
}

.cursor-line {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cursor {
  color: #4ecdc4;
  opacity: 0;
  transition: opacity 0.1s;
}

.cursor.visible {
  opacity: 1;
}

/* Responsive */
@media (max-width: 700px) {
  .hero-terminal {
    margin: 24px 16px 0;
  }
  
  .terminal-body {
    padding: 12px 16px;
    min-height: 240px;
  }
  
  .terminal-content {
    font-size: 11px;
  }
}
</style>
