<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, nextTick, computed } from 'vue'

const props = defineProps<{
  exhibit: string
  title?: string
  cols?: number
  rows?: number
}>()

const terminalEl = ref<HTMLElement | null>(null)
const containerEl = ref<HTMLElement | null>(null)
const isOpen = ref(false)
const isLoading = ref(false)
const error = ref<string | null>(null)

// Terminal state
let terminal: any = null
let websocket: WebSocket | null = null

// Floating window state
const position = ref({ x: 100, y: 100 })
const terminalSize = ref({ cols: props.cols || 80, rows: props.rows || 24 })
const isResizing = ref(false)
const isAnimating = ref(false)
const resizeDirection = ref('')
const resizeOffset = ref({ top: 0, left: 0, right: 0, bottom: 0 })
let recentlyInteracted = false
const resizeState = ref<{
  startX: number
  startY: number
  startCols: number
  startRows: number
  cellWidth: number
  cellHeight: number
} | null>(null)

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

const TERMINAL_SIZES = [
  { name: '80×24', cols: 80, rows: 24 },
  { name: '120×30', cols: 120, rows: 30 },
  { name: '132×43', cols: 132, rows: 43 }
]

function openTerminal() {
  isOpen.value = true
  
  // Constrain terminal size to fit window and center
  nextTick(() => {
    initTerminal().then(() => {
      constrainTerminalSize()
      centerTerminal()
    })
  })
}

function closeTerminal() {
  isOpen.value = false
  cleanup()
}

function handleOverlayClick() {
  // Don't close if we just finished dragging or resizing
  if (recentlyInteracted) {
    recentlyInteracted = false
    return
  }
  closeTerminal()
}

function cleanup() {
  websocket?.close()
  websocket = null
  terminal?.dispose()
  terminal = null
}

