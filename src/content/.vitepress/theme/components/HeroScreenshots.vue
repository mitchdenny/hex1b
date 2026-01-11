<script setup lang="ts">
import { ref } from 'vue'

// Screenshots to display - these would be paths to actual screenshots
const screenshots = ref([
  '/screenshots/demo-1.png',
  '/screenshots/demo-2.png',
])
</script>

<template>
  <div class="hero-screenshots">
    <div class="screenshot-stack">
      <!-- Placeholder terminal-style windows until real screenshots are added -->
      <div class="screenshot-window back">
        <div class="window-chrome">
          <span class="dot"></span>
          <span class="dot"></span>
          <span class="dot"></span>
        </div>
        <div class="window-content">
          <div class="fake-line short"></div>
          <div class="fake-line"></div>
          <div class="fake-line medium"></div>
          <div class="fake-line"></div>
          <div class="fake-line short"></div>
        </div>
      </div>
      <div class="screenshot-window front">
        <div class="window-chrome">
          <span class="dot"></span>
          <span class="dot"></span>
          <span class="dot"></span>
        </div>
        <div class="window-content">
          <div class="fake-ui-row">
            <div class="fake-button"></div>
            <div class="fake-button"></div>
            <div class="fake-button"></div>
          </div>
          <div class="fake-line"></div>
          <div class="fake-line medium"></div>
          <div class="fake-list">
            <div class="fake-list-item active"></div>
            <div class="fake-list-item"></div>
            <div class="fake-list-item"></div>
          </div>
        </div>
      </div>
    </div>
    <div class="gradient-overlay"></div>
  </div>
</template>

<style scoped>
.hero-screenshots {
  position: relative;
  width: 100%;
  height: 320px;
  pointer-events: none;
  overflow: visible;
}

.gradient-overlay {
  position: absolute;
  top: 0;
  left: -100px;
  bottom: 0;
  width: calc(100% + 100px);
  background: linear-gradient(
    to right,
    #0a0a12 0%,
    rgba(10, 10, 18, 0.95) 10%,
    rgba(10, 10, 18, 0.7) 25%,
    rgba(10, 10, 18, 0.3) 50%,
    rgba(10, 10, 18, 0) 75%
  );
  z-index: 2;
}

.screenshot-stack {
  position: relative;
  width: 100%;
  height: 100%;
}

.screenshot-window {
  position: absolute;
  background: #1a1a2e;
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
  overflow: hidden;
}

.screenshot-window.back {
  width: 280px;
  height: 190px;
  top: 10px;
  right: -30px;
  opacity: 0.5;
  transform: rotate(3deg);
}

.screenshot-window.front {
  width: 300px;
  height: 210px;
  top: 50px;
  right: 30px;
  transform: rotate(-2deg);
}

.window-chrome {
  display: flex;
  gap: 6px;
  padding: 10px 12px;
  background: #2d2d3a;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.window-chrome .dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #3d3d4a;
}

.window-content {
  padding: 16px;
}

.fake-line {
  height: 8px;
  background: rgba(78, 205, 196, 0.15);
  border-radius: 4px;
  margin-bottom: 8px;
}

.fake-line.short {
  width: 40%;
}

.fake-line.medium {
  width: 70%;
}

.fake-ui-row {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.fake-button {
  width: 60px;
  height: 24px;
  background: rgba(78, 205, 196, 0.3);
  border-radius: 4px;
}

.fake-list {
  margin-top: 8px;
}

.fake-list-item {
  height: 28px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 4px;
  margin-bottom: 4px;
}

.fake-list-item.active {
  background: rgba(78, 205, 196, 0.2);
  border: 1px solid rgba(78, 205, 196, 0.3);
}

/* Hide on smaller screens */
@media (max-width: 960px) {
  .hero-screenshots {
    display: none;
  }
}
</style>
