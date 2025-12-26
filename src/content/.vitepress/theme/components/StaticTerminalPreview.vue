<script setup lang="ts">
import { ref, onMounted, useSlots, watch, nextTick, type VNode } from 'vue'
import { codeToHtml } from 'shiki'
import { useData } from 'vitepress'

const { isDark } = useData()

interface Props {
  code?: string
  language?: string
  svgPath: string // Path to the SVG file (relative to public, e.g., '/svg/text-overflow-truncate.svg')
}

interface CellData {
  c: string
  fg: { r: number; g: number; b: number } | null
  bg: { r: number; g: number; b: number } | null
  a: number
  seq: number
  t: string | null
  sixel: { origin: boolean; w: number; h: number } | null
  link: { uri: string; params?: string; group?: string } | null
}

const props = withDefaults(defineProps<Props>(), {
  language: 'csharp'
})

const slots = useSlots()
const isExpanded = ref(false)
const copied = ref(false)
const svgContent = ref('')
const svgContainerRef = ref<HTMLDivElement | null>(null)
const tooltipRef = ref<HTMLDivElement | null>(null)
const highlightRef = ref<HTMLDivElement | null>(null)

// For Shiki highlighting (matching CodeBlock.vue)
const highlightedCode = ref<string>('')
const actualCode = ref('')

// Cell data parsed from the HTML file
const cellData = ref<CellData[][]>([])
const svgDimensions = ref({ width: 0, height: 0, cols: 0, rows: 0 })
const CELL_WIDTH = 9
const CELL_HEIGHT = 18

// Current hovered cell
const hoveredCell = ref<{ x: number; y: number; cell: CellData } | null>(null)

// Extract text from VNode tree recursively (from CodeBlock.vue)
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

// Get code from slot (from CodeBlock.vue)
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

// Get the code content from slot or prop
function getCodeContent(): string {
  if (props.code) return props.code
  return getCodeFromSlot()
}

// Highlight code with Shiki (from CodeBlock.vue)
async function highlightCode() {
  const code = props.code || getCodeFromSlot()
  if (code && code !== actualCode.value) {
    actualCode.value = code
    highlightedCode.value = await codeToHtml(code, {
      lang: props.language || 'csharp',
      themes: {
        light: 'github-light',
        dark: 'github-dark'
      },
      defaultColor: false
    })
  }
}

// Re-highlight when theme changes
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

async function copyCode() {
  const code = getCodeContent()
  if (!code) return
  
  try {
    await navigator.clipboard.writeText(code)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch (err) {
    console.error('Failed to copy:', err)
  }
}

function toggleExpanded() {
  isExpanded.value = !isExpanded.value
}

// Fetch and parse the SVG file
async function loadSvg() {
  try {
    const response = await fetch(props.svgPath)
    const text = await response.text()
    svgContent.value = text
    
    // Parse dimensions from SVG
    const widthMatch = text.match(/width="(\d+)"/)
    const heightMatch = text.match(/height="(\d+)"/)
    if (widthMatch && heightMatch) {
      const width = parseInt(widthMatch[1])
      const height = parseInt(heightMatch[1])
      svgDimensions.value = {
        width,
        height,
        cols: Math.floor(width / CELL_WIDTH),
        rows: Math.floor(height / CELL_HEIGHT)
      }
    }
    
    // Parse cell data from SVG DOM after it's rendered
    await nextTick()
    parseSvgCellData()
  } catch (err) {
    console.error('Failed to load SVG:', err)
  }
}

// Parse color from SVG fill attribute (handles #rrggbb hex and rgb() formats)
function parseColor(fill: string | null): { r: number, g: number, b: number } | null {
  if (!fill) return null
  
  // Handle hex format: #rrggbb
  if (fill.startsWith('#') && fill.length === 7) {
    const r = parseInt(fill.slice(1, 3), 16)
    const g = parseInt(fill.slice(3, 5), 16)
    const b = parseInt(fill.slice(5, 7), 16)
    if (!isNaN(r) && !isNaN(g) && !isNaN(b)) {
      return { r, g, b }
    }
  }
  
  // Handle rgb() format: rgb(r, g, b) or rgb(r,g,b)
  if (fill.startsWith('rgb(')) {
    const match = fill.match(/rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)/)
    if (match) {
      return { r: parseInt(match[1]), g: parseInt(match[2]), b: parseInt(match[3]) }
    }
  }
  
  return null
}

