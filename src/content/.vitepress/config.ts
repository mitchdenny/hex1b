import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: 'Hex1b',
  description: 'A .NET TUI library with a React-inspired declarative API',
  cleanUrls: true,
  
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }]
  ],

  themeConfig: {
    logo: '/logo.svg',
    
    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'Deep Dives', link: '/deep-dives/reconciliation' },
      { text: 'API', link: '/api/' },
      { text: 'Gallery', link: '/gallery' }
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Introduction',
          items: [
            { text: 'Getting Started', link: '/guide/getting-started' },
            { text: 'Your First App', link: '/guide/first-app' }
          ]
        },
        {
          text: 'Core Concepts',
          items: [
            { text: 'Widgets & Nodes', link: '/guide/widgets-and-nodes' },
            { text: 'Layout System', link: '/guide/layout' },
            { text: 'Input Handling', link: '/guide/input' },
            { text: 'Theming', link: '/guide/theming' }
          ]
        },
        {
          text: 'Widgets',
          items: [
            { text: 'Text & TextBlock', link: '/guide/widgets/text' },
            { text: 'Button', link: '/guide/widgets/button' },
            { text: 'TextBox', link: '/guide/widgets/textbox' },
            { text: 'List', link: '/guide/widgets/list' },
            { text: 'Stacks (HStack/VStack)', link: '/guide/widgets/stacks' },
            { text: 'Border & Panel', link: '/guide/widgets/containers' },
            { text: 'Navigator', link: '/guide/widgets/navigator' }
          ]
        }
      ],
      '/deep-dives/': [
        {
          text: 'Deep Dives',
          items: [
            { text: 'Reconciliation', link: '/deep-dives/reconciliation' },
            { text: 'The Render Loop', link: '/deep-dives/render-loop' },
            { text: 'Focus System', link: '/deep-dives/focus-system' },
            { text: 'ANSI & Terminal Rendering', link: '/deep-dives/terminal-rendering' }
          ]
        }
      ],
      '/api/': [
        {
          text: 'API Reference',
          items: [
            { text: 'Overview', link: '/api/' }
          ]
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/mitchdenny/hex1b' }
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2025 Mitch Denny'
    },

    search: {
      provider: 'local'
    }
  },

  vite: {
    optimizeDeps: {
      include: ['@xterm/xterm', '@xterm/addon-unicode11', '@xterm/addon-image']
    },
    ssr: {
      noExternal: ['@xterm/xterm', '@xterm/addon-unicode11', '@xterm/addon-image']
    },
    server: {
      proxy: {
        '/apps': {
          target: process.env.WEBSITE_HTTPS || process.env.WEBSITE_HTTP || 'http://localhost:5000',
          changeOrigin: true,
          ws: true,
          secure: false,
          rewriteWsOrigin: true
        },
        '/examples': {
          target: process.env.WEBSITE_HTTPS || process.env.WEBSITE_HTTP || 'http://localhost:5000',
          changeOrigin: true,
          ws: true,
          secure: false,
          rewriteWsOrigin: true
        }
      }
    }
  }
})
