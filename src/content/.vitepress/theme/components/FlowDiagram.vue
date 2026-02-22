<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const phase = ref(0)
const spinnerFrame = ref(0)
const PHASES = 10
const PHASE_DURATION = 2000
let timer: ReturnType<typeof setInterval> | null = null
let spinnerTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  timer = setInterval(() => {
    phase.value = (phase.value + 1) % PHASES
  }, PHASE_DURATION)
  spinnerTimer = setInterval(() => {
    spinnerFrame.value = (spinnerFrame.value + 1) % 4
  }, 200)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
  if (spinnerTimer) clearInterval(spinnerTimer)
})
</script>

<template>
  <div class="flow-diagram-wrapper">
    <svg viewBox="0 0 400 340" xmlns="http://www.w3.org/2000/svg" class="flow-diagram">
      <defs>
        <!-- Scrollback fade gradient -->
        <linearGradient id="scrollback-fade" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stop-color="var(--fd-bg)" stop-opacity="1" />
          <stop offset="100%" stop-color="var(--fd-bg)" stop-opacity="0" />
        </linearGradient>
        <!-- Clip to viewport -->
        <clipPath id="viewport-clip">
          <rect x="21" y="31" width="358" height="240" rx="2" />
        </clipPath>
      </defs>

      <!-- Terminal chrome -->
      <rect x="10" y="10" width="380" height="270" rx="8" class="fd-terminal-frame" />
      <!-- Title bar -->
      <rect x="10" y="10" width="380" height="24" rx="8" class="fd-title-bar" />
      <rect x="10" y="26" width="380" height="8" class="fd-title-bar" />
      <!-- Window buttons -->
      <circle cx="28" cy="22" r="5" class="fd-btn-close" />
      <circle cx="44" cy="22" r="5" class="fd-btn-minimize" />
      <circle cx="60" cy="22" r="5" class="fd-btn-maximize" />
      <text x="200" y="25" text-anchor="middle" class="fd-title-text">Terminal</text>

      <!-- Viewport contents (clipped) -->
      <g clip-path="url(#viewport-clip)">
        <!-- All content shifts up when template list needs space (phase 5+) -->
        <g :class="['fd-content', { 'fd-scroll': phase >= 5 && phase <= 8 }]">

          <!-- Prompt line -->
          <text x="30" y="50" class="fd-prompt">$</text>
          <text x="44" y="50" class="fd-command">dotnet run</text>

          <!-- == STEP 1: Project name == -->
          <!-- Active (phases 1-2) -->
          <g v-if="phase >= 1 && phase <= 2" class="fd-step-enter">
            <rect x="25" y="58" width="350" height="50" rx="3" class="fd-active-region" />
            <text x="34" y="76" class="fd-widget-text">Enter your project name:</text>
            <rect x="34" y="82" width="260" height="20" rx="3" class="fd-textbox" />
            <text x="40" y="96" class="fd-textbox-text">{{ phase >= 2 ? 'my-app' : '' }}</text>
            <text x="42" y="96" class="fd-cursor" v-if="phase < 2">│</text>
            <text x="340" y="76" class="fd-label">Step 1</text>
          </g>
          <!-- Completed (phases 3+) -->
          <g v-if="phase >= 3" :class="{ 'fd-complete-enter': phase === 3 }">
            <text x="34" y="76" class="fd-completed-text">✓ Project: my-app</text>
          </g>

          <!-- == STEP 2: Docker support == -->
          <!-- Active (phases 3-4) -->
          <g v-if="phase >= 3 && phase <= 4" class="fd-step-enter">
            <rect x="25" y="84" width="350" height="46" rx="3" class="fd-active-region" />
            <text x="34" y="100" class="fd-widget-text">Add Docker support?</text>
            <rect x="34" y="106" width="60" height="20" rx="4" class="fd-button fd-button-primary" />
            <text x="64" y="120" text-anchor="middle" class="fd-button-text">Yes</text>
            <rect x="100" y="106" width="60" height="20" rx="4" class="fd-button" />
            <text x="130" y="120" text-anchor="middle" class="fd-button-text-dim">No</text>
            <text x="340" y="100" class="fd-label">Step 2</text>
          </g>
          <!-- Completed (phases 5+) -->
          <g v-if="phase >= 5" :class="{ 'fd-complete-enter': phase === 5 }">
            <text x="34" y="100" class="fd-completed-text">✓ Docker: Yes</text>
          </g>

          <!-- == STEP 3: Template selection (tall list) == -->
          <!-- Active (phases 5-6) -->
          <g v-if="phase >= 5 && phase <= 6" class="fd-step-enter">
            <rect x="25" y="108" width="350" height="190" rx="3" class="fd-active-region" />
            <text x="34" y="126" class="fd-widget-text">Select a template:</text>
            <!-- List items -->
            <rect x="34" y="132" width="280" height="18" rx="2" :class="['fd-list-item', { 'fd-list-selected': phase >= 6 }]" />
            <text x="40" y="146" class="fd-list-text">{{ phase >= 6 ? '›' : ' ' }} ASP.NET Core Web API</text>
            <rect x="34" y="152" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="166" class="fd-list-text-dim">  Blazor Server</text>
            <rect x="34" y="172" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="186" class="fd-list-text-dim">  Console Application</text>
            <rect x="34" y="192" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="206" class="fd-list-text-dim">  Worker Service</text>
            <rect x="34" y="212" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="226" class="fd-list-text-dim">  gRPC Service</text>
            <rect x="34" y="232" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="246" class="fd-list-text-dim">  Minimal API</text>
            <rect x="34" y="252" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="266" class="fd-list-text-dim">  Class Library</text>
            <rect x="34" y="272" width="280" height="18" rx="2" class="fd-list-item" />
            <text x="40" y="286" class="fd-list-text-dim">  xUnit Test Project</text>
            <text x="340" y="126" class="fd-label">Step 3</text>
          </g>
          <!-- Completed (phases 7+) -->
          <g v-if="phase >= 7" :class="{ 'fd-complete-enter': phase === 7 }">
            <text x="34" y="126" class="fd-completed-text">✓ Template: ASP.NET Core Web API</text>
          </g>

          <!-- == STEP 4: Activity spinner == -->
          <!-- Active (phase 7-8) -->
          <g v-if="phase >= 7 && phase <= 8" class="fd-step-enter">
            <rect x="25" y="134" width="350" height="24" rx="3" class="fd-active-region" />
            <text x="34" y="150" class="fd-spinner">{{ ['◐','◓','◑','◒'][spinnerFrame] }}</text>
            <text x="50" y="150" class="fd-widget-text">{{ phase === 7 ? 'Creating project structure...' : 'Installing packages...' }}</text>
            <text x="340" y="150" class="fd-label">Step 4</text>
          </g>
          <!-- Completed (phase 9) -->
          <g v-if="phase >= 9" :class="{ 'fd-complete-enter': phase === 9 }">
            <text x="34" y="150" class="fd-completed-text">✓ Project created!</text>
          </g>

          <!-- Return to prompt (phase 9) -->
          <g v-if="phase >= 9">
            <text x="30" y="170" class="fd-prompt">$</text>
            <text x="44" y="170" class="fd-cursor">│</text>
          </g>

        </g>

        <!-- Scrollback fade overlay (phase 5+, while scrolled) -->
        <rect v-if="phase >= 5 && phase <= 8" x="21" y="31" width="358" height="40" fill="url(#scrollback-fade)" class="fd-fade-in" />
      </g>

      <!-- Phase indicator dots -->
      <g transform="translate(200, 296)">
        <circle v-for="i in PHASES" :key="i"
          :cx="(i - 1) * 12 - (PHASES * 12 / 2) + 6"
          cy="0" r="3"
          :class="['fd-dot', { 'fd-dot-active': i - 1 === phase }]"
        />
      </g>

      <!-- Phase label -->
      <text x="200" y="320" text-anchor="middle" class="fd-phase-label">
        {{ ['Terminal ready',
            'Step 1: Project name',
            'User types "my-app"',
            'Step 1 completes → Step 2',
            'Step 2 completes',
            'Step 3: list scrolls content up',
            'User selects template',
            'Step 3 completes → spinner',
            'Creating project...',
            'Done — back to prompt'][phase] }}
      </text>
    </svg>
  </div>
