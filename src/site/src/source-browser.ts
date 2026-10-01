import { createFileTree, sourceFiles } from "./sample-files";
import type { SampleFile, SourceEntry, SourceFile } from "./sample-files";
import type { SourceEditor } from "./source-editor";

export interface SourceBrowserElements {
  root: HTMLElement;
  tree: HTMLElement;
  editor: HTMLElement;
  fallback: HTMLElement;
  path: HTMLElement;
  language: HTMLElement;
  status: HTMLElement;
}

export function mountSourceBrowser(elements: SourceBrowserElements, files: readonly SampleFile[]): { dispose(): void } {
  const root = createFileTree(files);
  const entries = sourceFiles(root);
  let active = entries.find(entry => entry.path === "Program.cs") ?? entries[0];
  let editor: SourceEditor | undefined;
  let loading = false;
  let disposed = false;
  const listeners = new AbortController();
  const items = new Map<string, HTMLLIElement>();

  function reportError(error: unknown): void {
    if (disposed) return;
    elements.status.textContent = `Source editor unavailable: ${error instanceof Error ? error.message : String(error)}. Plain text is shown below.`;
    elements.status.dataset.level = "error";
    elements.fallback.hidden = false;
    elements.editor.hidden = true;
    console.error(error);
  }

  function select(file: SourceFile): void {
    active = file;
    elements.path.textContent = file.path;
    elements.language.textContent = `${file.file.language === "csharp" ? "C#" : file.file.language.toUpperCase()} · Read only`;
    elements.fallback.textContent = file.file.content;
    for (const [path, item] of items) {
      if (item.hasAttribute("aria-selected")) item.setAttribute("aria-selected", String(path === file.path));
    }
    try { editor?.open(file.file); } catch (error) { reportError(error); }
  }

  function focus(item: HTMLLIElement): void {
    for (const entry of items.values()) entry.tabIndex = entry === item ? 0 : -1;
    item.focus();
  }

  function toggle(item: HTMLLIElement, expanded: boolean): void {
    item.setAttribute("aria-expanded", String(expanded));
    const group = item.querySelector<HTMLUListElement>(":scope > ul");
    if (group) group.hidden = !expanded;
  }

  function render(entry: SourceEntry, depth: number): HTMLLIElement {
    const item = document.createElement("li");
    item.setAttribute("role", "treeitem");
    item.setAttribute("aria-label", entry.name);
    item.tabIndex = entry.path === active.path ? 0 : -1;
    item.dataset.path = entry.path;
    item.dataset.kind = entry.kind;
    items.set(entry.path, item);
    const row = document.createElement("div");
    row.className = "file-row";
    row.style.paddingInlineStart = `${12 + depth * 16}px`;
    const icon = document.createElement("span");
    icon.className = entry.kind === "directory" ? "folder-icon" : "file-icon";
    icon.setAttribute("aria-hidden", "true");
    icon.textContent = entry.kind === "directory" ? "" : entry.name.endsWith(".cs") ? "C#" : "<>";
    const label = document.createElement("span");
    label.className = "file-name";
    label.textContent = entry.name;
    row.title = entry.path || entry.name;
    row.append(icon, label);
    item.append(row);
    if (entry.kind === "directory") {
      item.setAttribute("aria-expanded", "true");
      const group = document.createElement("ul");
      group.setAttribute("role", "group");
      group.append(...entry.children.map(child => render(child, depth + 1)));
      item.append(group);
    } else item.setAttribute("aria-selected", "false");
    row.addEventListener("click", () => {
      focus(item);
      if (entry.kind === "directory") toggle(item, item.getAttribute("aria-expanded") !== "true");
      else select(entry);
    }, { signal: listeners.signal });
    return item;
  }

  const list = document.createElement("ul");
  list.setAttribute("role", "none");
  list.append(render(root, 0));
  elements.tree.replaceChildren(list);
  elements.tree.addEventListener("keydown", event => {
    const current = event.target;
    if (!(current instanceof HTMLLIElement) || current.getAttribute("role") !== "treeitem") return;
    const visible = [...items.values()].filter(item => !item.closest("[hidden]"));
    const index = visible.indexOf(current);
    let target: HTMLLIElement | undefined;
    switch (event.key) {
      case "ArrowDown": target = visible[Math.min(index + 1, visible.length - 1)]; break;
      case "ArrowUp": target = visible[Math.max(index - 1, 0)]; break;
      case "Home": target = visible[0]; break;
      case "End": target = visible.at(-1); break;
      case "ArrowRight":
        if (current.hasAttribute("aria-expanded")) {
          if (current.getAttribute("aria-expanded") === "false") toggle(current, true);
          else target = visible[index + 1];
        }
        break;
      case "ArrowLeft":
        if (current.getAttribute("aria-expanded") === "true") toggle(current, false);
        else {
          const parent = current.parentElement?.closest('[role="treeitem"]');
          if (parent instanceof HTMLLIElement) target = parent;
        }
        break;
      case "Enter":
      case " ":
        current.querySelector<HTMLElement>(":scope > .file-row")?.click();
        break;
      default: return;
    }
    event.preventDefault();
    if (target) focus(target);
  }, { signal: listeners.signal });
  select(active);

  async function loadEditor(): Promise<void> {
    if (loading || disposed) return;
    loading = true;
    observer.disconnect();
    elements.status.textContent = "Loading source editor...";
    try {
      const { createSourceEditor } = await import("./source-editor");
      if (disposed) return;
      elements.editor.hidden = false;
      editor = createSourceEditor(elements.editor);
      editor.open(active.file);
      elements.fallback.hidden = true;
      elements.status.textContent = "Read-only Monaco editor. C# and XML highlighting; no language server.";
      elements.status.dataset.level = "ready";
      elements.root.dataset.editor = "ready";
    } catch (error) { reportError(error); }
  }

  const observer = new IntersectionObserver(entries => {
    if (entries.some(entry => entry.isIntersecting)) void loadEditor();
  }, { rootMargin: "120px" });
  observer.observe(elements.root);
  elements.root.addEventListener("focusin", () => { void loadEditor(); }, { signal: listeners.signal });
  return {
    dispose() {
      disposed = true;
      observer.disconnect();
      listeners.abort();
      editor?.dispose();
    },
  };
}