async function initTerminal() {
  if (!terminalEl.value) {
    error.value = 'Terminal element not found'
    return
  }
  
  isLoading.value = true
  error.value = null
  
  try {
    const xtermModule = await import('@xterm/xterm')
    const unicode11Module = await import('@xterm/addon-unicode11')
    const imageModule = await import('@xterm/addon-image')
    await import('@xterm/xterm/css/xterm.css')
    
    const Terminal = xtermModule.Terminal
    const Unicode11Addon = unicode11Module.Unicode11Addon
    const ImageAddon = imageModule.ImageAddon
    
    terminal = new Terminal({
      cols: terminalSize.value.cols,
      rows: terminalSize.value.rows,
      theme: terminalTheme,
      fontFamily: '"Cascadia Code", "Fira Code", "JetBrains Mono", Menlo, Monaco, monospace',
      fontSize: 14,
      lineHeight: 1.2,
      cursorBlink: true,
      cursorStyle: 'block',
      allowProposedApi: true
    })
    
    terminal.open(terminalEl.value)
    
    const unicode11Addon = new Unicode11Addon()
    terminal.loadAddon(unicode11Addon)
    terminal.unicode.activeVersion = '11'
    
    const imageAddon = new ImageAddon()
    terminal.loadAddon(imageAddon)
    
    connectWebSocket()
    
    isLoading.value = false
    
    // Focus the terminal
    terminal.focus()
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
  
  websocket.onopen = () => {
    // Send initial size
    websocket?.send(JSON.stringify({
      type: 'resize',
      cols: terminalSize.value.cols,
      rows: terminalSize.value.rows
    }))
  }
  
  websocket.onmessage = (event) => {
    const data = event.data
    
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
  websocket?.close()
  terminal?.reset()
  connectWebSocket()
}

// Resize functionality
function getTerminalCellSize(): { cellWidth: number; cellHeight: number } {
  if (!terminal || !terminalEl.value) return { cellWidth: 8.4, cellHeight: 17 }
  
  const termElement = terminalEl.value.querySelector('.xterm-screen') as HTMLElement
  if (termElement) {
    return {
      cellWidth: termElement.offsetWidth / terminal.cols,
      cellHeight: termElement.offsetHeight / terminal.rows
    }
  }
  
  return { cellWidth: 8.4, cellHeight: 17 }
}

function startResize(e: MouseEvent, direction: string) {
  e.preventDefault()
  e.stopPropagation()
  
  isResizing.value = true
  resizeDirection.value = direction
  
  const { cellWidth, cellHeight } = getTerminalCellSize()
  
  resizeState.value = {
    startX: e.clientX,
    startY: e.clientY,
    startCols: terminalSize.value.cols,
    startRows: terminalSize.value.rows,
    cellWidth,
    cellHeight
  }
  
  document.body.classList.add('resizing')
  document.addEventListener('mousemove', onResize)
  document.addEventListener('mouseup', stopResize)
}

function onResize(e: MouseEvent) {
  if (!isResizing.value || !resizeState.value) return
  
  const { startX, startY, startCols, startRows, cellWidth, cellHeight } = resizeState.value
  const direction = resizeDirection.value
  
  const deltaX = e.clientX - startX
  const deltaY = e.clientY - startY
  
  // Get max allowed size based on window dimensions
  const { maxCols, maxRows } = getMaxTerminalSize()
  
  let newCols = startCols
  let newRows = startRows
  
  if (direction.includes('e')) {
    newCols = Math.max(40, Math.min(maxCols, startCols + Math.round(deltaX / cellWidth)))
  } else if (direction.includes('w')) {
    newCols = Math.max(40, Math.min(maxCols, startCols - Math.round(deltaX / cellWidth)))
  }
  
  if (direction.includes('s')) {
    newRows = Math.max(10, Math.min(maxRows, startRows + Math.round(deltaY / cellHeight)))
  } else if (direction.includes('n')) {
    newRows = Math.max(10, Math.min(maxRows, startRows - Math.round(deltaY / cellHeight)))
  }
  
  // Calculate pixel offsets for overlay preview
  const colDelta = newCols - startCols
  const rowDelta = newRows - startRows
  const pixelDeltaX = colDelta * cellWidth
  const pixelDeltaY = rowDelta * cellHeight
  
  // Update overlay edges based on resize direction
  resizeOffset.value = {
    top: direction.includes('n') ? -pixelDeltaY : 0,
    left: direction.includes('w') ? -pixelDeltaX : 0,
    right: direction.includes('e') ? -pixelDeltaX : 0,
    bottom: direction.includes('s') ? -pixelDeltaY : 0
  }
  
  terminalSize.value = { cols: newCols, rows: newRows }
}

function stopResize() {
  if (!isResizing.value) return
  
  const { cols, rows } = terminalSize.value
  
  // Apply the new size to the terminal
  if (terminal) {
    terminal.resize(cols, rows)
    
    // Send resize to server
    if (websocket?.readyState === WebSocket.OPEN) {
      websocket.send(JSON.stringify({
        type: 'resize',
        cols,
        rows
      }))
    }
  }
  
  isResizing.value = false
  resizeState.value = null
  resizeOffset.value = { top: 0, left: 0, right: 0, bottom: 0 }
  recentlyInteracted = true
  document.body.classList.remove('resizing')
  document.removeEventListener('mousemove', onResize)
  document.removeEventListener('mouseup', stopResize)
  
  // Center the terminal after resize
  centerTerminal()
}

function centerTerminal() {
  // Wait a tick for the terminal to render at new size
  nextTick(() => {
    const container = containerEl.value
    if (!container) return
    
    const rect = container.getBoundingClientRect()
    const newX = Math.max(20, (window.innerWidth - rect.width) / 2)
    const newY = Math.max(20, (window.innerHeight - rect.height) / 2)
    
    isAnimating.value = true
    position.value = { x: newX, y: newY }
    
    // Turn off animation after transition completes
    setTimeout(() => {
      isAnimating.value = false
    }, 400)
  })
}

function selectSize(size: { cols: number; rows: number }) {
  // Constrain size to fit within 90% of window
  const { maxCols, maxRows } = getMaxTerminalSize()
  const cols = Math.min(size.cols, maxCols)
  const rows = Math.min(size.rows, maxRows)
  
  terminalSize.value = { cols, rows }
  
  if (terminal) {
    terminal.resize(cols, rows)
    
    if (websocket?.readyState === WebSocket.OPEN) {
      websocket.send(JSON.stringify({
        type: 'resize',
        cols,
        rows
      }))
    }
  }
  
  // Center the terminal after size change
  centerTerminal()
}

function getMaxTerminalSize(): { maxCols: number; maxRows: number } {
  // Terminal must fit within 90% of window dimensions
  // Account for padding and header (approx 80px for header + padding)
  const { cellWidth, cellHeight } = getTerminalCellSize()
  const maxWidth = window.innerWidth * 0.9 - 40 // 40px for padding
  const maxHeight = window.innerHeight * 0.9 - 100 // 100px for header + padding
  
  const maxCols = Math.floor(maxWidth / cellWidth)
  const maxRows = Math.floor(maxHeight / cellHeight)
  
  return { maxCols: Math.max(40, maxCols), maxRows: Math.max(10, maxRows) }
}

function constrainTerminalSize() {
  const { maxCols, maxRows } = getMaxTerminalSize()
  const currentCols = terminalSize.value.cols
  const currentRows = terminalSize.value.rows
  
  if (currentCols > maxCols || currentRows > maxRows) {
    const newCols = Math.min(currentCols, maxCols)
    const newRows = Math.min(currentRows, maxRows)
    
    terminalSize.value = { cols: newCols, rows: newRows }
    
    if (terminal) {
      terminal.resize(newCols, newRows)
      
      if (websocket?.readyState === WebSocket.OPEN) {
        websocket.send(JSON.stringify({
          type: 'resize',
          cols: newCols,
          rows: newRows
        }))
      }
    }
  }
}

function handleWindowResize() {
  if (isOpen.value && !isResizing.value) {
    constrainTerminalSize()
    centerTerminal()
  }
}

const displaySize = computed(() => `${terminalSize.value.cols}×${terminalSize.value.rows}`)

onMounted(() => {
  window.addEventListener('resize', handleWindowResize)
})

onUnmounted(() => {
  cleanup()
  window.removeEventListener('resize', handleWindowResize)
})

watch(() => props.exhibit, () => {
  if (isOpen.value) {
    websocket?.close()
    terminal?.reset()
    connectWebSocket()
  }
})
</script>

<template>
  <ClientOnly>
    <!-- Trigger button/card -->
    <div class="terminal-trigger" @click="openTerminal">
      <div class="trigger-preview">
        <div class="trigger-icon">▶</div>
        <span class="trigger-text">{{ title || exhibit }}</span>
      </div>
      <div class="trigger-hint">Click to open terminal</div>
    </div>
    
    <!-- Floating terminal modal -->
    <Teleport to="body">
      <div v-if="isOpen" class="floating-terminal-overlay" @click.self="handleOverlayClick">
        <div 
          ref="containerEl"
          class="floating-terminal"
          :class="{ 'is-resizing': isResizing, 'is-animating': isAnimating }"
          :style="{ left: position.x + 'px', top: position.y + 'px' }"
        >
          <!-- Resize handles -->
          <div class="resize-handle resize-handle-n" @mousedown="startResize($event, 'n')"></div>
          <div class="resize-handle resize-handle-s" @mousedown="startResize($event, 's')"></div>
          <div class="resize-handle resize-handle-e" @mousedown="startResize($event, 'e')"></div>
          <div class="resize-handle resize-handle-w" @mousedown="startResize($event, 'w')"></div>
          <div class="resize-handle resize-handle-nw" @mousedown="startResize($event, 'nw')"></div>
          <div class="resize-handle resize-handle-ne" @mousedown="startResize($event, 'ne')"></div>
          <div class="resize-handle resize-handle-sw" @mousedown="startResize($event, 'sw')"></div>
          <div class="resize-handle resize-handle-se" @mousedown="startResize($event, 'se')"></div>
          
          <!-- Resize overlay -->
          <div 
            class="resize-overlay" 
            :class="{ active: isResizing }"
            :style="{
              top: resizeOffset.top + 'px',
              left: resizeOffset.left + 'px',
              right: resizeOffset.right + 'px',
              bottom: resizeOffset.bottom + 'px'
            }"
          >
            <div class="resize-overlay-info">{{ displaySize }}</div>
          </div>
          
          <!-- Header -->
          <div class="terminal-header">
            <div class="terminal-dots">
              <span class="terminal-dot red" @click.stop="closeTerminal"></span>
              <span class="terminal-dot yellow"></span>
              <span class="terminal-dot green"></span>
            </div>
            <span class="terminal-title">{{ title || exhibit }}</span>
            <div class="terminal-controls">
              <div class="size-buttons">
                <button 
                  v-for="size in TERMINAL_SIZES" 
                  :key="size.name"
                  class="size-btn"
                  :class="{ active: terminalSize.cols === size.cols && terminalSize.rows === size.rows }"
                  @click.stop="selectSize(size)"
                >
                  {{ size.name }}
                </button>
              </div>
              <button class="terminal-refresh" @click.stop="refresh" title="Refresh">↻</button>
              <button class="terminal-close" @click.stop="closeTerminal" title="Close">×</button>
            </div>
          </div>
          
          <!-- Terminal content -->
          <div class="terminal-body">
            <div v-if="isLoading" class="terminal-loading">
              Loading terminal...
            </div>
            
            <div v-else-if="error" class="terminal-loading" style="color: #ff6b6b;">
              {{ error }}
            </div>
            
            <div v-show="!isLoading && !error" ref="terminalEl" class="terminal-viewport"></div>
          </div>
          
          <!-- Size indicator -->
          <div class="size-indicator">{{ displaySize }}</div>
        </div>
      </div>
    </Teleport>
    
    <template #fallback>
      <div class="terminal-trigger disabled">
        <span>Loading...</span>
      </div>
    </template>
  </ClientOnly>
</template>

<style scoped>
.terminal-trigger {
  background: linear-gradient(135deg, #1a1a2e 0%, #0f0f1a 100%);
  border: 1px solid rgba(78, 205, 196, 0.3);
  border-radius: 12px;
  padding: 24px;
  cursor: pointer;
  transition: all 0.3s ease;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.terminal-trigger:hover {
  border-color: rgba(78, 205, 196, 0.6);
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3), 0 0 20px rgba(78, 205, 196, 0.1);
}

.terminal-trigger.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.trigger-preview {
  display: flex;
  align-items: center;
  gap: 12px;
}

.trigger-icon {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  background: rgba(78, 205, 196, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  color: #4ecdc4;
  transition: all 0.3s ease;
}

.terminal-trigger:hover .trigger-icon {
  background: rgba(78, 205, 196, 0.3);
  transform: scale(1.1);
}

.trigger-text {
  font-size: 18px;
  font-weight: 600;
  color: #e0e0e0;
}

.trigger-hint {
  font-size: 14px;
  color: rgba(255, 255, 255, 0.4);
}

/* Floating terminal overlay */
.floating-terminal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 9999;
  backdrop-filter: blur(4px);
}

.floating-terminal {
  position: absolute;
  background: #0f0f1a;
  border-radius: 12px;
  box-shadow: 
    0 4px 6px rgba(0, 0, 0, 0.3),
    0 10px 40px rgba(0, 0, 0, 0.4),
    0 0 0 1px rgba(78, 205, 196, 0.3),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.08);
  overflow: visible;
}

.floating-terminal.is-resizing {
  user-select: none;
}

.floating-terminal.is-animating {
  transition: left 0.35s cubic-bezier(0.4, 0, 0.2, 1), top 0.35s cubic-bezier(0.4, 0, 0.2, 1);
}

/* Resize handles */
.resize-handle {
  position: absolute;
  z-index: 10;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.floating-terminal:hover .resize-handle {
  opacity: 1;
}

.resize-handle::before {
  content: '';
  position: absolute;
  background: #4ecdc4;
  border-radius: 3px;
  transition: background 0.2s ease, box-shadow 0.2s ease;
  box-shadow: 0 0 4px rgba(78, 205, 196, 0.5);
}

.resize-handle:hover::before {
  background: #6ee7de;
  box-shadow: 0 0 8px rgba(78, 205, 196, 0.8);
}

.resize-handle-n,
.resize-handle-s {
  left: 20%;
  right: 20%;
  height: 14px;
  cursor: ns-resize;
}

.resize-handle-n { top: -7px; }
.resize-handle-s { bottom: -7px; }

.resize-handle-n::before,
.resize-handle-s::before {
  left: 50%;
  transform: translateX(-50%);
  width: 60px;
  height: 5px;
  top: 4px;
}

.resize-handle-e,
.resize-handle-w {
  top: 20%;
  bottom: 20%;
  width: 14px;
  cursor: ew-resize;
}

.resize-handle-e { right: -7px; }
.resize-handle-w { left: -7px; }

.resize-handle-e::before,
.resize-handle-w::before {
  top: 50%;
  transform: translateY(-50%);
  width: 5px;
  height: 60px;
  left: 4px;
}

.resize-handle-nw,
.resize-handle-ne,
.resize-handle-sw,
.resize-handle-se {
  width: 20px;
  height: 20px;
}

.resize-handle-nw { top: -4px; left: -4px; cursor: nwse-resize; }
.resize-handle-ne { top: -4px; right: -4px; cursor: nesw-resize; }
.resize-handle-sw { bottom: -4px; left: -4px; cursor: nesw-resize; }
.resize-handle-se { bottom: -4px; right: -4px; cursor: nwse-resize; }

.resize-handle-nw::before,
.resize-handle-ne::before,
.resize-handle-sw::before,
.resize-handle-se::before {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.resize-handle-nw::before { top: 3px; left: 3px; }
.resize-handle-ne::before { top: 3px; right: 3px; left: auto; }
.resize-handle-sw::before { bottom: 3px; left: 3px; top: auto; }
.resize-handle-se::before { bottom: 3px; right: 3px; top: auto; left: auto; }

/* Resize overlay */
.resize-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(78, 205, 196, 0.15);
  border: 3px solid #4ecdc4;
  border-radius: 12px;
  pointer-events: none;
  z-index: 100;
  display: none;
  justify-content: center;
  align-items: center;
  box-shadow: 
    0 0 0 1px rgba(0, 0, 0, 0.3),
    0 0 20px rgba(78, 205, 196, 0.4),
    inset 0 0 30px rgba(78, 205, 196, 0.1);
}

.resize-overlay.active {
  display: flex;
}

.resize-overlay-info {
  background: rgba(15, 15, 26, 0.98);
  padding: 12px 24px;
  border-radius: 8px;
  border: 2px solid #4ecdc4;
  color: #4ecdc4;
  font-size: 18px;
  font-weight: 600;
  font-family: 'Cascadia Code', 'Fira Code', monospace;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
}

/* Terminal header */
.terminal-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  user-select: none;
}

.terminal-dots {
  display: flex;
  gap: 6px;
}

.terminal-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  cursor: pointer;
  transition: transform 0.2s;
}