// Parse cell data directly from the SVG DOM structure
function parseSvgCellData() {
  if (!svgContainerRef.value) return
  
  const svgEl = svgContainerRef.value.querySelector('svg')
  if (!svgEl) return
  
  // Initialize 2D array for cell data
  const rows = svgDimensions.value.rows
  const cols = svgDimensions.value.cols
  const data: CellData[][] = Array.from({ length: rows }, () => 
    Array.from({ length: cols }, () => ({
      c: '\0',
      fg: null,
      bg: null,
      a: 0,
      seq: 0,
      t: null,
      sixel: null,
      link: null
    }))
  )
  
  // Find all cell groups in the SVG
  const cellGroups = svgEl.querySelectorAll('g.cell')
  
  for (const group of cellGroups) {
    const x = parseInt(group.getAttribute('data-x') || '0')
    const y = parseInt(group.getAttribute('data-y') || '0')
    
    if (x < 0 || x >= cols || y < 0 || y >= rows) continue
    
    const cell = data[y][x]
    
    // Get background from rect
    const rect = group.querySelector('rect.cell-bg')
    if (rect) {
      const fill = rect.getAttribute('fill')
      const bg = parseColor(fill)
      if (bg) cell.bg = bg
    }
    
    // Get text content and foreground color
    const textEl = group.querySelector('text')
    if (textEl) {
      // Get character - decode HTML entities
      let char = textEl.textContent || ''
      if (char === '␣' || textEl.innerHTML === '&#160;') char = ' '
      cell.c = char
      
      // Get foreground color
      const fill = textEl.getAttribute('fill')
      const fg = parseColor(fill)
      if (fg) cell.fg = fg
      
      // Parse style attributes
      const style = textEl.getAttribute('style') || ''
      let attrs = 0
      if (style.includes('font-weight:bold')) attrs |= 1
      if (style.includes('opacity:0.5')) attrs |= 2
      if (style.includes('font-style:italic')) attrs |= 4
      if (style.includes('underline')) attrs |= 8
      if (textEl.classList.contains('blink')) attrs |= 16
      if (style.includes('line-through')) attrs |= 128
      if (style.includes('overline')) attrs |= 256
      cell.a = attrs
    }
    
    // Check for hyperlink group class
    const classList = Array.from(group.classList)
    const linkClass = classList.find(c => c.startsWith('link-'))
    if (linkClass) {
      // We don't have the actual URI in the SVG, but we can indicate it's a link
      cell.link = { uri: '(hyperlink)', group: linkClass }
    }
  }
  
  cellData.value = data
}

// Color utilities
function rgbToHex(r: number, g: number, b: number): string {
  return '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('')
}

function getAttributeBadges(attrs: number): string[] {
  const badges: string[] = []
  if (attrs & 1) badges.push('Bold')
  if (attrs & 2) badges.push('Dim')
  if (attrs & 4) badges.push('Italic')
  if (attrs & 8) badges.push('Underline')
  if (attrs & 16) badges.push('Blink')
  if (attrs & 32) badges.push('Reverse')
  if (attrs & 64) badges.push('Hidden')
  if (attrs & 128) badges.push('Strike')
  if (attrs & 256) badges.push('Overline')
  return badges
}

