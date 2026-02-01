<script setup lang="ts">
import { ref, onMounted, nextTick, watch } from 'vue'

/**
 * StaticTerminal - Displays a static ANSI file in an xterm.js terminal.
 * 
 * This component loads a pre-generated .ansi file and renders it in a
 * non-interactive terminal view. Use this for documentation screenshots
 * that don't require live interaction.
 * 
 * Props:
 *   - file: Path to the .ansi file relative to /public (e.g., "ansi/text-basic.ansi")
 *   - title: Optional title to display in the terminal header
 *   - cols: Terminal width (default: 80)
 *   - rows: Terminal height (default: 24)
 */
const props = defineProps<{
  file: string
  title?: string
  cols?: number
  rows?: number
}>()

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

async function initTerminal() {
  await nextTick()
  
  if (!terminalEl.value) {
    console.error('Terminal element not found')
    error.value = 'Terminal element not found'
    isLoading.value = false
    return
  }
  
  try {
    console.log('Loading xterm for static terminal...')
    const xtermModule = await import('@xterm/xterm')
    const unicode11Module = await import('@xterm/addon-unicode11')
    await import('@xterm/xterm/css/xterm.css')
    
    const Terminal = xtermModule.Terminal
    const Unicode11Addon = unicode11Module.Unicode11Addon
    
    terminal = new Terminal({
      cols: props.cols || 80,
      rows: props.rows || 24,
      theme: terminalTheme,
      fontFamily: '"Cascadia Code", "Fira Code", "JetBrains Mono", Menlo, Monaco, monospace',
      fontSize: 14,
      lineHeight: 1,
      cursorBlink: false,
      cursorStyle: 'block',
      disableStdin: true,  // Non-interactive
      allowProposedApi: true
    })
    
    terminal.open(terminalEl.value)
    
    // Load Unicode support
    const unicode11Addon = new Unicode11Addon()
    terminal.loadAddon(unicode11Addon)
    terminal.unicode.activeVersion = '11'
    
    // Load the ANSI file
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
    const response = await fetch(`/${props.file}`)
    if (!response.ok) {
      throw new Error(`Failed to load ${props.file}: ${response.status}`)
    }
    const ansiContent = await response.text()
    terminal?.write(ansiContent)
  } catch (err) {
    console.error('Failed to load ANSI file:', err)
    error.value = `Failed to load: ${props.file}`
  }
}

onMounted(() => {
  initTerminal()
})

// Reload if file prop changes
watch(() => props.file, async () => {
  terminal?.reset()
  await loadAnsiFile()
})
</script>

<template>
  <ClientOnly>
    <div class="terminal-container static-terminal">
      <div class="terminal-header">
        <div class="terminal-dots">
          <span class="terminal-dot red"></span>
          <span class="terminal-dot yellow"></span>
          <span class="terminal-dot green"></span>
        </div>
        <span class="terminal-title">{{ title || 'Terminal' }}</span>
        <span class="terminal-badge">Static</span>
      </div>
      
      <div v-if="isLoading" class="terminal-loading">
        Loading terminal...
      </div>
      
      <div v-else-if="error" class="terminal-loading" style="color: #ff6b6b;">
        {{ error }}
      </div>
      
      <div v-show="!isLoading && !error" ref="terminalEl" class="terminal-viewport"></div>
    </div>
    
    <template #fallback>
      <div class="terminal-container">
        <div class="terminal-loading">Loading terminal...</div>
      </div>
    </template>
  </ClientOnly>
</template>

<style scoped>
.static-terminal {
  /* Indicate this is non-interactive */
  opacity: 0.95;
}

.terminal-badge {
  background: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.4);
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 3px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
</style>
