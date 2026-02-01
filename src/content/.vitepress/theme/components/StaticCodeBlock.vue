<script setup lang="ts">
import { ref, onMounted, watch, useSlots, nextTick, type VNode } from 'vue'
import { codeToHtml } from 'shiki'
import { useData } from 'vitepress'

/**
 * StaticCodeBlock - A code block with an attached static ANSI terminal preview.
 * 
 * Use this component for documentation code snippets that show:
 * 1. The source code (static, syntax-highlighted)
 * 2. The terminal output (static ANSI rendering)
 * 
 * This is ideal for partial code snippets (not full runnable examples)
 * where you want to show what the output looks like without interactivity.
 * 
 * Props:
 *   - code: Optional inline code (alternative to slot content)
 *   - lang: Language for syntax highlighting (default: "csharp")
 *   - title: Title for the code block header (default: "C#")
 *   - ansiFile: Path to the .ansi file relative to /public (e.g., "ansi/text-wrap.ansi")
 *   - terminalTitle: Title for the terminal header (default: "Output")
 *   - cols: Terminal width (default: 60)
 *   - rows: Terminal height (default: 6)
 */
const { isDark } = useData()

const props = defineProps<{
  code?: string
  lang?: string
  title?: string
  ansiFile: string
  terminalTitle?: string
  cols?: number
  rows?: number
}>()

const slots = useSlots()
const highlightedCode = ref<string>('')
const copied = ref(false)
const actualCode = ref('')
const terminalEl = ref<HTMLElement | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)

let terminal: any = null

const terminalTheme = {
  background: '#0f0f1a',
  foreground: '#e0e0e0',
  cursor: '#e0e0e0',
  cursorAccent: '#0f0f1a',
  selectionBackground: 'rgba(255, 255, 255, 0.2)',
  black: '#1a1a2e',
  red: '#ff6b6b',
  green: '#4ecdc4',
  yellow: '#ffe66d',
  blue: '#4a90d9',
  magenta: '#c56cf0',
  cyan: '#67e8f9',
  white: '#e0e0e0',
  brightBlack: '#4a4a5e',
  brightRed: '#ff8787',
  brightGreen: '#6ee7de',
  brightYellow: '#fff089',
  brightBlue: '#6ba3e0',
  brightMagenta: '#d98bf0',
  brightCyan: '#89f0ff',
  brightWhite: '#ffffff'
}

// Extract text from VNode tree recursively
function extractTextFromVNodes(vnodes: VNode[]): string {
  const parts: string[] = []
  
  for (const vnode of vnodes) {
    if (typeof vnode.children === 'string') {
      parts.push(vnode.children)
    } else if (Array.isArray(vnode.children)) {
      parts.push(extractTextFromVNodes(vnode.children as VNode[]))
    } else if (vnode.children && typeof vnode.children === 'object') {
      const children = vnode.children as Record<string, unknown>
      if (typeof children.default === 'function') {
        const result = children.default()
        if (Array.isArray(result)) {
          parts.push(extractTextFromVNodes(result))
        }
      }
    }
  }
  
  return parts.join('')
}

function getCodeFromSlot(): string {
  const defaultSlot = slots.default
  if (defaultSlot) {
    const vnodes = defaultSlot()
    if (vnodes && vnodes.length > 0) {
      const texts: string[] = []
      for (const vnode of vnodes) {
        if (vnode.type === 'p' && typeof vnode.children === 'string') {
          texts.push(vnode.children)
        } else if (typeof vnode.children === 'string') {
          texts.push(vnode.children)
        } else if (Array.isArray(vnode.children)) {
          texts.push(extractTextFromVNodes(vnode.children as VNode[]))
        } else {
          const extracted = extractTextFromVNodes([vnode])
          if (extracted) {
            texts.push(extracted)
          }
        }
      }
      return texts.filter(t => t.trim()).join('\n\n')
    }
  }
  return ''
}

