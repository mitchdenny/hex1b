import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: 'Hex1b',
  description: 'A .NET TUI library with a React-inspired declarative API',
  cleanUrls: true,
  
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }]
  ],

  markdown: {
    lineNumbers: true
  },

  themeConfig: {
    logo: '/logo.svg',
    
    nav: [
      { text: 'Guide', link: '/guide/' },
      { text: 'API Reference', link: '/reference/' }
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Overview',
          items: [
            { text: 'Guide', link: '/guide/' }
          ]
        },
        {
          text: 'Features',
          items: [
            { text: 'Terminal UIs', link: '/guide/tui' },
            { text: 'Terminal Emulator', link: '/guide/terminal-emulator' },
            { text: 'Automation & Testing', link: '/guide/testing' }
          ]
        },
        {
          text: 'Building TUIs',
          items: [
            { text: 'Your First App', link: '/guide/getting-started' },
            { text: 'Widgets & Nodes', link: '/guide/widgets-and-nodes' },
            { text: 'Layout System', link: '/guide/layout' },
            { text: 'Input Handling', link: '/guide/input' },
            { text: 'Theming', link: '/guide/theming' }
          ]
        },
        {
          text: 'Terminal Stack',
          items: [
            { text: 'Using the Emulator', link: '/guide/using-the-emulator' },
            { text: 'Presentation Adapters', link: '/guide/presentation-adapters' },
            { text: 'Workload Adapters', link: '/guide/workload-adapters' }
          ]
        },
        {
          text: 'Reference',
          items: [
            { text: 'Widgets', link: '/guide/widgets/' },
            { text: 'API Docs', link: '/reference/' }
          ]
        },
        {
          text: 'Tools',
          items: [
            { text: 'MCP Server', link: '/guide/mcp-server' }
          ]
        }
      ],
      '/reference/': [
        {
          text: 'API Reference',
          items: [
            { text: 'Overview', link: '/reference/' }
          ]
        },
        {
          text: 'Namespaces',
          items: [
            { text: 'Hex1b', link: '/reference/Hex1b' },
            { text: 'Hex1b.Animation', link: '/reference/Hex1b.Animation' },
            { text: 'Hex1b.Automation', link: '/reference/Hex1b.Automation' },
            { text: 'Hex1b.Events', link: '/reference/Hex1b.Events' },
            { text: 'Hex1b.Input', link: '/reference/Hex1b.Input' },
            { text: 'Hex1b.Layout', link: '/reference/Hex1b.Layout' },
            { text: 'Hex1b.Nodes', link: '/reference/Hex1b.Nodes' },
            { text: 'Hex1b.Surfaces', link: '/reference/Hex1b.Surfaces' },
            { text: 'Hex1b.Theming', link: '/reference/Hex1b.Theming' },
            { text: 'Hex1b.Tokens', link: '/reference/Hex1b.Tokens' },
            { text: 'Hex1b.Widgets', link: '/reference/Hex1b.Widgets' }
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
      include: ['@xterm/xterm', '@xterm/addon-unicode11', '@xterm/addon-image', '@xterm/addon-web-links']
    },
    ssr: {
      noExternal: ['@xterm/xterm', '@xterm/addon-unicode11', '@xterm/addon-image', '@xterm/addon-web-links']
    },
    server: {
      proxy: {
        '/api': {
          target: process.env.WEBSITE_HTTPS || process.env.WEBSITE_HTTP || 'http://localhost:5000',
          changeOrigin: true,
          secure: false
        },
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