// Mouse handling for SVG hover
function handleSvgMouseMove(e: MouseEvent) {
  if (!svgContainerRef.value || cellData.value.length === 0) return
  
  const svgEl = svgContainerRef.value.querySelector('svg')
  if (!svgEl) return
  
  const svgRect = svgEl.getBoundingClientRect()
  const scaleX = svgRect.width / svgDimensions.value.width
  const scaleY = svgRect.height / svgDimensions.value.height
  const cellWidth = CELL_WIDTH * scaleX
  const cellHeight = CELL_HEIGHT * scaleY
  
  const x = Math.floor((e.clientX - svgRect.left) / cellWidth)
  const y = Math.floor((e.clientY - svgRect.top) / cellHeight)
  
  if (x >= 0 && x < svgDimensions.value.cols && y >= 0 && y < svgDimensions.value.rows) {
    const cell = cellData.value[y]?.[x]
    if (cell) {
      hoveredCell.value = { x, y, cell }
      
      // Position highlight
      if (highlightRef.value) {
        const containerRect = svgContainerRef.value.getBoundingClientRect()
        highlightRef.value.style.left = (svgRect.left - containerRect.left + x * cellWidth) + 'px'
        highlightRef.value.style.top = (svgRect.top - containerRect.top + y * cellHeight) + 'px'
        highlightRef.value.style.width = cellWidth + 'px'
        highlightRef.value.style.height = cellHeight + 'px'
      }
      
      // Position tooltip
      if (tooltipRef.value) {
        let tooltipX = e.clientX + 15
        let tooltipY = e.clientY + 15
        
        nextTick(() => {
          if (!tooltipRef.value) return
          const tooltipRect = tooltipRef.value.getBoundingClientRect()
          if (tooltipX + tooltipRect.width > window.innerWidth - 10) {
            tooltipX = e.clientX - tooltipRect.width - 15
          }
          if (tooltipY + tooltipRect.height > window.innerHeight - 10) {
            tooltipY = e.clientY - tooltipRect.height - 15
          }
          tooltipRef.value.style.left = tooltipX + 'px'
          tooltipRef.value.style.top = tooltipY + 'px'
        })
      }
    }
  } else {
    hoveredCell.value = null
  }
}

function handleSvgMouseLeave() {
  hoveredCell.value = null
}

// Format display character
function formatChar(c: string): string {
  if (c === ' ') return '␣'
  if (c === '\0') return '∅'
  if (c === '') return '⋯'
  return c
}

// Get code point info
function getCodePointInfo(c: string): { hex: string; dec: number } {
  const codePoint = c.codePointAt(0) || 0
  return {
    hex: 'U+' + codePoint.toString(16).toUpperCase().padStart(4, '0'),
    dec: codePoint
  }
}

onMounted(async () => {
  await nextTick()
  await highlightCode()
  if (isExpanded.value) {
    loadSvg()
  }
})

watch(() => props.code, () => {
  highlightCode()
})

watch(isExpanded, (expanded) => {
  if (expanded && !svgContent.value) {
    loadSvg()
  }
})
</script>