.terminal-dot:hover {
  transform: scale(1.2);
}

.terminal-dot.red { background: #ff5f56; }
.terminal-dot.yellow { background: #ffbd2e; }
.terminal-dot.green { background: #27c93f; }

.terminal-title {
  color: #4ecdc4;
  font-size: 14px;
  font-weight: 500;
  flex: 1;
}

.terminal-controls {
  display: flex;
  align-items: center;
  gap: 8px;
}

.size-buttons {
  display: flex;
  gap: 4px;
}

.size-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.5);
  font-size: 11px;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  font-family: 'Cascadia Code', 'Fira Code', monospace;
}

.size-btn:hover {
  background: rgba(78, 205, 196, 0.1);
  border-color: rgba(78, 205, 196, 0.3);
  color: rgba(255, 255, 255, 0.8);
}

.size-btn.active {
  background: rgba(78, 205, 196, 0.2);
  border-color: rgba(78, 205, 196, 0.5);
  color: #4ecdc4;
}

.terminal-refresh,
.terminal-close {
  background: none;
  border: none;
  color: rgba(255, 255, 255, 0.3);
  font-size: 18px;
  cursor: pointer;
  padding: 4px 8px;
  transition: color 0.2s, transform 0.2s;
  border-radius: 4px;
}

.terminal-refresh:hover,
.terminal-close:hover {
  color: rgba(255, 255, 255, 0.7);
  background: rgba(255, 255, 255, 0.05);
}

.terminal-refresh:active {
  transform: rotate(180deg);
}

.terminal-close:hover {
  color: #ff5f56;
}

/* Terminal body */
.terminal-body {
  padding: 16px;
}

.terminal-viewport {
  width: fit-content;
  height: fit-content;
}

/* Hide xterm scrollbar */
.terminal-viewport :deep(.xterm-viewport) {
  overflow: hidden !important;
}

.terminal-viewport :deep(.xterm-viewport::-webkit-scrollbar) {
  display: none;
}

.terminal-loading {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 200px;
  min-width: 400px;
  color: rgba(255, 255, 255, 0.4);
  font-family: 'Cascadia Code', 'Fira Code', monospace;
}

/* Size indicator */
.size-indicator {
  position: absolute;
  bottom: -24px;
  right: 8px;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.3);
  font-family: 'Cascadia Code', 'Fira Code', monospace;
}
</style>

<style>
/* Global styles for resizing */
body.resizing {
  user-select: none;
  cursor: inherit;
}

body.resizing * {
  cursor: inherit !important;
}
</style>