</template>

<style scoped>
.flow-diagram-wrapper {
  margin: 1.5rem 0;
  display: flex;
  justify-content: center;
}

.flow-diagram {
  width: 100%;
  max-width: 520px;
  height: auto;
}

/* Theme variables */
.flow-diagram {
  --fd-bg: #1e1e2e;
  --fd-frame: #313244;
  --fd-title-bg: #2a2a3c;
  --fd-text: #cdd6f4;
  --fd-text-dim: #6c7086;
  --fd-active: rgba(137, 180, 250, 0.12);
  --fd-active-border: rgba(137, 180, 250, 0.4);
  --fd-completed: #a6e3a1;
  --fd-textbox-bg: #313244;
  --fd-textbox-border: #585b70;
  --fd-button-bg: #585b70;
  --fd-button-primary-bg: #89b4fa;
  --fd-prompt-color: #a6e3a1;
  --fd-label-color: rgba(137, 180, 250, 0.5);
  --fd-list-selected: rgba(137, 180, 250, 0.15);
  --fd-dot-inactive: #45475a;
  --fd-dot-active: #89b4fa;
}

:root:not(.dark) .flow-diagram {
  --fd-bg: #eff1f5;
  --fd-frame: #ccd0da;
  --fd-title-bg: #dce0e8;
  --fd-text: #4c4f69;
  --fd-text-dim: #9ca0b0;
  --fd-active: rgba(30, 102, 245, 0.08);
  --fd-active-border: rgba(30, 102, 245, 0.3);
  --fd-completed: #40a02b;
  --fd-textbox-bg: #e6e9ef;
  --fd-textbox-border: #bcc0cc;
  --fd-button-bg: #ccd0da;
  --fd-button-primary-bg: #1e66f5;
  --fd-prompt-color: #40a02b;
  --fd-label-color: rgba(30, 102, 245, 0.5);
  --fd-list-selected: rgba(30, 102, 245, 0.1);
  --fd-dot-inactive: #ccd0da;
  --fd-dot-active: #1e66f5;
}

