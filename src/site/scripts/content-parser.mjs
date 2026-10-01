import { readFile, realpath } from "node:fs/promises";
import path from "node:path";
import MarkdownIt from "markdown-it";
import container from "markdown-it-container";
import matter from "gray-matter";
import ts from "typescript";

export const componentNames = new Set([
  "CodeBlock", "StaticCodeBlock", "StaticTerminalPreview", "TerminalCommand",
  "TerminalDemo", "FlowDiagram", "InstallGuide", "ContentArchitecture",
]);

export function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, character => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  })[character]);
}

function fail(filePath, message) {
  throw new Error(`${filePath}: ${message}`);
}

function staticValue(node, bindings, filePath) {
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) return node.text;
  if (ts.isNumericLiteral(node)) return Number(node.text);
  if (node.kind === ts.SyntaxKind.TrueKeyword) return true;
  if (node.kind === ts.SyntaxKind.FalseKeyword) return false;
  if (node.kind === ts.SyntaxKind.NullKeyword) return null;
  if (ts.isParenthesizedExpression(node) || ts.isAsExpression(node) || ts.isSatisfiesExpression(node)) {
    return staticValue(node.expression, bindings, filePath);
  }
  if (ts.isIdentifier(node) && Object.hasOwn(bindings, node.text)) return bindings[node.text];
  if (ts.isArrayLiteralExpression(node)) return node.elements.map(item => staticValue(item, bindings, filePath));
  if (ts.isObjectLiteralExpression(node)) {
    const result = Object.create(null);
    for (const property of node.properties) {
      if (!ts.isPropertyAssignment(property) ||
          !(ts.isIdentifier(property.name) || ts.isStringLiteral(property.name))) {
        fail(filePath, "Only literal object properties are supported in documentation bindings");
      }
      result[property.name.text] = staticValue(property.initializer, bindings, filePath);
    }
    return result;
  }
  fail(filePath, `Unsupported dynamic documentation expression: ${node.getText().slice(0, 120)}`);
}

function inside(directory, filename) {
  const relative = path.relative(directory, filename);
  return relative !== ".." && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
}

