import * as monaco from "monaco-editor/editor/editor.api";
import "monaco-editor/languages/definitions/csharp/register";
import "monaco-editor/languages/definitions/xml/register";
import "monaco-editor/editor/contrib/clipboard/browser/clipboard";
import "monaco-editor/editor/contrib/contextmenu/browser/contextmenu";
import "monaco-editor/editor/contrib/folding/browser/folding";
import "monaco-editor/features/find/register";
import "monaco-editor/features/codicon/register";
import EditorWorker from "monaco-editor/editor/editor.worker?worker";
import type { SampleFile } from "./sample-files";

globalThis.MonacoEnvironment = { getWorker: () => new EditorWorker() };
let nextProject = 0;

export interface SourceEditor {
  open(file: SampleFile): void;
  dispose(): void;
}

function applyTheme(): void {
  const css = getComputedStyle(document.documentElement);
  const color = (name: string) => css.getPropertyValue(`--cp-${name}`).trim();
  const token = (name: string) => color(name).replace(/^#/u, "");
  monaco.editor.defineTheme("hex1b-site", {
    base: document.documentElement.dataset.theme === "dark" ? "vs-dark" : "vs",
    inherit: false,
    rules: [
      { token: "", foreground: token("text") },
      { token: "comment", foreground: token("text-muted"), fontStyle: "italic" },
      { token: "keyword", foreground: token("accent") },
      { token: "string", foreground: token("success") },
      { token: "number", foreground: token("link") },
      { token: "type", foreground: token("accent") },
      { token: "tag", foreground: token("accent") },
      { token: "attribute.name", foreground: token("link") },
      { token: "attribute.value", foreground: token("success") },
      { token: "delimiter", foreground: token("text-muted") },
    ],
    colors: {
      "editor.background": color("surface"),
      "editor.foreground": color("text"),
      "editorLineNumber.foreground": color("text-soft"),
      "editorLineNumber.activeForeground": color("text"),
      "editor.selectionBackground": color("border"),
      "editor.inactiveSelectionBackground": color("surface-soft"),
      "editor.lineHighlightBackground": color("surface-soft"),
      "editorCursor.foreground": color("accent"),
      "editorWidget.background": color("bg-elevated"),
      "editorWidget.border": color("border"),
      "input.background": color("surface"),
      "input.foreground": color("text"),
      "input.border": color("border-strong"),
      "focusBorder": color("accent"),
      "scrollbarSlider.background": color("border"),
      "scrollbarSlider.hoverBackground": color("border-strong"),
    },
  });
  monaco.editor.setTheme("hex1b-site");
}

export function createSourceEditor(container: HTMLElement): SourceEditor {
  applyTheme();
  const project = `sample-${++nextProject}`;
  const editor = monaco.editor.create(container, {
    model: null,
    theme: "hex1b-site",
    readOnly: true,
    domReadOnly: true,
    automaticLayout: true,
    fontFamily: 'Consolas, "Courier New", Courier, monospace',
    fontSize: 13,
    lineHeight: 21,
    lineNumbers: "on",
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    padding: { top: 16, bottom: 16 },
    renderLineHighlight: "none",
    stickyScroll: { enabled: false },
    quickSuggestions: false,
    suggestOnTriggerCharacters: false,
    wordBasedSuggestions: "off",
    bracketPairColorization: { enabled: false },
    ariaLabel: "Read-only sample source code",
  });
  const models = new Map<string, { model: monaco.editor.ITextModel; view: monaco.editor.ICodeEditorViewState | null }>();
  let active: string | undefined;
  const observer = new MutationObserver(applyTheme);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
  return {
    open(file) {
      if (file.path === active) return;
      const previous = active === undefined ? undefined : models.get(active);
      if (previous) previous.view = editor.saveViewState();
      let entry = models.get(file.path);
      if (!entry) {
        const language = ["csharp", "xml"].includes(file.language) ? file.language : "plaintext";
        const uri = monaco.Uri.from({ scheme: "inmemory", authority: project, path: `/${file.path}` });
        entry = { model: monaco.editor.createModel(file.content, language, uri), view: null };
        entry.model.updateOptions({
          bracketColorizationOptions: { enabled: false, independentColorPoolPerBracketType: false },
        });
        models.set(file.path, entry);
      }
      active = file.path;
      editor.setModel(entry.model);
      if (entry.view) editor.restoreViewState(entry.view);
      editor.updateOptions({ ariaLabel: `${file.path}, read-only sample source code` });
    },
    dispose() {
      observer.disconnect();
      editor.dispose();
      for (const entry of models.values()) entry.model.dispose();
      models.clear();
    },
  };
}
