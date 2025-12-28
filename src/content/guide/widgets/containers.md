# Container Widgets

This page has been split into individual widget documentation pages for better organization.

## BorderWidget

BorderWidget draws a decorative box border around content with optional title support.

**[View full BorderWidget documentation →](/guide/widgets/border)**

Key features:
- Box drawing characters for visual borders
- Optional title in the top border
- Customizable via theming
- Clipping support for child content

## PanelWidget

PanelWidget provides a styled background for content without adding decorative borders.

**[View full PanelWidget documentation →](/guide/widgets/panel)**

Key features:
- Background fill color
- Foreground color inheritance
- No size overhead
- Combine with borders for layered styling

## ThemePanelWidget

ThemePanelWidget applies scoped theme mutations to a subtree of widgets.

**[View full ThemePanelWidget documentation →](/guide/widgets/themepanel)**

Key features:
- Scoped theme mutations with `Func<Hex1bTheme, Hex1bTheme>` API
- Nested ThemePanels accumulate mutations
- No visual presence—only affects theming
- Supports caching and memoization

## See Also

- [BorderWidget](/guide/widgets/border) - Detailed border documentation
- [PanelWidget](/guide/widgets/panel) - Detailed panel documentation
- [ThemePanelWidget](/guide/widgets/themepanel) - Detailed theme panel documentation
- [HStackWidget](/guide/widgets/hstack) - Horizontal layout container
- [VStackWidget](/guide/widgets/vstack) - Vertical layout container
