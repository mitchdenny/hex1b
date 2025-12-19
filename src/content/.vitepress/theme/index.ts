import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import TerminalDemo from './components/TerminalDemo.vue'
import TerminalGallery from './components/TerminalGallery.vue'
import FloatingTerminal from './components/FloatingTerminal.vue'
import FeatureSamples from './components/FeatureSamples.vue'
import InstallGuide from './components/InstallGuide.vue'
import TerminalCommand from './components/TerminalCommand.vue'
import CodeBlock from './components/CodeBlock.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    // Register global components
    app.component('TerminalDemo', TerminalDemo)
    app.component('TerminalGallery', TerminalGallery)
    app.component('FloatingTerminal', FloatingTerminal)
    app.component('FeatureSamples', FeatureSamples)
    app.component('InstallGuide', InstallGuide)
    app.component('TerminalCommand', TerminalCommand)
    app.component('CodeBlock', CodeBlock)
  }
} satisfies Theme