async function highlightCode() {
  const code = props.code || getCodeFromSlot()
  if (code && code !== actualCode.value) {
    actualCode.value = code
    highlightedCode.value = await codeToHtml(code, {
      lang: props.lang || 'csharp',
      themes: {
        light: 'github-light',
        dark: 'github-dark'
      },
      defaultColor: false
    })
  }
}

watch(isDark, () => {
  const code = actualCode.value
  actualCode.value = ''
  nextTick(() => {
    if (code) {
      actualCode.value = code
      highlightCode()
    }
  })
})

function copyToClipboard() {
  navigator.clipboard.writeText(actualCode.value)
  copied.value = true
  setTimeout(() => {
    copied.value = false
  }, 2000)
}

async function initTerminal() {
  await nextTick()
  
  if (!terminalEl.value) {
    error.value = 'Terminal element not found'
    isLoading.value = false
    return
  }
  
  try {
    const xtermModule = await import('@xterm/xterm')
    const unicode11Module = await import('@xterm/addon-unicode11')
    await import('@xterm/xterm/css/xterm.css')
    
    const Terminal = xtermModule.Terminal
    const Unicode11Addon = unicode11Module.Unicode11Addon
    
    terminal = new Terminal({
      cols: props.cols || 60,
      rows: props.rows || 6,
      theme: terminalTheme,
      fontFamily: '"Cascadia Code", "Fira Code", "JetBrains Mono", Menlo, Monaco, monospace',
      fontSize: 14,
      lineHeight: 1,
      cursorBlink: false,
      cursorStyle: 'block',
      disableStdin: true,
      allowProposedApi: true
    })
    
    terminal.open(terminalEl.value)
    
    const unicode11Addon = new Unicode11Addon()
    terminal.loadAddon(unicode11Addon)
    terminal.unicode.activeVersion = '11'
    
    await loadAnsiFile()
    
    isLoading.value = false
  } catch (err) {
    console.error('Failed to initialize terminal:', err)
    error.value = 'Failed to load terminal'
    isLoading.value = false
  }
}

async function loadAnsiFile() {
  try {
    const response = await fetch(`/${props.ansiFile}`)
    if (!response.ok) {
      throw new Error(`Failed to load ${props.ansiFile}: ${response.status}`)
    }
    const ansiContent = await response.text()
    terminal?.write(ansiContent)
  } catch (err) {
    console.error('Failed to load ANSI file:', err)
    error.value = `Failed to load: ${props.ansiFile}`
  }
}

onMounted(async () => {
  await nextTick()
  await highlightCode()
  await initTerminal()
})

watch(() => props.code, () => {
  highlightCode()
})

watch(() => props.ansiFile, async () => {
  terminal?.reset()
  await loadAnsiFile()
})
</script>

<template>
  <div class="static-code-block-wrapper">
    <!-- Code section -->
    <div class="code-section">
      <div class="code-header">
        <span class="code-lang">{{ title || lang || 'C#' }}</span>
        <button class="copy-button" @click="copyToClipboard" :title="copied ? 'Copied!' : 'Copy code'">
          <svg v-if="!copied" class="copy-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
          </svg>
          <svg v-else class="copy-icon check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="20 6 9 17 4 12"></polyline>
          </svg>
        </button>
      </div>
      <div 
        v-if="highlightedCode" 
        class="code-block highlighted"
        v-html="highlightedCode"
      ></div>
      <pre v-else class="code-block"><code>{{ actualCode }}</code></pre>
    </div>
    
    <!-- Terminal preview section -->
    <ClientOnly>
      <div class="terminal-section">
        <div class="terminal-header">
          <span class="terminal-title">{{ terminalTitle || 'Output' }}</span>
        </div>
        
        <div v-if="isLoading" class="terminal-loading">
          Loading preview...
        </div>
        
        <div v-else-if="error" class="terminal-loading terminal-error">
          {{ error }}
        </div>
        
        <div v-show="!isLoading && !error" ref="terminalEl" class="terminal-viewport"></div>
      </div>
      
      <template #fallback>
        <div class="terminal-section">
          <div class="terminal-header">
            <span class="terminal-title">{{ terminalTitle || 'Output' }}</span>
          </div>
          <div class="terminal-loading">Loading preview...</div>
        </div>
      </template>
    </ClientOnly>
  </div>