async function rawImport(specifier, filePath, contentDirectory) {
  if (!specifier.startsWith("./") && !specifier.startsWith("../")) {
    fail(filePath, `Raw imports must be relative files: ${specifier}`);
  }
  const filename = specifier.slice(0, -4);
  if (/[?#\\\0]/.test(filename)) fail(filePath, `Invalid raw import: ${specifier}`);
  const root = path.resolve(contentDirectory);
  const target = path.resolve(path.dirname(filePath), filename);
  if (!inside(root, target)) fail(filePath, `Raw import escapes content directory: ${specifier}`);
  const [realRoot, realTarget] = await Promise.all([realpath(root), realpath(target)]);
  if (!inside(realRoot, realTarget)) fail(filePath, `Raw import symlink escapes content directory: ${specifier}`);
  return readFile(realTarget, "utf8");
}

async function parseScript(script, options, bindings) {
  const { filePath, contentDirectory } = options;
  const source = ts.createSourceFile(filePath, script, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
  if (source.parseDiagnostics.length) {
    fail(filePath, ts.flattenDiagnosticMessageText(source.parseDiagnostics[0].messageText, "\n"));
  }
  const architecture = path.relative(contentDirectory, filePath).split(path.sep).join("/") === "guide/index.md";
  for (const statement of source.statements) {
    if (ts.isImportDeclaration(statement)) {
      const specifier = statement.moduleSpecifier.text;
      const clause = statement.importClause;
      if (specifier.endsWith("?raw") && clause?.name && !clause.namedBindings) {
        bindings[clause.name.text] = await rawImport(specifier, filePath, contentDirectory);
      } else if (clause?.name && componentNames.has(clause.name.text) &&
                 specifier.endsWith(`/.vitepress/theme/components/${clause.name.text}.vue`)) {
        // Components are replaced by static renderers; their Vue modules are never loaded.
      } else if (architecture && specifier === "vue" &&
                 clause?.namedBindings && ts.isNamedImports(clause.namedBindings) &&
                 clause.namedBindings.elements.every(item => item.name.text === "onMounted" && !item.propertyName)) {
        // The authored architecture diagram's hover hook is replaced with native SVG titles and links.
      } else {
        fail(filePath, `Unsupported documentation import: ${specifier}`);
      }
    } else if (ts.isVariableStatement(statement) &&
               (statement.declarationList.flags & ts.NodeFlags.Const)) {
      for (const declaration of statement.declarationList.declarations) {
        if (!ts.isIdentifier(declaration.name) || !declaration.initializer) {
          fail(filePath, "Documentation bindings must be initialized named constants");
        }
        if (Object.hasOwn(bindings, declaration.name.text)) fail(filePath, `Duplicate binding: ${declaration.name.text}`);
        bindings[declaration.name.text] = staticValue(declaration.initializer, bindings, filePath);
      }
    } else if (architecture && ts.isExpressionStatement(statement) &&
               ts.isCallExpression(statement.expression) &&
               ts.isIdentifier(statement.expression.expression) &&
               statement.expression.expression.text === "onMounted" && bindings.componentInfo) {
      // Intentionally discard this known runtime-only hook, not arbitrary script execution.
    } else {
      fail(filePath, `Unsupported documentation statement: ${statement.getText(source).slice(0, 100)}`);
    }
  }
  return architecture && bindings.componentInfo;
}

function readTag(source, start) {
  const opening = /^<([A-Za-z][\w-]*)\b/.exec(source.slice(start));
  if (!opening) return null;
  let quote = null;
  let end = start + opening[0].length;
  for (; end < source.length; end++) {
    const character = source[end];
    if (quote) {
      if (character === quote) quote = null;
    } else if (character === "'" || character === '"') quote = character;
    else if (character === ">") break;
  }
  if (end === source.length) throw new Error(`Unclosed <${opening[1]}> tag`);
  const attributes = source.slice(start + opening[0].length, end);
  return { name: opening[1], attributes: attributes.replace(/\/\s*$/, ""), end: end + 1, selfClosing: /\/\s*$/.test(attributes) };
}

function readComponent(source, start, filePath) {
  const opening = readTag(source, start);
  if (!opening || !/^[A-Z]/.test(opening.name)) return null;
  if (!componentNames.has(opening.name)) fail(filePath, `Unknown documentation component <${opening.name}>`);
  let end = opening.end;
  let slot = "";
  if (!opening.selfClosing) {
    const close = `</${opening.name}>`;
    // A closing-tag example inside a fenced slot is code, not the component boundary.
    const protectedSource = source.replace(/^([ \t]*)(`{3,}|~{3,})[^\n]*\n[\s\S]*?^\1\2[ \t]*$/gm,
      fence => fence.replace(/[^\n]/g, " "));
    const closeIndex = protectedSource.indexOf(close, end);
    if (closeIndex < 0) fail(filePath, `Missing ${close}`);
    slot = source.slice(end, closeIndex);
    end = closeIndex + close.length;
  }
  const attributes = Object.create(null);
  let remaining = opening.attributes.trim();
  while (remaining) {
    const match = /^([:@#\w-]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'=<>`]+)))?/.exec(remaining);
    if (!match) fail(filePath, `Invalid attribute on <${opening.name}>: ${remaining}`);
    const key = match[1].replace(/^v-bind:/, ":");
    if (Object.hasOwn(attributes, key)) fail(filePath, `Duplicate attribute ${key}`);
    attributes[key] = match[2] ?? match[3] ?? match[4] ?? true;
    remaining = remaining.slice(match[0].length).trim();
  }
  return { name: opening.name, attributes, slot, start, end };
}

export function createMarkdown() {
  const md = new MarkdownIt({ html: true, linkify: true, typographer: false });
  for (const name of ["tip", "warning", "danger", "info", "details"]) {
    md.use(container, name, {
      render(tokens, index) {
        if (tokens[index].nesting === -1) return name === "details" ? "</details>\n" : "</aside>\n";
        const title = tokens[index].info.trim().slice(name.length).trim() ||
          (name === "details" ? "Details" : name.toUpperCase());
        return name === "details"
          ? `<details class="custom-block details"><summary>${md.renderInline(title)}</summary>\n`
          : `<aside class="custom-block ${name}"><p class="custom-block-title">${md.renderInline(title)}</p>\n`;
      },
    });
  }
  md.block.ruler.before("html_block", "site_component", (state, startLine, endLine, silent) => {
    const start = state.bMarks[startLine] + state.tShift[startLine];
    if (!/^<[A-Z]/.test(state.src.slice(start))) return false;
    const component = readComponent(state.src, start, state.env.filePath);
    if (!component) return false;
    if (silent) return true;
    const token = state.push("site_component", "", 0);
    token.meta = component;
    token.block = true;
    let nextLine = startLine;
    while (nextLine < endLine && state.bMarks[nextLine] < component.end) nextLine++;
    if (state.src.slice(component.end, state.eMarks[nextLine - 1]).trim()) {
      fail(state.env.filePath, "Block components must end on their own line");
    }
    token.map = [startLine, nextLine];
    state.line = nextLine;
    return true;
  }, { alt: ["paragraph", "reference", "blockquote", "list"] });
  md.inline.ruler.before("html_inline", "site_component", (state, silent) => {
    if (!/^<[A-Z]/.test(state.src.slice(state.pos))) return false;
    const component = readComponent(state.src, state.pos, state.env.filePath);
    if (!component) return false;
    if (!silent) state.push("site_component", "", 0).meta = component;
    state.pos = component.end;
    return true;
  });
  return md;
}

function resolveComponent(component, bindings, filePath, md) {
  const props = Object.create(null);
  let codeKey;
  for (const [attribute, value] of Object.entries(component.attributes)) {
    if (attribute.startsWith(":")) {
      const key = attribute.slice(1);
      const expression = ts.createSourceFile("binding.ts", `(${value})`, ts.ScriptTarget.Latest, true);
      if (expression.parseDiagnostics.length || expression.statements.length !== 1 ||
          !ts.isExpressionStatement(expression.statements[0])) fail(filePath, `Invalid binding: ${value}`);
      props[key] = staticValue(expression.statements[0].expression, bindings, filePath);
      if (key === "code" && /^[A-Za-z_$][\w$]*$/.test(value)) codeKey = value;
    } else if (attribute.startsWith("@") || attribute.startsWith("v-")) {
      fail(filePath, `Runtime directive is not supported: ${attribute}`);
    } else {
      props[attribute] = typeof value === "string" ? md.utils.unescapeAll(value) : value;
    }
  }
  if (component.slot.trim()) {
    const slot = component.slot.replace(/^\s*<template\s+(?:#default|#code|v-slot:default|v-slot:code)\s*>/i, "")
      .replace(/<\/template>\s*$/i, "");
    const slotTokens = md.parse(slot, { filePath });
    const fences = slotTokens.filter(token => token.type === "fence" || token.type === "code_block");
    if (fences.length) {
      if (fences.length !== 1 || slotTokens.some(token => token.type !== "fence" && token.type !== "code_block")) {
        fail(filePath, `Expected a single code fence in <${component.name}> slot`);
      }
      props.code ??= fences[0].content;
      props.lang ??= fences[0].info.trim().split(/\s/)[0];
    } else {
      if (/<\/?template\b/i.test(slot)) fail(filePath, `Unsupported slot in <${component.name}>`);
      props.code ??= slot.trim();
    }
  }
  return { ...component, props, codeKey, example: props.example };
}

export async function parseContent(source, { filePath, contentDirectory }) {
  if (!filePath || !contentDirectory) throw new Error("parseContent requires filePath and contentDirectory");
  if (/^\uFEFF?---[^\r\n]/.test(source)) fail(filePath, "Only YAML frontmatter delimited by --- is supported");
  const frontmatter = matter(source, { language: "yaml" });
  const md = createMarkdown();
  let markdown = frontmatter.content;
  const bindings = Object.create(null);
  const preliminary = new MarkdownIt({ html: true }).parse(markdown, {});
  const lines = markdown.split("\n");
  const scripts = preliminary.filter(token => token.type === "html_block" && /^<script\b/i.test(token.content.trim()));
  for (const token of scripts) {
    const match = /^\s*<script\s+setup(?:\s+lang=["'](?:ts|js)["'])?\s*>([\s\S]*?)<\/script>\s*$/i.exec(token.content);
    if (!match) fail(filePath, "Only static <script setup> documentation blocks are supported");
    const architecture = await parseScript(match[1], { filePath, contentDirectory }, bindings);
    for (let index = token.map[0]; index < token.map[1]; index++) lines[index] = "";
    if (architecture) lines[token.map[0]] = "<ContentArchitecture />";
  }
  markdown = lines.join("\n");
  const components = [];
  const fences = [];
  let csharpFenceCount = 0;
  const tokens = md.parse(markdown, { filePath });
  function visit(list) {
    for (const token of list) {
      if (token.type === "fence") {
        const language = token.info.trim().split(/\s/)[0];
        const fence = {
          codeKey: /^(?:csharp|cs|c#)$/i.test(language)
            ? `fence-${++csharpFenceCount}` : `markdown-fence-${fences.length}`,
          code: token.content, language, info: token.info, map: token.map,
        };
        token.meta = { ...token.meta, codeKey: fence.codeKey };
        fences.push(fence);
      }
      if (token.type === "site_component") {
        token.meta = resolveComponent(token.meta, bindings, filePath, md);
        components.push(token.meta);
      }
      if (token.type === "html_block" || token.type === "html_inline") {
        const protectedHtml = token.content.replace(/<!--[\s\S]*?-->|<(pre|code|style)\b[^>]*>[\s\S]*?<\/\1>/gi,
          block => block.replace(/[^\n]/g, " "));
        const replacements = [];
        const matcher = /<[A-Z][\w-]*\b/g;
        let match;
        while ((match = matcher.exec(protectedHtml))) {
          const component = resolveComponent(readComponent(token.content, match.index, filePath), bindings, filePath, md);
          replacements.push({ start: component.start, end: component.end, replacement: `\0COMPONENT${components.length}\0` });
          components.push(component);
          matcher.lastIndex = component.end;
        }
        for (const replacement of replacements.reverse()) {
          token.content = token.content.slice(0, replacement.start) + replacement.replacement + token.content.slice(replacement.end);
        }
      }
      if (token.children) visit(token.children);
    }
  }
  visit(tokens);
  return {
    markdown, frontmatter: frontmatter.data, frontmatterSource: frontmatter.matter,
    bindings, components, fences, tokens,
  };
}

export async function readContent(filePath, { contentDirectory }) {
  return parseContent(await readFile(filePath, "utf8"), { filePath, contentDirectory });
}
