<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, nextTick } from 'vue'

const props = defineProps<{
  exhibit: string
  title?: string
  cols?: number
  rows?: number
}>()

const terminalEl = ref<HTMLElement | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)

let terminal: any = null
let websocket: WebSocket | null = null

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
  // Wait for next tick to ensure DOM is ready
  await nextTick()
  
  if (!terminalEl.value) {
    console.error('Terminal element not found')
    error.value = 'Terminal element not found'
    isLoading.value = false
    return
  }
  
  try {
    console.log('Loading xterm modules...')
    // Dynamic imports - these work in Vite but only on client side
    const xtermModule = await import('@xterm/xterm')
    const unicode11Module = await import('@xterm/addon-unicode11')
    const imageModule = await import('@xterm/addon-image')
    
    console.log('xterm modules loaded, importing CSS...')
    // Import CSS
    await import('@xterm/xterm/css/xterm.css')
    
    const Terminal = xtermModule.Terminal
    const Unicode11Addon = unicode11Module.Unicode11Addon
    const ImageAddon = imageModule.ImageAddon
    
    console.log('Creating terminal...')
    terminal = new Terminal({
      cols: props.cols || 80,
      rows: props.rows || 24,
      theme: terminalTheme,
      fontFamily: '"Cascadia Code", "Fira Code", "JetBrains Mono", Menlo, Monaco, monospace',
      fontSize: 14,
      lineHeight: 1.2,
      cursorBlink: true,
      cursorStyle: 'block',
      allowProposedApi: true
    })
    
    terminal.open(terminalEl.value)
    
    // Load addons
    const unicode11Addon = new Unicode11Addon()
    terminal.loadAddon(unicode11Addon)
    terminal.unicode.activeVersion = '11'
    
    const imageAddon = new ImageAddon()
    terminal.loadAddon(imageAddon)
    
    // Connect WebSocket
    connectWebSocket()
    
    isLoading.value = false
  } catch (err) {
    console.error('Failed to initialize terminal:', err)
    error.value = 'Failed to load terminal'
    isLoading.value = false
  }
}

function connectWebSocket() {
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
  const wsUrl = `${protocol}//${window.location.host}/apps/${props.exhibit}`
  
  websocket = new WebSocket(wsUrl)
  
  websocket.onmessage = (event) => {
    const data = event.data
    
    // Handle DA1 query for Sixel support
    if (data.includes('\x1b[c') && websocket?.readyState === WebSocket.OPEN) {
      websocket.send('\x1b[?62;4c')
    }
    
    terminal?.write(data)
  }
  
  websocket.onclose = () => {
    terminal?.write('\r\n\x1b[31m[Connection closed]\x1b[0m\r\n')
  }
  
  websocket.onerror = () => {
    error.value = 'WebSocket connection failed'
  }
  
  terminal?.onData((data: string) => {
    if (websocket?.readyState === WebSocket.OPEN) {
      websocket.send(data)
    }
  })
  
  terminal?.onBinary((data: string) => {
    if (websocket?.readyState === WebSocket.OPEN) {
      websocket.send(data)
    }
  })
}

function refresh() {
  if (websocket) {
    websocket.close()
  }
  terminal?.reset()
  connectWebSocket()
}

onMounted(() => {
  initTerminal()
})

onUnmounted(() => {
  websocket?.close()
  terminal?.dispose()
})

// Watch for exhibit changes
watch(() => props.exhibit, () => {
  if (websocket) {
    websocket.close()
  }
  terminal?.reset()
  connectWebSocket()
})
</script>

<template>
  <ClientOnly>
    <div class="terminal-container">
      <div class="terminal-header">
        <div class="terminal-dots">
          <span class="terminal-dot red"></span>
          <span class="terminal-dot yellow"></span>
          <span class="terminal-dot green"></span>
        </div>
        <span class="terminal-title">{{ title || exhibit }}</span>
        <button class="terminal-refresh" @click="refresh" title="Refresh">↻</button>
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
.terminal-refresh {
  background: none;
  border: none;
  color: rgba(255, 255, 255, 0.3);
  font-size: 18px;
  cursor: pointer;
  padding: 4px 8px;
  transition: color 0.2s, transform 0.2s;
  border-radius: 4px;
}

.terminal-refresh:hover {
  color: rgba(255, 255, 255, 0.7);
  background: rgba(255, 255, 255, 0.05);
}

.terminal-refresh:active {
  transform: rotate(180deg);
}
</style>