<template>
  <div class="static-terminal-preview">
    <!-- Code Block -->
    <div class="code-container" :class="{ expanded: isExpanded }">
      <div class="code-header">
        <span class="language-label">{{ language }}</span>
        <div class="header-actions">
          <button 
            class="icon-button" 
            @click="toggleExpanded"
            :title="isExpanded ? 'Hide output' : 'Show output'"
            :class="{ active: isExpanded }"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" :class="{ rotated: isExpanded }">
              <polyline points="6 9 12 15 18 9"></polyline>
            </svg>
          </button>
          <button 
            class="icon-button" 
            @click="copyCode"
            :title="copied ? 'Copied!' : 'Copy code'"
          >
            <svg v-if="!copied" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
            <svg v-else xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
          </button>
        </div>
      </div>
      <div 
        v-if="highlightedCode" 
        class="code-block highlighted"
        v-html="highlightedCode"
      ></div>
      <pre v-else class="code-block"><code>{{ actualCode }}</code></pre>
      
      <!-- Expandable SVG Preview Panel -->
      <Transition name="slide">
        <div v-if="isExpanded" class="preview-panel">
          <div class="preview-header">
            <span class="preview-label">Output</span>
          </div>
          <div 
            ref="svgContainerRef"
            class="svg-container"
            @mousemove="handleSvgMouseMove"
            @mouseleave="handleSvgMouseLeave"
          >
            <div v-html="svgContent" class="svg-wrapper"></div>
            <div 
              ref="highlightRef"
              class="cell-highlight"
              :class="{ visible: hoveredCell !== null }"
            ></div>
          </div>
        </div>
      </Transition>
    </div>

    <!-- Cell Inspector Tooltip (teleported to body) -->
    <Teleport to="body">
      <div 
        v-if="hoveredCell"
        ref="tooltipRef"
        class="cell-tooltip"
      >
        <div class="tooltip-header">
          <div 
            class="tooltip-char"
            :style="{
              color: hoveredCell.cell.fg ? `rgb(${hoveredCell.cell.fg.r},${hoveredCell.cell.fg.g},${hoveredCell.cell.fg.b})` : undefined,
              background: hoveredCell.cell.bg ? `rgb(${hoveredCell.cell.bg.r},${hoveredCell.cell.bg.g},${hoveredCell.cell.bg.b})` : '#1e1e1e'
            }"
          >{{ formatChar(hoveredCell.cell.c) }}</div>
          <div class="tooltip-position">
            <div class="tooltip-cell-coords">Cell ({{ hoveredCell.x }}, {{ hoveredCell.y }})</div>
            <div class="tooltip-cell-desc">Column {{ hoveredCell.x }}, Row {{ hoveredCell.y }}</div>
          </div>
        </div>
        
        <div class="tooltip-section">
          <div class="tooltip-label">Character</div>
          <div class="tooltip-value">
            '{{ hoveredCell.cell.c === '\0' ? '\\0' : hoveredCell.cell.c }}' · 
            {{ getCodePointInfo(hoveredCell.cell.c).hex }} · 
            Dec: {{ getCodePointInfo(hoveredCell.cell.c).dec }}
          </div>
        </div>
        
        <div class="tooltip-colors">
          <div class="tooltip-section">
            <div class="tooltip-label">Foreground</div>
            <div class="tooltip-value">
              <template v-if="hoveredCell.cell.fg">
                <span 
                  class="color-swatch" 
                  :style="{ background: rgbToHex(hoveredCell.cell.fg.r, hoveredCell.cell.fg.g, hoveredCell.cell.fg.b) }"
                ></span>
                {{ rgbToHex(hoveredCell.cell.fg.r, hoveredCell.cell.fg.g, hoveredCell.cell.fg.b) }}
              </template>
              <template v-else>
                <span class="color-default">Default</span>
              </template>
            </div>
          </div>
          <div class="tooltip-section">
            <div class="tooltip-label">Background</div>
            <div class="tooltip-value">
              <template v-if="hoveredCell.cell.bg">
                <span 
                  class="color-swatch" 
                  :style="{ background: rgbToHex(hoveredCell.cell.bg.r, hoveredCell.cell.bg.g, hoveredCell.cell.bg.b) }"
                ></span>
                {{ rgbToHex(hoveredCell.cell.bg.r, hoveredCell.cell.bg.g, hoveredCell.cell.bg.b) }}
              </template>
              <template v-else>
                <span class="color-default">Default</span>
              </template>
            </div>
          </div>
        </div>
        
        <div class="tooltip-section" v-if="hoveredCell.cell.a !== 0">
          <div class="tooltip-label">Attributes</div>
          <div class="tooltip-badges">
            <span 
              v-for="badge in getAttributeBadges(hoveredCell.cell.a)" 
              :key="badge"
              class="attr-badge"
            >{{ badge }}</span>
          </div>
        </div>
        
        <div class="tooltip-section" v-if="hoveredCell.cell.link">
          <div class="tooltip-label">Hyperlink</div>
          <div class="tooltip-value tooltip-link">{{ hoveredCell.cell.link.uri }}</div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.static-terminal-preview {
  margin: 1.5rem 0;
}

/* Code Container */
.code-container {
  border-radius: 8px;
  overflow: hidden;
  background: #2d333b;
  border: 1px solid var(--vp-c-divider);
}

/* Light mode container */
:root:not(.dark) .code-container {
  background: #f6f8fa;
}

.code-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: rgba(0, 0, 0, 0.15);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

:root:not(.dark) .code-header {
  background: rgba(0, 0, 0, 0.05);
  border-bottom: 1px solid rgba(0, 0, 0, 0.1);
}

.language-label {
  font-size: 12px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.5);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

