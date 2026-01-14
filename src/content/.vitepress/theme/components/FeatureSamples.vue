<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { codeToHtml } from 'shiki'
import FloatingTerminal from './FloatingTerminal.vue'

interface FeatureSample {
  id: string
  title: string
  description: string
  feature: string
  example: string
  code: string
}

const highlightedCode = ref<Record<string, string>>({})

const samples: FeatureSample[] = [
  {
    id: 'declarative',
    title: 'A simple API for describing TUIs',
    description: 'Describe your TUI app using a simple expressive API. Hex1b takes care of the complexity of layout. It\`s a bit like React but for TUIs!',
    example: 'minimal',
    code: `var clickCount = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Hello, Hex1b!"),
        v.Text($"Clicks: {clickCount}"),
        v.Button("Click me", _ => clickCount++)
    ]))
    .Build();

await terminal.RunAsync();`
  },
  {
    id: 'layout',
    title: 'Layout your user interface',
    description: 'Layout your UI using flexible containers like vertical and horizontal stacks and let Hex1b take care of exact positioning and responding to terminal size changes.',
    example: 'todo',
    code: `var items = new List<(string Text, bool Done)>
{
    ("Learn Hex1b", true),
    ("Build a TUI", false)
};

IReadOnlyList<string> Format() =>
    items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.HStack(h => [
            h.Text("New task: "),
            h.TextBox(newItem, e => newItem = e.NewText),
            h.Button("Add", _ => items.Add((newItem, false)))
        ]),
        new SeparatorWidget(),
        b.List(Format(), null, e =>
            items[e.ActivatedIndex] = (items[e.ActivatedIndex].Text, !items[e.ActivatedIndex].Done))
    ], title: "📋 Todo"))
    .Build();

await terminal.RunAsync();`
  },
  {
    id: 'input',
    title: 'Interactive Input',
    description: 'Built-in text input, keyboard navigation, and focus management. Tab between controls, type in text boxes, and respond to key events.',
    example: 'text-input',
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
    example: 'theming',
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

const copiedId = ref<string | null>(null)

async function copyCode(sampleId: string, code: string) {
  await navigator.clipboard.writeText(code)
  copiedId.value = sampleId
  setTimeout(() => {
    if (copiedId.value === sampleId) {
      copiedId.value = null
    }
  }, 2000)
}

async function highlightSamples() {
  for (const sample of samples) {
    highlightedCode.value[sample.id] = await codeToHtml(sample.code, {
      lang: 'csharp',
      theme: 'github-dark'
    })
  }
}

onMounted(() => {
  checkBackend()
  highlightSamples()
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
        <h3 class="sample-title">{{ sample.title }}</h3>
        <p class="sample-description">{{ sample.description }}</p>
        <ClientOnly>
          <button 
            v-if="backendAvailable"
            class="demo-button" 
            @click="openDemo(sample.id)"
          >
            <span class="demo-icon-box">
              <svg class="terminal-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="4 17 10 11 4 5"></polyline>
                <line x1="12" y1="19" x2="20" y2="19"></line>
              </svg>
            </span>
            <span class="demo-text">Open terminal</span>
          </button>
        </ClientOnly>
      </div>
      
      <div class="code-container">
        <div class="code-header">
          <span class="code-lang">C#</span>
          <button class="copy-button" @click="copyCode(sample.id, sample.code)" :title="copiedId === sample.id ? 'Copied!' : 'Copy code'">
            <svg v-if="copiedId !== sample.id" class="copy-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
            <svg v-else class="copy-icon check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
          </button>
        </div>
        <div 
          v-if="highlightedCode[sample.id]" 
          class="code-block highlighted"
          v-html="highlightedCode[sample.id]"
        ></div>
        <pre v-else class="code-block"><code>{{ sample.code }}</code></pre>
      </div>
      
      <!-- Hidden FloatingTerminal for each sample -->
      <ClientOnly>
        <div v-if="backendAvailable" class="hidden-terminal">
          <FloatingTerminal 
            :ref="(el: any) => setTerminalRef(sample.id, el)"
            :example="sample.example"
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
  border: 3px solid var(--vp-c-brand-1);
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
  justify-content: flex-start;
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
  align-items: stretch;
  width: fit-content;
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
  border: none;
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.2s ease;
  margin-top: 8px;
  padding: 0;
}

.demo-button:hover {
  background: linear-gradient(135deg, #5fd9d1 0%, #4ecdc4 100%);
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(78, 205, 196, 0.35);
}

.demo-button:active {
  transform: translateY(0);
}

.demo-icon-box {
  display: flex;
  align-items: center;
  justify-content: center;
  background: #0f0f1a;
  padding: 12px;
  width: 44px;
  height: 44px;
  box-sizing: border-box;
}

.terminal-icon {
  width: 20px;
  height: 20px;
  color: #4ecdc4;
}

.demo-text {
  display: flex;
  align-items: center;
  padding: 12px 20px;
  font-size: 15px;
  font-weight: 600;
  color: #0f0f1a;
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

.copy-button {
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: none;
  padding: 4px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.copy-button:hover {
  background: rgba(255, 255, 255, 0.1);
}

.copy-icon {
  width: 16px;
  height: 16px;
  color: rgba(255, 255, 255, 0.5);
  transition: color 0.2s ease;
}

.copy-button:hover .copy-icon {
  color: rgba(255, 255, 255, 0.8);
}

.copy-icon.check {
  color: #4ecdc4;
}

.code-block {
  margin: 0;
  padding: 20px;
  overflow-x: auto;
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 13px;
  line-height: 1.0;
  color: #e0e0e0;
  flex: 1;
}

.code-block code {
  white-space: pre;
}

.code-block.highlighted :deep(pre) {
  margin: 0;
  padding: 0;
  background: transparent !important;
  counter-reset: line;
}

.code-block.highlighted :deep(code) {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 13px;
  line-height: 1.0;
  display: block;
}

.code-block.highlighted :deep(code .line) {
  display: block;
}

.code-block.highlighted :deep(code .line::before) {
  counter-increment: line;
  content: counter(line);
  display: inline-block;
  width: 2em;
  margin-right: 12px;
  padding-right: 8px;
  text-align: right;
  color: rgba(255, 255, 255, 0.3);
  border-right: 1px solid rgba(255, 255, 255, 0.1);
}

/* Custom scrollbar styling for code blocks */
.code-block::-webkit-scrollbar {
  height: 8px;
}

.code-block::-webkit-scrollbar-track {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 4px;
}

.code-block::-webkit-scrollbar-thumb {
  background: rgba(78, 205, 196, 0.4);
  border-radius: 4px;
}

.code-block::-webkit-scrollbar-thumb:hover {
  background: rgba(78, 205, 196, 0.6);
}

/* Firefox scrollbar */
.code-block {
  scrollbar-width: thin;
  scrollbar-color: rgba(78, 205, 196, 0.4) rgba(0, 0, 0, 0.2);
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
