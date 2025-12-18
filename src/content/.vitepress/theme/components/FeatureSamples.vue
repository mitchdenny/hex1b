<script setup lang="ts">
import { ref, onMounted } from 'vue'
import FloatingTerminal from './FloatingTerminal.vue'

interface FeatureSample {
  id: string
  title: string
  description: string
  feature: string
  exhibit: string
  code: string
}

const samples: FeatureSample[] = [
  {
    id: 'declarative',
    title: 'Declarative UI',
    description: 'Describe your UI as a tree of widgets. Hex1b handles rendering, state management, and reconciliation automatically.',
    feature: '🎯 Declarative API',
    exhibit: 'hello-world',
    code: `var ctx = new RootContext();

var widget = ctx.VStack(v => [
    v.Text("Hello, Hex1b!"),
    v.Text($"Click count: {state.ClickCount}"),
    v.Button("Click me!", _ => state.ClickCount++)
]);`
  },
  {
    id: 'layout',
    title: 'Flexible Layouts',
    description: 'Build complex UIs with HStack, VStack, Splitter, and constraint-based sizing. Responsive layouts that adapt to terminal size.',
    feature: '📐 Layout System',
    exhibit: 'layout',
    code: `ctx.Splitter(
    ctx.Panel(left => [
        left.List(items, onSelect, null)
    ]),
    ctx.Panel(right => [
        right.VStack(v => [
            v.Text("Content area"),
            v.Border(b => [...], title: "Nested")
        ])
    ]),
    leftWidth: 22
)`
  },
  {
    id: 'input',
    title: 'Interactive Input',
    description: 'Built-in text input, keyboard navigation, and focus management. Tab between controls, type in text boxes, and respond to key events.',
    feature: '⌨️ Smart Input',
    exhibit: 'text-input',
    code: `ctx.VStack(v => [
    v.Text("Enter your name:"),
    v.TextBox(
        state.Name,
        args => state.Name = args.NewText
    ),
    v.Text($"Hello, {state.Name}!")
])`
  },
  {
    id: 'theming',
    title: 'Theming Support',
    description: 'Customize colors and styles with a powerful theming system. Switch themes dynamically and create your own custom themes.',
    feature: '🎨 Theming',
    exhibit: 'theming',
    code: `new Hex1bTheme("Custom")
    .Set(ButtonTheme.FocusedBackgroundColor, 
         Hex1bColor.FromRgb(34, 139, 34))
    .Set(BorderTheme.BorderColor, 
         Hex1bColor.FromRgb(78, 205, 196))
    .Set(ListTheme.SelectedIndicator, "→ ")`
  }
]

// Store refs to FloatingTerminal instances by sample id
const terminalRefs = ref<Record<string, InstanceType<typeof FloatingTerminal> | null>>({})
const backendAvailable = ref(false)

async function checkBackend() {
  try {
    const response = await fetch('/apps')
    backendAvailable.value = response.ok
  } catch {
    backendAvailable.value = false
  }
}

function setTerminalRef(id: string, el: any) {
  terminalRefs.value[id] = el
}

function openDemo(sampleId: string) {
  const terminal = terminalRefs.value[sampleId]
  if (terminal) {
    terminal.openTerminal()
  }
}

onMounted(() => {
  checkBackend()
})
</script>

<template>
  <div class="feature-samples">
    <div 
      v-for="(sample, index) in samples" 
      :key="sample.id" 
      class="sample-card"
      :class="{ 'reversed': index % 2 === 1 }"
    >
      <div class="sample-info">
        <span class="feature-badge">{{ sample.feature }}</span>
        <h3 class="sample-title">{{ sample.title }}</h3>
        <p class="sample-description">{{ sample.description }}</p>
        <ClientOnly>
          <button 
            v-if="backendAvailable"
            class="demo-button" 
            @click="openDemo(sample.id)"
          >
            <span class="demo-icon">▶</span>
            Live Demo
          </button>
        </ClientOnly>
      </div>
      
      <div class="code-container">
        <div class="code-header">
          <span class="code-lang">C#</span>
        </div>
        <pre class="code-block"><code>{{ sample.code }}</code></pre>
      </div>
      
      <!-- Hidden FloatingTerminal for each sample -->
      <ClientOnly>
        <div v-if="backendAvailable" class="hidden-terminal">
          <FloatingTerminal 
            :ref="(el: any) => setTerminalRef(sample.id, el)"
            :exhibit="sample.exhibit"
            :title="sample.title"
          />
        </div>
      </ClientOnly>
    </div>
  </div>
</template>

<style scoped>
.feature-samples {
  display: flex;
  flex-direction: column;
  gap: 32px;
  margin: 32px 0;
}

.sample-card {
  display: grid;
  grid-template-columns: 40% 60%;
  background: var(--vp-c-bg-soft);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  overflow: hidden;
  transition: all 0.3s ease;
  align-items: stretch;
}

.sample-card:hover {
  border-color: var(--vp-c-brand-1);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
}

.sample-card.reversed {
  grid-template-columns: 60% 40%;
}

.sample-card.reversed .sample-info {
  order: 2;
}

.sample-card.reversed .code-container {
  order: 1;
}

.sample-info {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 32px;
  justify-content: center;
}

.feature-badge {
  display: inline-block;
  width: fit-content;
  font-size: 12px;
  font-weight: 600;
  color: var(--vp-c-brand-1);
  background: var(--vp-c-brand-soft);
  padding: 6px 14px;
  border-radius: 20px;
}

.sample-title {
  font-size: 24px;
  font-weight: 700;
  color: var(--vp-c-text-1);
  margin: 0;
  line-height: 1.3;
}

.sample-description {
  color: var(--vp-c-text-2);
  font-size: 15px;
  line-height: 1.7;
  margin: 0;
}

.demo-button {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  width: fit-content;
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
  color: #0f0f1a;
  border: none;
  padding: 12px 24px;
  border-radius: 8px;
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  margin-top: 8px;
}

.demo-button:hover {
  background: linear-gradient(135deg, #5fd9d1 0%, #4ecdc4 100%);
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(78, 205, 196, 0.35);
}

.demo-button:active {
  transform: translateY(0);
}

.demo-icon {
  font-size: 12px;
}

.code-container {
  background: #1a1a2e;
  display: flex;
  flex-direction: column;
  min-height: 100%;
}

.code-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background: rgba(0, 0, 0, 0.3);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.code-lang {
  font-size: 12px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.5);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.code-block {
  margin: 0;
  padding: 20px;
  overflow-x: auto;
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #e0e0e0;
  flex: 1;
  display: flex;
  align-items: center;
}

.code-block code {
  white-space: pre;
}

/* Completely hide the terminal trigger cards */
.hidden-terminal {
  position: absolute;
  width: 0;
  height: 0;
  overflow: hidden;
  pointer-events: none;
}

.hidden-terminal :deep(.terminal-trigger) {
  display: none !important;
}

/* Responsive: stack on smaller screens */
@media (max-width: 768px) {
  .sample-card,
  .sample-card.reversed {
    grid-template-columns: 1fr;
  }
  
  .sample-card.reversed .sample-info,
  .sample-card.reversed .code-container {
    order: unset;
  }
  
  .sample-info {
    text-align: center;
    align-items: center;
  }
  
  .code-container {
    border-radius: 0;
  }
  
  .demo-button {
    align-self: center;
  }
}
</style>