:root:not(.dark) .language-label {
  color: rgba(0, 0, 0, 0.5);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.icon-button {
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

.icon-button:hover {
  background: rgba(255, 255, 255, 0.1);
}

:root:not(.dark) .icon-button:hover {
  background: rgba(0, 0, 0, 0.1);
}

.icon-button svg {
  width: 16px;
  height: 16px;
  color: rgba(255, 255, 255, 0.5);
  transition: all 0.2s ease;
}

:root:not(.dark) .icon-button svg {
  color: rgba(0, 0, 0, 0.5);
}

.icon-button:hover svg {
  color: rgba(255, 255, 255, 0.8);
}

:root:not(.dark) .icon-button:hover svg {
  color: rgba(0, 0, 0, 0.8);
}

.icon-button.active svg {
  color: #4ecdc4;
}

.icon-button svg.rotated {
  transform: rotate(180deg);
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

:root:not(.dark) .code-block.highlighted :deep(code::before) {
  background: rgba(0, 0, 0, 0.1);
}

:root:not(.dark) .code-block.highlighted :deep(code .line::before) {
  color: rgba(0, 0, 0, 0.4);
}

/* Preview Panel */
.preview-panel {
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  background: #0f0f1a;
}

:root:not(.dark) .preview-panel {
  border-top: 1px solid rgba(0, 0, 0, 0.1);
  background: #1a1a2e;
}

.preview-header {
  display: flex;
  align-items: center;
  padding: 10px 16px;
  background: rgba(0, 0, 0, 0.15);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.preview-label {
  font-size: 12px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.5);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.svg-container {
  position: relative;
  padding: 1rem;
  display: flex;
  justify-content: center;
}

.svg-wrapper {
  display: flex;
  width: 100%;
}

.svg-wrapper :deep(svg) {
  display: block;
  width: 100%;
  height: auto;
}

/* Cell Highlight */
.cell-highlight {
  position: absolute;
  pointer-events: none;
  border: 1px solid #00d4ff;
  border-radius: 2px;
  opacity: 0;
  transition: opacity 0.1s;
  box-shadow: 0 0 8px rgba(0, 212, 255, 0.5);
}

.cell-highlight.visible {
  opacity: 1;
}

/* Slide Transition */
.slide-enter-active,
.slide-leave-active {
  transition: all 0.2s ease;
  overflow: hidden;
}

.slide-enter-from,
.slide-leave-to {
  opacity: 0;
  max-height: 0;
}

.slide-enter-to,
.slide-leave-from {
  opacity: 1;
  max-height: 500px;
}

/* Cell Tooltip */
.cell-tooltip {
  position: fixed;
  z-index: 9999;
  background: #2d2d44;
  border: 1px solid #444;
  border-radius: 8px;
  padding: 12px 16px;
  font-size: 13px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
  pointer-events: none;
  min-width: 240px;
  max-width: 360px;
  color: #eee;
}

.tooltip-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 10px;
  padding-bottom: 10px;
  border-bottom: 1px solid #444;
}

.tooltip-char {
  font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
  font-size: 24px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  border: 1px solid #555;
}

.tooltip-position {
  flex: 1;
}

.tooltip-cell-coords {
  font-size: 14px;
  font-weight: 500;
}

.tooltip-cell-desc {
  font-size: 11px;
  color: #888;
}

.tooltip-section {
  margin-bottom: 8px;
}

.tooltip-section:last-child {
  margin-bottom: 0;
}

.tooltip-label {
  color: #888;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 2px;
}

.tooltip-value {
  font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
  font-size: 12px;
}

.tooltip-colors {
  display: flex;
  gap: 16px;
}

.tooltip-colors .tooltip-section {
  flex: 1;
}

.color-swatch {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 2px;
  border: 1px solid #666;
  vertical-align: middle;
  margin-right: 6px;
}

.color-default {
  color: #666;
  font-style: italic;
}

.tooltip-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.attr-badge {
  display: inline-block;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 10px;
  background: #3d3d5c;
  color: #ccc;
}

.tooltip-link {
  color: #00d4ff;
  word-break: break-all;
}
</style>