/* Terminal frame */
.fd-terminal-frame {
  fill: var(--fd-bg);
  stroke: var(--fd-frame);
  stroke-width: 1.5;
}
.fd-title-bar {
  fill: var(--fd-title-bg);
}
.fd-btn-close { fill: #ed8796; }
.fd-btn-minimize { fill: #eed49f; }
.fd-btn-maximize { fill: #a6da95; }
.fd-title-text {
  fill: var(--fd-text-dim);
  font-size: 11px;
  font-family: system-ui, sans-serif;
}

/* Text styles */
.fd-prompt {
  fill: var(--fd-prompt-color);
  font-size: 13px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
  font-weight: 600;
}
.fd-command {
  fill: var(--fd-text);
  font-size: 13px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
}
.fd-widget-text {
  fill: var(--fd-text);
  font-size: 12px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
}
.fd-widget-text-sm {
  fill: var(--fd-text-dim);
  font-size: 11px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
}
.fd-completed-text {
  fill: var(--fd-completed);
  font-size: 12px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
  font-weight: 500;
}
.fd-label {
  fill: var(--fd-label-color);
  font-size: 10px;
  font-family: system-ui, sans-serif;
  font-style: italic;
}
.fd-spinner {
  fill: #cba6f7;
  font-size: 13px;
  font-family: monospace;
}

/* Active step region */
.fd-active-region {
  fill: var(--fd-active);
  stroke: var(--fd-active-border);
  stroke-width: 1;
  stroke-dasharray: 4 2;
}

/* Text box */
.fd-textbox {
  fill: var(--fd-textbox-bg);
  stroke: var(--fd-textbox-border);
  stroke-width: 1;
}
.fd-textbox-text {
  fill: var(--fd-text);
  font-size: 12px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
}
.fd-cursor {
  fill: var(--fd-text);
  font-size: 14px;
  font-family: monospace;
  animation: blink 1s step-end infinite;
}

/* List items */
.fd-list-item {
  fill: transparent;
}
.fd-list-selected {
  fill: var(--fd-list-selected);
}
.fd-list-text {
  fill: var(--fd-text);
  font-size: 12px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
}
.fd-list-text-dim {
  fill: var(--fd-text-dim);
  font-size: 12px;
  font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
}

/* Buttons */
.fd-button {
  fill: var(--fd-button-bg);
  stroke: none;
}
.fd-button-primary {
  fill: var(--fd-button-primary-bg);
}
.fd-button-text {
  fill: var(--fd-bg);
  font-size: 11px;
  font-family: system-ui, sans-serif;
  font-weight: 600;
}
.fd-button-text-dim {
  fill: var(--fd-text-dim);
  font-size: 11px;
  font-family: system-ui, sans-serif;
}

/* Phase dots */
.fd-dot {
  fill: var(--fd-dot-inactive);
  transition: fill 0.3s ease;
}
.fd-dot-active {
  fill: var(--fd-dot-active);
}

.fd-phase-label {
  fill: var(--fd-text-dim);
  font-size: 12px;
  font-family: system-ui, sans-serif;
}

/* Animations */
.fd-step-enter {
  animation: step-appear 0.4s ease-out;
}
.fd-complete-enter {
  animation: complete-appear 0.3s ease-out;
}
.fd-fade-in {
  animation: fade-in 0.5s ease-out;
}

.fd-scroll {
  animation: scroll-up 0.6s ease-in-out forwards;
}

@keyframes step-appear {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

@keyframes complete-appear {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes scroll-up {
  from { transform: translateY(0); }
  to { transform: translateY(-60px); }
}

@keyframes blink {
  50% { opacity: 0; }
}
</style>