</template>

<style scoped>
.static-code-block-wrapper {
  border-radius: 8px;
  overflow: hidden;
  margin: 16px 0;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

/* Code section */
.code-section {
  background: #2d333b;
}

.code-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background: rgba(0, 0, 0, 0.15);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
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
  line-height: 0.7;
  color: #e0e0e0;
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
  line-height: 0.7;
  display: block;
  position: relative;
  padding-left: calc(2em + 20px);
}

.code-block.highlighted :deep(code::before) {
  content: '';
  position: absolute;
  left: calc(2em + 8px);
  top: 0;
  bottom: 0;
  width: 1px;
  background: rgba(255, 255, 255, 0.08);
}

.code-block.highlighted :deep(code .line) {
  display: block;
  line-height: 0.7;
}

.code-block.highlighted :deep(code .line::before) {
  counter-increment: line;
  content: counter(line);
  display: inline-block;
  width: 2em;
  margin-left: calc(-2em - 20px);
  margin-right: 20px;
  text-align: right;
  color: rgba(255, 255, 255, 0.25);
}

/* Terminal section */
.terminal-section {
  background: #0f0f1a;
  border-top: 1px solid rgba(78, 205, 196, 0.3);
}

.terminal-header {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  background: rgba(0, 0, 0, 0.3);
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.terminal-title {
  font-size: 12px;
  color: rgba(255, 255, 255, 0.5);
  font-weight: 500;
}

.terminal-viewport {
  padding: 8px 12px;
}

.terminal-loading {
  padding: 20px;
  text-align: center;
  color: rgba(255, 255, 255, 0.5);
  font-size: 13px;
}

.terminal-error {
  color: #ff6b6b;
}

/* Scrollbar styling */
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

.code-block {
  scrollbar-width: thin;
  scrollbar-color: rgba(78, 205, 196, 0.4) rgba(0, 0, 0, 0.2);
}

/* Light mode overrides */
:root:not(.dark) .static-code-block-wrapper {
  border-color: rgba(0, 0, 0, 0.1);
}

:root:not(.dark) .code-section {
  background: #f6f8fa;
}

:root:not(.dark) .code-header {
  background: rgba(0, 0, 0, 0.05);
  border-bottom: 1px solid rgba(0, 0, 0, 0.1);
}

:root:not(.dark) .code-lang {
  color: rgba(0, 0, 0, 0.5);
}

:root:not(.dark) .copy-icon {
  color: rgba(0, 0, 0, 0.5);
}

:root:not(.dark) .copy-button:hover {
  background: rgba(0, 0, 0, 0.1);
}

:root:not(.dark) .copy-button:hover .copy-icon {
  color: rgba(0, 0, 0, 0.8);
}

:root:not(.dark) .code-block {
  color: #24292e;
}

:root:not(.dark) .code-block.highlighted :deep(code::before) {
  background: rgba(0, 0, 0, 0.1);
}

:root:not(.dark) .code-block.highlighted :deep(code .line::before) {
  color: rgba(0, 0, 0, 0.4);
}

:root:not(.dark) .code-block.highlighted :deep(.shiki) {
  background-color: transparent !important;
}

:root:not(.dark) .terminal-section {
  border-top-color: rgba(78, 205, 196, 0.4);
}

/* Shiki dual theme support */
.code-block.highlighted :deep(.shiki),
.code-block.highlighted :deep(.shiki span) {
  color: var(--shiki-dark) !important;
  background-color: transparent !important;
}

:root:not(.dark) .code-block.highlighted :deep(.shiki),
:root:not(.dark) .code-block.highlighted :deep(.shiki span) {
  color: var(--shiki-light) !important;
}
</style>
