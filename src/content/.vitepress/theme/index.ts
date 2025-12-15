import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import TerminalDemo from './components/TerminalDemo.vue'
import TerminalGallery from './components/TerminalGallery.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    // Register global components
    app.component('TerminalDemo', TerminalDemo)
    app.component('TerminalGallery', TerminalGallery)
  }
} satisfies Theme
