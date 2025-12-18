<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { codeToHtml } from 'shiki'

interface InstallOption {
  id: string
  label: string
  code: string
  lang: string
}

const options: InstallOption[] = [
  {
    id: 'dotnet-cli',
    label: '.NET CLI',
    code: 'dotnet add package Hex1b@0.1.0',
    lang: 'bash'
  },
  {
    id: 'package-reference',
    label: 'PackageReference',
    code: '<PackageReference Include="Hex1b" Version="0.1.0" />',
    lang: 'xml'
  },
  {
    id: 'file-apps',
    label: 'File-based apps',
    code: '#:package Hex1b@0.1.0',
    lang: 'csharp'
  }
]

const selectedOption = ref('dotnet-cli')
const highlightedCode = ref<Record<string, string>>({})
const dropdownOpen = ref(false)
const dropdownRef = ref<HTMLElement | null>(null)

const currentOption = computed(() => options.find(o => o.id === selectedOption.value))
const currentCode = computed(() => highlightedCode.value[selectedOption.value])
const currentCodeFallback = computed(() => currentOption.value?.code || '')

async function highlightOptions() {
  for (const option of options) {
    highlightedCode.value[option.id] = await codeToHtml(option.code, {
      lang: option.lang,
      theme: 'github-dark'
    })
  }
}

function selectOption(id: string) {
  selectedOption.value = id
  dropdownOpen.value = false
}

function toggleDropdown() {
  dropdownOpen.value = !dropdownOpen.value
}

function copyToClipboard() {
  const option = options.find(o => o.id === selectedOption.value)
  if (option) {
    navigator.clipboard.writeText(option.code)
  }
}

function handleClickOutside(event: MouseEvent) {
  if (dropdownRef.value && !dropdownRef.value.contains(event.target as Node)) {
    dropdownOpen.value = false
  }
}

onMounted(() => {
  highlightOptions()
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <div class="install-guide">
    <p class="install-cta">Add the Hex1b package to your app:</p>
    <div class="install-card">
      <!-- Left: Dropdown selector -->
      <div class="dropdown-section" ref="dropdownRef">
        <button class="dropdown-trigger" @click.stop="toggleDropdown">
          <span class="dropdown-label">{{ currentOption?.label }}</span>
          <svg class="dropdown-arrow" :class="{ open: dropdownOpen }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="6 9 12 15 18 9"></polyline>
          </svg>
        </button>
        <div v-if="dropdownOpen" class="dropdown-menu">
          <button
            v-for="option in options"
            :key="option.id"
            class="dropdown-item"
            :class="{ active: selectedOption === option.id }"
            @click.stop="selectOption(option.id)"
          >
            {{ option.label }}
          </button>
        </div>
      </div>
      
      <!-- Right: Code display -->
      <div class="code-section">
        <div class="install-code">
          <div 
            v-if="currentCode" 
            class="code-content"
            v-html="currentCode"
          ></div>
          <code v-else class="code-content-fallback">{{ currentCodeFallback }}</code>
        </div>
        
        <button class="copy-button" @click="copyToClipboard" title="Copy to clipboard">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
          </svg>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.install-guide {
  margin: 48px 0;
  padding: 0;
}

.install-cta {
  text-align: left;
  font-size: 20px;
  font-weight: 600;
  color: var(--vp-c-text-1);
  margin: 0 0 16px 0;
  padding-left: 32px;
}

.install-title {
  font-size: 32px;
  font-weight: 700;
  color: var(--vp-c-text-1);
  margin: 0 0 8px 0;
}

.install-subtitle {
  font-size: 16px;
  color: var(--vp-c-text-2);
  margin: 0 0 24px 0;
}

.install-card {
  display: flex;
  align-items: stretch;
  border-radius: 8px;
  overflow: visible;
}

/* Left dropdown section */
.dropdown-section {
  position: relative;
  flex-shrink: 0;
}

.dropdown-trigger {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 100%;
  padding: 14px 16px 14px 32px;
  background: #0f0f1a;
  border: none;
  border-radius: 8px 0 0 8px;
  color: var(--vp-c-brand-1);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  min-width: 200px;
  justify-content: space-between;
}

.dropdown-trigger:hover {
  background: #1a1a2e;
}

.dropdown-arrow {
  width: 16px;
  height: 16px;
  transition: transform 0.2s ease;
}

.dropdown-arrow.open {
  transform: rotate(180deg);
}

.dropdown-menu {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 4px;
  background: #0f0f1a;
  border: 1px solid rgba(78, 205, 196, 0.3);
  border-radius: 8px;
  overflow: hidden;
  z-index: 100;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

.dropdown-item {
  display: block;
  width: 100%;
  padding: 12px 16px;
  background: transparent;
  border: none;
  color: rgba(255, 255, 255, 0.8);
  font-size: 14px;
  font-weight: 500;
  text-align: left;
  cursor: pointer;
  transition: all 0.15s ease;
}

.dropdown-item:hover {
  background: rgba(78, 205, 196, 0.1);
  color: #ffffff;
}

.dropdown-item.active {
  color: var(--vp-c-brand-1);
  background: rgba(78, 205, 196, 0.15);
}

/* Right code section */
.code-section {
  flex: 1;
  display: flex;
  align-items: center;
  background: linear-gradient(135deg, #4ecdc4 0%, #44a8a0 100%);
  padding: 14px 16px;
  gap: 12px;
  border-radius: 0 8px 8px 0;
}

.install-code {
  flex: 1;
  text-align: left;
  overflow-x: auto;
}

.code-content :deep(pre) {
  margin: 0;
  padding: 0;
  background: transparent !important;
}

.code-content :deep(code) {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 14px;
  line-height: 1.5;
  color: #0f0f1a !important;
}

.code-content :deep(span) {
  color: #0f0f1a !important;
}

.code-content-fallback {
  font-family: 'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace;
  font-size: 14px;
  color: #0f0f1a;
}

.copy-button {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  background: rgba(15, 15, 26, 0.2);
  border: none;
  border-radius: 6px;
  color: #0f0f1a;
  cursor: pointer;
  transition: all 0.2s ease;
}

.copy-button:hover {
  background: rgba(15, 15, 26, 0.3);
}

.copy-button svg {
  width: 16px;
  height: 16px;
}

@media (max-width: 600px) {
  .install-card {
    flex-direction: column;
  }
  
  .dropdown-trigger {
    border-radius: 8px 8px 0 0;
    min-width: unset;
  }
  
  .code-section {
    border-radius: 0 0 8px 8px;
  }
}
</style>
