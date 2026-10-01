export const navigation = [
  { text: "Guide", link: "/guide/" },
  { text: "API Reference", link: "/reference/" },
];

const item = (text, link) => ({ text, link });
const group = (text, items) => ({ text, items });

export const guideSidebar = [
  group("Overview", [item("Guide", "/guide/")]),
  group("Features", [
    item("Terminal UIs", "/guide/tui/"),
    item("Terminal Emulator", "/guide/terminal-emulator/"),
    item("Automation & Testing", "/guide/testing/"),
  ]),
  group("Building TUIs", [
    item("Your First App", "/guide/getting-started/"),
    item("Your First Flow App", "/guide/building-clis/"),
    item("Widgets & Nodes", "/guide/widgets-and-nodes/"),
    item("API Design Guidelines", "/guide/api-design/"),
    item("Composing Widgets", "/guide/composition/"),
    item("Layout System", "/guide/layout/"),
    item("Input Handling", "/guide/input/"),
    item("Theming", "/guide/theming/"),
  ]),
  group("Terminal Stack", [
    item("Using the Emulator", "/guide/using-the-emulator/"),
    item("Presentation Adapters", "/guide/presentation-adapters/"),
    item("Workload Adapters", "/guide/workload-adapters/"),
  ]),
  group("Observability", [item("Performance & Metrics", "/guide/performance/")]),
  group("Reference", [item("Widgets", "/guide/widgets/"), item("API Docs", "/reference/")]),
  group("Tools", [item("CLI Tool", "/guide/cli/"), item("MCP Server", "/guide/mcp-server/")]),
];

export const referenceSidebar = [
  group("API Reference", [item("Overview", "/reference/")]),
  group("CLI Reference", [item("hex1b", "/reference/cli/")]),
  group("Namespaces", [
    "Hex1b", "Hex1b.Animation", "Hex1b.Automation", "Hex1b.Events", "Hex1b.Input",
    "Hex1b.Layout", "Hex1b.Nodes", "Hex1b.Surfaces", "Hex1b.Theming", "Hex1b.Tokens", "Hex1b.Widgets",
  ].map(namespace => item(namespace, `/reference/${namespace}/`))),
];

export const sidebars = { "/guide/": guideSidebar, "/reference/": referenceSidebar };

const widgetDirectory = new URL("../content/guide/widgets/", import.meta.url);
export const widgetLinks = (await Promise.all((await readdir(widgetDirectory))
  .filter(filename => filename.endsWith(".md") && filename !== "index.md")
  .map(async filename => {
    const content = await readFile(new URL(filename, widgetDirectory), "utf8");
    const title = /^#\s+(.+)$/m.exec(content.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, ""))?.[1];
    if (!title) throw new Error(`Widget page is missing its title: ${filename}`);
    return item(title, `/guide/widgets/${filename.slice(0, -3)}/`);
  }))).sort((left, right) => left.text.localeCompare(right.text, "en"));

export function sidebarFor(route, pages) {
  const normalized = route.replace(/^\//, "");
  if (normalized.startsWith("reference/")) return referenceSidebar;
  if (!normalized.startsWith("guide/")) return [];
  return pages ? createNavigation(pages).sidebar["/guide/"] : [...guideSidebar, group("Widgets", widgetLinks)];
}

export function createNavigation(pages) {
  const widgets = pages.filter(page => /^guide\/widgets\/[^/]+\/$/.test(page.route))
    .map(page => item(page.title, `/${page.route}`))
    .sort((left, right) => left.text.localeCompare(right.text, "en"));
  return {
    nav: navigation,
    sidebar: {
      "/guide/": [...guideSidebar, group("Widgets", widgets)],
      "/reference/": referenceSidebar,
    },
  };
}
import { readFile, readdir } from "node:fs/promises";
