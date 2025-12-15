---
layout: home

hero:
  name: "Hex1b"
  text: "Terminal UIs, the React Way"
  tagline: Build beautiful, interactive terminal applications with a declarative, component-based API for .NET
  image:
    src: /logo.svg
    alt: Hex1b
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: View Gallery
      link: /gallery
    - theme: alt
      text: GitHub
      link: https://github.com/your-username/hex1b

features:
  - icon: 🎯
    title: Declarative API
    details: Describe what your UI should look like, not how to draw it. Hex1b handles the rendering and state management.
  - icon: 🔄
    title: React-Inspired Reconciliation
    details: Efficient diffing algorithm preserves component state across re-renders, just like React's virtual DOM.
  - icon: 📐
    title: Flexible Layout System
    details: Constraint-based layout with HStack, VStack, and flexible sizing. Build responsive terminal UIs with ease.
  - icon: 🎨
    title: Theming Support
    details: Customize colors and styles with a powerful theming system. Dark mode friendly out of the box.
  - icon: ⌨️
    title: Smart Input Handling
    details: Built-in focus management, keyboard navigation, and input routing. Tab, Shift+Tab, and more.
  - icon: 🖼️
    title: Sixel Graphics
    details: Display images in supported terminals using the Sixel protocol. Perfect for data visualization.
---

<script setup>
import { VPTeamMembers } from 'vitepress/theme'
</script>

## Quick Example

```csharp
using Hex1b;

var app = new Hex1bApp<int>(
    initialState: 0,
    buildWidget: (ctx, ct) => 
        new VStackWidget([
            new TextBlockWidget($"Count: {ctx.State}"),
            new ButtonWidget("Increment", () => ctx.SetState(ctx.State + 1)),
            new ButtonWidget("Decrement", () => ctx.SetState(ctx.State - 1))
        ])
);

await app.RunAsync();
```

## Try It Live

See Hex1b in action with our interactive demos:

<div style="text-align: center; margin: 2rem 0;">
  <a href="/gallery" class="VPButton medium brand">
    Open Gallery →
  </a>
</div>
