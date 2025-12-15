<script setup lang="ts">
import { ref, onMounted } from 'vue'

interface Exhibit {
  id: string
  title: string
  description: string
  websocketUrl: string
}

const exhibits = ref<Exhibit[]>([])
const activeExhibit = ref<Exhibit | null>(null)
const isLoading = ref(true)
const error = ref<string | null>(null)

async function loadExhibits() {
  try {
    const response = await fetch('/apps')
    if (!response.ok) {
      throw new Error('Failed to load exhibits')
    }
    exhibits.value = await response.json()
    isLoading.value = false
  } catch (err) {
    console.error('Failed to load exhibits:', err)
    error.value = 'Failed to load gallery exhibits. Make sure the backend is running.'
    isLoading.value = false
  }
}

function selectExhibit(exhibit: Exhibit) {
  activeExhibit.value = exhibit
}

function closeExhibit() {
  activeExhibit.value = null
}

onMounted(() => {
  loadExhibits()
})
</script>

<template>
  <ClientOnly>
    <div class="gallery-container">
      <!-- Loading state -->
      <div v-if="isLoading" class="gallery-loading">
        Loading exhibits...
      </div>
      
      <!-- Error state -->
      <div v-else-if="error" class="gallery-error">
        {{ error }}
      </div>
      
      <!-- Gallery grid -->
      <div v-else-if="!activeExhibit" class="gallery-grid">
        <div 
          v-for="exhibit in exhibits" 
          :key="exhibit.id"
          class="gallery-card"
          @click="selectExhibit(exhibit)"
        >
          <div class="gallery-card-preview">
            <div class="gallery-card-placeholder">
              <span>{{ exhibit.title }}</span>
            </div>
          </div>
          <div class="gallery-card-info">
            <div class="gallery-card-title">{{ exhibit.title }}</div>
            <div class="gallery-card-description">{{ exhibit.description }}</div>
          </div>
        </div>
      </div>
      
      <!-- Active exhibit view -->
      <div v-else class="gallery-active">
        <button class="gallery-back" @click="closeExhibit">
          ← Back to Gallery
        </button>
        <TerminalDemo 
          :exhibit="activeExhibit.id" 
          :title="activeExhibit.title" 
        />
      </div>
    </div>
    
    <template #fallback>
      <div class="gallery-loading">Loading gallery...</div>
    </template>
  </ClientOnly>
</template>

<style scoped>
.gallery-container {
  margin-top: 24px;
}

.gallery-loading,
.gallery-error {
  text-align: center;
  padding: 48px;
  color: var(--vp-c-text-2);
}

.gallery-error {
  color: #ff6b6b;
}

.gallery-card-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--vp-c-text-3);
  font-family: 'Cascadia Code', 'Fira Code', monospace;
  font-size: 14px;
}

.gallery-back {
  background: var(--vp-c-bg-soft);
  border: 1px solid var(--vp-c-divider);
  color: var(--vp-c-text-1);
  padding: 8px 16px;
  border-radius: 8px;
  cursor: pointer;
  margin-bottom: 16px;
  transition: all 0.2s;
}

.gallery-back:hover {
  border-color: var(--vp-c-brand-1);
  color: var(--vp-c-brand-1);
}
</style>
