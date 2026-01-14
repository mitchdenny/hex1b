<script setup lang="ts">
import DefaultTheme from 'vitepress/theme'
import HeroScreenshots from './components/HeroScreenshots.vue'

const { Layout } = DefaultTheme
</script>

<template>
  <Layout>
    <template #home-hero-image>
      <HeroScreenshots />
    </template>
  </Layout>
</template>

<style scoped>
.site-banner {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  padding: 12px 48px;
  background: linear-gradient(90deg, #1a1a2e 0%, #0d0d18 50%, #1a1a2e 100%);
  border-bottom: 1px solid rgba(78, 205, 196, 0.3);
  color: rgba(255, 255, 255, 0.9);
  font-size: 14px;
  position: relative;
  width: 100%;
  box-sizing: border-box;
}

.banner-content {
  text-align: center;
  flex: 1;
  max-width: 900px;
}

.banner-content a {
  color: #4ecdc4;
  text-decoration: none;
  margin-left: 8px;
  white-space: nowrap;
}

.banner-content a:hover {
  text-decoration: underline;
}

.banner-close {
  position: absolute;
  right: 16px;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: rgba(255, 255, 255, 0.6);
  font-size: 20px;
  cursor: pointer;
  padding: 4px 8px;
  line-height: 1;
}

.banner-close:hover {
  color: rgba(255, 255, 255, 0.9);
}
</style>
