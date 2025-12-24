```vue
<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { codeToHtml } from 'shiki'
import FloatingTerminal from './FloatingTerminal.vue'

const props = defineProps<{
  command: string
  example?: string
  title?: string
}>()

const packageVersion = ref<string | null>(null)
const highlightedCode = ref<string>('')
const copied = ref(false)
const terminalRef = ref<InstanceType<typeof FloatingTerminal> | null>(null)
const backendAvailable = ref(false)

// Replace {{version}} placeholder with the actual version
const resolvedCommand = computed(() => {
  if (packageVersion.value && props.command.includes('{{version}}')) {
    return props.command.replace(/\{\{version\}\}/g, packageVersion.value)
  }
  return props.command
})

async function fetchVersion() {
  try {
    const response = await fetch('/api/version')
    if (response.ok) {
      const data = await response.json()
      if (data.version) {
        packageVersion.value = data.version
      }
    }
  } catch {
    // Use command as-is on error
  }
}

async function checkBackend() {
  try {
    const response = await fetch('/apps')
    backendAvailable.value = response.ok
  } catch {
    backendAvailable.value = false
  }
}

async function highlightCommand() {
  highlightedCode.value = await codeToHtml(resolvedCommand.value, {
    lang: 'bash',
    theme: 'github-dark'
  })
}

// Re-highlight when resolved command changes
watch(resolvedCommand, () => {
  highlightCommand()
})

function copyToClipboard() {
  navigator.clipboard.writeText(resolvedCommand.value)
  copied.value = true
  setTimeout(() => {
    copied.value = false
  }, 2000)
}

function openDemo() {
  if (terminalRef.value) {
    terminalRef.value.openTerminal()
  }
}

onMounted(() => {
  // Fetch version if command contains the placeholder
  if (props.command.includes('{{version}}')) {
    fetchVersion()
  }
  highlightCommand()
  if (props.example) {
    checkBackend()
  }
})
</script>

<template>
  <div class="terminal-command-wrapper">
    <div class="terminal-command">
      <div class="terminal-icon-box">
        <svg class="terminal-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="4 17 10 11 4 5"></polyline>
          <line x1="12" y1="19" x2="20" y2="19"></line>
        </svg>
      </div>
      <div class="command-content">
        <div 
          v-if="highlightedCode" 
          class="code-display"
          v-html="highlightedCode"
        ></div>
        <code v-else class="code-fallback">{{ resolvedCommand }}</code>
      </div>
      <button class="copy-button" @click="copyToClipboard" :title="copied ? 'Copied!' : 'Copy command'">
        <svg v-if="!copied" class="copy-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
        </svg>
        <svg v-else class="copy-icon check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
      </button>
      
      <!-- Integrated demo button when example is provided -->
      <ClientOnly>
        <button 
          v-if="example && backendAvailable"
          class="demo-button" 
          @click="openDemo"
          title="Open in terminal"
        >
          <svg class="play-icon" viewBox="0 0 24 24" fill="currentColor">
            <polygon points="5 3 19 12 5 21 5 3"></polygon>
          </svg>
          <span class="demo-label">Run in browser</span>
        </button>
      </ClientOnly>
    </div>
    
    <!-- Hidden FloatingTerminal -->
    <ClientOnly>
      <div v-if="example && backendAvailable" class="hidden-terminal">
        <FloatingTerminal 
          ref="terminalRef"
          :example="example"
          :title="title || 'Demo'"
        />
      </div>
    </ClientOnly>
  </div>
</template>

<style scoped>
.terminal-command-wrapper {
  margin: 16px 0;
}

.terminal-command {
  display: flex;
  align-items: stretch;
  border-radius: 8px;
  overflow: hidden;
}

.terminal-icon-box {
  display: flex;
  align-items: center;
  justify-content: center;
  background: #3d9690;
  padding: 14px 16px;
  flex-shrink: 0;
}

.terminal-icon {
  width: 20px;
  height: 20px;
  color: #0f3d3a;
}

.command-content {
  flex: 1;
  display: flex;
  align-items: center;
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
  padding: 14px 16px;
  overflow-x: auto;
  scrollbar-width: thin; /* Firefox - thin scrollbar */
  scrollbar-color: #3d9690 #44a8a0; /* Firefox - thumb and track colors */
}

/* Webkit browsers (Chrome, Safari, Edge) */
.command-content::-webkit-scrollbar {
  height: 8px; /* Thin scrollbar */
}

.command-content::-webkit-scrollbar-track {
  background: #44a8a0; /* Match the darker part of the gradient */
  border-radius: 4px;
}

.command-content::-webkit-scrollbar-thumb {
  background: #3d9690; /* Darker teal for the thumb */
  border-radius: 4px;
}

.command-content::-webkit-scrollbar-thumb:hover {
  background: #2d7e78; /* Even darker on hover */
}

.code-display :deep(pre) {
  margin: 0;
  padding: 0;
  background: transparent !important;
}

.code-display :deep(code) {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 15px;
  font-weight: 600;
  line-height: 1.5;
  color: #0f3d3a !important;
  position: relative;
  top: 1px;
}

.code-display :deep(span) {
  color: #0f3d3a !important;
}

.code-fallback {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 15px;
  font-weight: 600;
  color: #0f3d3a;
  position: relative;
  top: 1px;
}

.copy-button {
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #44a8a0 0%, #3d9690 100%);
  border: none;
  padding: 14px 16px;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.copy-button:hover {
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
}

.copy-icon {
  width: 18px;
  height: 18px;
  color: #0f3d3a;
}

.copy-icon.check {
  color: #0f3d3a;
}

/* Integrated demo button */
.demo-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  background: #0f0f1a;
  border: none;
  padding: 14px 20px;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.demo-button:hover {
  background: #1a1a2e;
}

.demo-button:hover .play-icon {
  transform: scale(1.1);
}

.play-icon {
  width: 16px;
  height: 16px;
  color: #4ecdc4;
  transition: transform 0.2s ease;
}

.demo-label {
  font-size: 14px;
  font-weight: 600;
  color: #4ecdc4;
}

/* Hide the terminal trigger card */
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

@media (max-width: 600px) {
  .terminal-command {
    flex-wrap: wrap;
  }
  
  .terminal-icon-box {
    padding: 12px;
  }
  
  .command-content {
    flex: 1 1 auto;
    min-width: 0;
    padding: 12px 16px;
  }
  
  .copy-button {
    padding: 12px;
  }
  
  .demo-button {
    padding: 12px 16px;
  }
}
</style>
```
