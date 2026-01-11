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
            { text: 'Guide', link: '/guide/' },
            { text: 'Terminal UIs', link: '/guide/tui' },
            { text: 'Terminal Emulator', link: '/guide/terminal-emulator' },
            { text: 'Automation & Testing', link: '/guide/testing' },
            { text: 'MCP Server', link: '/guide/mcp-server' }
          ]
        },
        {
          text: 'Getting Started',
          items: [
            { text: 'Installation', link: '/guide/getting-started' },
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
            { text: 'Align', link: '/guide/widgets/align' },
            { text: 'Border & ThemePanel', link: '/guide/widgets/containers' },
            { text: 'Button', link: '/guide/widgets/button' },
            { text: 'Hyperlink', link: '/guide/widgets/hyperlink' },
            { text: 'List', link: '/guide/widgets/list' },
            { text: 'Navigator', link: '/guide/widgets/navigator' },
            { text: 'Picker', link: '/guide/widgets/picker' },
            { text: 'Progress', link: '/guide/widgets/progress' },
            { text: 'Rescue', link: '/guide/widgets/rescue' },
            { text: 'Responsive', link: '/guide/widgets/responsive' },
            { text: 'Scroll', link: '/guide/widgets/scroll' },
            { text: 'Splitter', link: '/guide/widgets/splitter' },
            { text: 'Stacks (HStack/VStack)', link: '/guide/widgets/stacks' },
            { text: 'Text', link: '/guide/widgets/text' },
            { text: 'TextBox', link: '/guide/widgets/textbox' },
            { text: 'ThemePanel', link: '/guide/widgets/themepanel' },
            { text: 'ToggleSwitch', link: '/guide/widgets/toggle-switch' }
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
            { text: 'Hex1b.Widgets', link: '/reference/Hex1b.Widgets' },
            { text: 'Hex1b.Nodes', link: '/reference/Hex1b.Nodes' },
            { text: 'Hex1b.Layout', link: '/reference/Hex1b.Layout' },
            { text: 'Hex1b.Input', link: '/reference/Hex1b.Input' },
            { text: 'Hex1b.Events', link: '/reference/Hex1b.Events' },
            { text: 'Hex1b.Theming', link: '/reference/Hex1b.Theming' },
            { text: 'Hex1b.Tokens', link: '/reference/Hex1b.Tokens' },
            { text: 'Hex1b.Terminal', link: '/reference/Hex1b.Terminal' }
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
