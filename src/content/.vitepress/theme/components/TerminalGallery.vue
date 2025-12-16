<script setup lang="ts">
import { ref, onMounted } from 'vue'

interface Exhibit {
  id: string
  title: string
  description: string
  websocketUrl: string
}

const exhibits = ref<Exhibit[]>([])
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
      
      <!-- Gallery grid with FloatingTerminal triggers -->
      <div v-else class="gallery-grid">
        <div 
          v-for="exhibit in exhibits" 
          :key="exhibit.id"
          class="gallery-card-wrapper"
        >
          <FloatingTerminal 
            :exhibit="exhibit.id" 
            :title="exhibit.title" 
          />
          <div class="gallery-card-info">
            <div class="gallery-card-description">{{ exhibit.description }}</div>
          </div>
        </div>
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

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 24px;
}

.gallery-card-wrapper {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.gallery-card-info {
  padding: 0 8px;
}

.gallery-card-description {
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 1.5;
}
</style>
