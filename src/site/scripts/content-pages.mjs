import { readdir, stat } from "node:fs/promises";
import path from "node:path";
import { createMarkdown, escapeHtml, readContent, componentNames } from "./content-parser.mjs";

export const SITE_BASE = "__SITE_BASE__";

// These links are present in the verbatim architecture diagram's legacy data.
// Keep migration corrections here rather than silently editing authored Markdown.
export const legacyLinkAliases = {
  "/guide/terminal-emulator#console-adapter": "/guide/presentation-adapters#consolepresentationadapter",
  "/guide/terminal-emulator#web-adapter": "/guide/terminal-emulator#presentation-adapters",
  "/guide/testing#input-sequencer": "/guide/testing#input-sequence-builder-api",
  "/guide/testing#pattern-matching": "/guide/testing#terminal-inspection-apis",
  "/guide/terminal-emulator#workload-adapters": "/guide/workload-adapters/",
  "/guide/terminal-emulator#child-processes": "/guide/terminal-emulator#child-process-integration",
};

export class ContentCompilationError extends Error {
  constructor(diagnostics) {
    super(`Content compilation failed:\n${diagnostics.map(item => `- ${item.source}: ${item.message}`).join("\n")}`);
    this.name = "ContentCompilationError";
    this.diagnostics = diagnostics;
    this.unresolvedLinks = diagnostics.filter(item => item.kind === "link");
  }
}

export function routeFromPath(filename) {
  const withoutExtension = filename.replaceAll("\\", "/").replace(/\.md$/i, "");
  const route = withoutExtension.replace(/(^|\/)index$/, "$1").replace(/^\/+|\/+$/g, "");
  return route && route !== "." ? `${route}/` : "";
}

function splitSuffix(value) {
  const index = value.search(/[?#]/);
  return index === -1 ? [value, ""] : [value.slice(0, index), value.slice(index)];
}

export function rebaseContentUrl(value, source, { routes, links = [], asset = false } = {}) {
  if (value.startsWith(SITE_BASE)) return value;
  if (/^(?:https?:|mailto:|tel:|ftp:|\/\/)/i.test(value)) return value;
  if (/^[a-z][a-z\d+.-]*:/i.test(value)) throw new Error(`${source}: Unsupported URL scheme: ${value}`);
  if (!value || value.startsWith("#")) {
    if (value && !asset) links.push({ source, original: value, route: routeFromPath(source), suffix: value, asset: false });
    return value;
  }
  const [pathname, suffix] = splitSuffix(value);
  if (!pathname) {
    const target = routeFromPath(source);
    links.push({ source, original: value, route: target, suffix, asset });
    return `${SITE_BASE}${target}${suffix}`;
  }
  if (pathname.includes("\\") || /%2f|%5c/i.test(pathname)) throw new Error(`${source}: Invalid URL path: ${value}`);
  let decoded;
  try { decoded = decodeURIComponent(pathname); }
  catch { throw new Error(`${source}: Invalid URL encoding: ${value}`); }
  const resolved = path.posix.normalize(pathname.startsWith("/")
    ? decoded.slice(1) : path.posix.join(path.posix.dirname(source), decoded));
  if (resolved === ".." || resolved.startsWith("../")) throw new Error(`${source}: URL escapes site root: ${value}`);
  let target = routeFromPath(resolved);
  const document = !asset && (/\.md$/i.test(resolved) || routes?.has(target) || !/\.[^/]+$/.test(resolved));
  if (!document) target = resolved === "." ? "" : resolved;
  links.push({ source, original: value, route: target, suffix, asset: !document });
  return `${SITE_BASE}${target}${suffix}`;
}

async function markdownFiles(directory, prefix = "") {
  const result = [];
  for (const entry of (await readdir(directory, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name, "en"))) {
    if (entry.name.startsWith(".")) continue;
    const relative = path.posix.join(prefix, entry.name);
    if (entry.isDirectory()) result.push(...await markdownFiles(path.join(directory, entry.name), relative));
    else if (entry.isFile() && entry.name.endsWith(".md")) result.push(relative);
  }
  return result;
}

function codeHtml(code, language = "csharp") {
  if (typeof code !== "string") throw new Error("Code components require a string code binding or a code slot");
  return `<pre><code class="language-${escapeHtml(language)}">${escapeHtml(code)}</code></pre>`;
}

function flowDiagram() {
  const steps = [
    ["Enter your project name:", "my-app", "✓ Project: my-app"],
    ["Add Docker support?", "Yes / No", "✓ Docker: Yes"],
    ["Select a template:", "ASP.NET Core Web API", "✓ Template: ASP.NET Core Web API"],
    ["Creating project structure...", "Installing packages...", "✓ Project created!"],
  ];
  return `<figure class="flow-diagram"><figcaption>Terminal</figcaption><pre><code>$ dotnet run</code></pre>
<ol>${steps.map((step, index) => `<li><strong>Step ${index + 1}</strong><p>${step.map(escapeHtml).join("<br>")}</p>${index === 2
    ? "<details><summary>Select a template:</summary><ul><li>ASP.NET Core Web API</li><li>Blazor Server</li><li>Console Application</li><li>Worker Service</li><li>gRPC Service</li><li>Minimal API</li><li>Class Library</li><li>xUnit Test Project</li></ul></details>" : ""}</li>`).join("")}</ol>
<figcaption>Done — back to prompt</figcaption></figure>`;
}

const allowedProps = {
  CodeBlock: ["code", "lang", "language", "command", "example", "exampleTitle", "title"],
  StaticCodeBlock: ["code", "lang", "language", "command", "example", "exampleTitle", "title"],
  StaticTerminalPreview: ["code", "lang", "language", "svgPath", "title"],
  TerminalCommand: ["command"],
  TerminalDemo: ["example", "title", "width", "height", "cols", "rows"],
  FlowDiagram: [],
  InstallGuide: [],
  ContentArchitecture: [],
};

function componentHtml(component, context) {
  const { source, samples, parsed, packageVersion, url, linkedSamples } = context;
  const { name, props, codeKey } = component;
  for (const key of Object.keys(props)) {
    if (!allowedProps[name].includes(key)) throw new Error(`${source}: Unsupported ${name} property: ${key}`);
  }
  const sample = (codeKey ? samples.find(item => item.page === source && item.codeKey === codeKey) : undefined)
    ?? (props.example ? samples.find(item => item.page === source && item.example === props.example)
      ?? samples.find(item => item.example === props.example) : undefined);
  if (props.example && !sample) throw new Error(`${source}: Missing standalone sample for example "${props.example}"`);
  if (sample) linkedSamples.add(sample.id);
  const openSample = sample ? `<a class="sample-link" href="${SITE_BASE}samples/${escapeHtml(sample.id)}/">Open sample</a>` : "";
  const code = sample?.files?.find(file => file.path === "Program.cs")?.content ?? props.code;
  switch (name) {
    case "CodeBlock":
    case "StaticCodeBlock":
      return `<div class="code-example">${props.title ? `<p class="code-title">${escapeHtml(props.title)}</p>` : ""}${codeHtml(code, props.lang ?? props.language)}${props.command
        ? `<div class="code-command"><code>${escapeHtml(props.command)}</code></div>` : ""}${openSample}</div>\n`;
    case "StaticTerminalPreview":
      return props.svgPath
        ? `<figure class="static-terminal-preview"><img src="${escapeHtml(url(props.svgPath, true))}" alt="${escapeHtml(props.title ?? "Terminal output")}" loading="lazy"><details><summary>View code</summary>${codeHtml(code, props.language ?? props.lang)}</details>${openSample}</figure>\n`
        : `<div class="code-example">${codeHtml(code, props.language ?? props.lang)}${openSample}</div>\n`;
    case "TerminalCommand":
      if (typeof props.command !== "string") throw new Error(`${source}: TerminalCommand requires command`);
      return `<pre class="terminal-command"><code>${escapeHtml(props.command.replaceAll("{{version}}", packageVersion))}</code></pre>\n`;
    case "TerminalDemo":
      if (!sample) throw new Error(`${source}: TerminalDemo requires a registered sample`);
      return `<aside class="sample-card"><h3>${escapeHtml(props.title ?? sample.title)}</h3>${openSample}</aside>\n`;
    case "InstallGuide":
      return `<section class="install-guide"><p class="install-cta">Add the Hex1b package to your app:</p>
<div class="install-options"><section><h2>.NET CLI</h2>${codeHtml(`dotnet add package Hex1b@${packageVersion}`, "bash")}</section>
<details><summary>PackageReference</summary>${codeHtml(`<PackageReference Include="Hex1b" Version="${packageVersion}" />`, "xml")}</details>
<details><summary>File-based apps</summary>${codeHtml(`#:package Hex1b@${packageVersion}`)}</details></div></section>\n`;
    case "FlowDiagram":
      return flowDiagram();
    case "ContentArchitecture":
      return `<div class="architecture-details">${Object.entries(parsed.bindings.componentInfo).map(([id, info]) =>
        `<details id="architecture-${escapeHtml(id)}"><summary>${escapeHtml(info.title)}</summary><p>${escapeHtml(info.description)}</p><a href="${escapeHtml(url(info.link))}">Learn more →</a></details>`).join("")}</div>\n`;
    default:
      throw new Error(`${source}: Unknown component ${name}`);
  }
}

function plainText(tokens) {
  return tokens.map(token => {
    if (token.type === "html_inline") return "";
    if (token.children) return plainText(token.children);
    return token.content ?? "";
  }).join("");
}

function addHeadings(tokens) {
  const headings = [];
  const used = new Set();
  for (let index = 0; index < tokens.length; index++) {
    if (tokens[index].type !== "heading_open") continue;
    const inline = tokens[index + 1];
    const last = inline.children?.at(-1);
    const explicit = last?.type === "text" && /\s*\{#([^{}\s]+)\}\s*$/.exec(last.content);
    if (explicit) last.content = last.content.slice(0, explicit.index);
    const text = plainText(inline.children ?? []).trim();
    const slug = text.toLowerCase().normalize("NFKD").replace(/\p{M}/gu, "")
      .replace(/[^\p{L}\p{N}\s_-]/gu, "").trim().replace(/\s+/g, "-") || "section";
    let id = explicit ? explicit[1] : slug;
    if (explicit && used.has(id)) throw new Error(`Duplicate explicit heading ID: ${id}`);
    for (let suffix = 1; used.has(id); suffix++) id = `${slug}-${suffix}`;
    used.add(id);
    tokens[index].attrSet("id", id);
    headings.push({ id, text, level: Number(tokens[index].tag.slice(1)) });
  }
  return headings;
}

function rewriteHtml(html, context) {
  const { url, source, parsed } = context;
  if (/<script\b/i.test(html.replace(/<!--[\s\S]*?-->/g, ""))) {
    throw new Error(`${source}: Runtime HTML element <script> is not supported`);
  }
  const protectedBlocks = [];
  const protectedHtml = html.replace(/<!--[\s\S]*?-->|<(pre|code|style)\b[^>]*>[\s\S]*?<\/\1>/gi, block => {
    protectedBlocks.push(block);
    return `\0HTML${protectedBlocks.length - 1}\0`;
  });
  let result = protectedHtml.replace(/<([A-Za-z][\w:-]*)\b(?:[^>"']|"[^"]*"|'[^']*')*>/g, tag => {
    const name = /^<([\w:-]+)/.exec(tag)[1];
    if (/^(?:script|iframe|object|embed)$/i.test(name)) throw new Error(`${source}: Runtime HTML element <${name}> is not supported`);
    if (/^[A-Z]/.test(name) && !componentNames.has(name)) throw new Error(`${source}: Unknown documentation component <${name}>`);
    if (componentNames.has(name)) throw new Error(`${source}: Nested <${name}> must be separated from surrounding HTML with blank lines`);
    if (/\s(?:on[\w-]+|v-[\w:-]+|@[\w:-]+|:[\w-]+)\s*=/i.test(tag)) {
      throw new Error(`${source}: Runtime HTML attributes are not supported: ${tag.slice(0, 120)}`);
    }
    return tag.replace(/\b(href|src|poster|xlink:href)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))/gi,
      (_, attribute, double, single, bare) => `${attribute}="${escapeHtml(url(context.md.utils.unescapeAll(double ?? single ?? bare), !/href$/i.test(attribute)))}"`);
  });
  if (parsed.bindings.componentInfo) {
    result = result.replace(/<g class="arch-tile" data-component="([^"]+)">([\s\S]*?)<\/g>/g, (_, id, contents) => {
      const info = parsed.bindings.componentInfo[id];
      if (!info) throw new Error(`${source}: Missing architecture description for ${id}`);
      return `<a href="#architecture-${escapeHtml(id)}"><g class="arch-tile" data-component="${escapeHtml(id)}"><title>${escapeHtml(`${info.title}: ${info.description}`)}</title>${contents}</g></a>`;
    });
  }
  return result.replace(/\0HTML(\d+)\0/g, (_, index) => protectedBlocks[Number(index)])
    .replace(/\0COMPONENT(\d+)\0/g, (_, index) => componentHtml(parsed.components[Number(index)], context));
}

function searchText(html, md) {
  return md.utils.unescapeAll(html.replace(/<(?:style|script)\b[^>]*>[\s\S]*?<\/(?:style|script)>/gi, "")
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/<\/(?:p|h[1-6]|pre|li|td|th|tr|div|section|summary|details)>|<br\s*\/?>/gi, " ")
    .replace(/<[^>]*>/g, "")).replace(/\s+/g, " ").trim();
}

export async function compileSite({ contentDirectory, referenceDirectory, samples = [], packageVersion }) {
  if (!contentDirectory || typeof packageVersion !== "string" || !packageVersion.trim()) {
    throw new Error("compileSite requires contentDirectory and a non-empty packageVersion");
  }
  const sources = new Map();
  const root = path.resolve(contentDirectory);
  for (const source of await markdownFiles(root)) sources.set(source, { filename: path.join(root, source), root });
  if (referenceDirectory) {
    const referenceRoot = path.resolve(referenceDirectory);
    for (const filename of await markdownFiles(referenceRoot)) {
      const source = `reference/${filename}`;
      if (!sources.has(source)) sources.set(source, { filename: path.join(referenceRoot, filename), root: referenceRoot });
    }
  }
  const routes = new Set();
  for (const source of sources.keys()) {
    const route = routeFromPath(source);
    if (routes.has(route)) throw new Error(`Duplicate content route: ${route}`);
    routes.add(route);
  }
  const sampleIds = new Set();
  for (const sample of samples) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(sample.id) || sampleIds.has(sample.id)) {
      throw new Error(`Invalid or duplicate sample ID: ${sample.id}`);
    }
    sampleIds.add(sample.id);
  }
  const links = [];
  const pages = [];
  const diagnostics = [];
  const obsoleteLinks = [];
  for (const [source, location] of sources) {
    try {
      const parsed = await readContent(location.filename, { contentDirectory: location.root });
      const md = createMarkdown();
      const headings = addHeadings(parsed.tokens);
      const context = {
        source, parsed, samples, packageVersion, md, linkedSamples: new Set(),
        url: (value, asset = false) => {
          const replacement = !asset && legacyLinkAliases[value];
          if (replacement) obsoleteLinks.push({ source, original: value, replacement });
          return rebaseContentUrl(replacement || value, source, { routes, links, asset });
        },
      };
      md.renderer.rules.site_component = (tokens, index) => componentHtml(tokens[index].meta, context);
      const renderFence = md.renderer.rules.fence;
      md.renderer.rules.fence = (tokens, index, ...rest) => {
        const token = tokens[index];
        const sample = samples.find(item => item.page === source && item.codeKey === token.meta?.codeKey);
        if (!sample) return renderFence(tokens, index, ...rest);
        context.linkedSamples.add(sample.id);
        const original = token.content;
        try {
          token.content = sample.files?.find(file => file.path === "Program.cs")?.content ?? original;
          return `<div class="code-example">${renderFence(tokens, index, ...rest)}<a class="sample-link" href="${SITE_BASE}samples/${escapeHtml(sample.id)}/">Open sample</a></div>\n`;
        } finally {
          token.content = original;
        }
      };
      for (const type of ["html_block", "html_inline"]) {
        md.renderer.rules[type] = (tokens, index) => rewriteHtml(tokens[index].content, context);
      }
      const renderLink = md.renderer.rules.link_open ?? ((tokens, index, options, env, renderer) => renderer.renderToken(tokens, index, options));
      md.renderer.rules.link_open = (tokens, index, ...rest) => {
        tokens[index].attrSet("href", context.url(tokens[index].attrGet("href")));
        return renderLink(tokens, index, ...rest);
      };
      const renderImage = md.renderer.rules.image;
      md.renderer.rules.image = (tokens, index, ...rest) => {
        tokens[index].attrSet("src", context.url(tokens[index].attrGet("src"), true));
        return renderImage(tokens, index, ...rest);
      };
      let body = md.renderer.render(parsed.tokens, md.options, { filePath: source });
      const additionalSamples = samples.filter(sample => sample.page === source && !context.linkedSamples.has(sample.id));
      for (const sample of additionalSamples) {
        if (!sample.codeKey || !Object.hasOwn(parsed.bindings, sample.codeKey)) {
          throw new Error(`${source}: Sample "${sample.id}" has no matching component, fence, or static binding "${sample.codeKey ?? ""}"`);
        }
      }
      if (additionalSamples.length) {
        body += `<aside class="additional-samples"><p><strong>Additional samples</strong></p><ul>${additionalSamples.map(sample =>
          `<li>${escapeHtml(sample.title ?? sample.id)} — <a class="sample-link" href="${SITE_BASE}samples/${escapeHtml(sample.id)}/">Open sample</a></li>`).join("")}</ul></aside>\n`;
      }
      const home = parsed.frontmatter.layout === "home" ? structuredClone(parsed.frontmatter) : undefined;
      if (home) {
        for (const entry of [...(home.hero?.actions ?? []), ...(home.features ?? [])]) {
          if (entry.link) context.url(entry.link);
        }
      }
      const title = parsed.frontmatter.title ?? headings.find(heading => heading.level === 1)?.text ?? home?.hero?.name ?? path.basename(source, ".md");
      const text = searchText(body, md);
      pages.push({
        route: routeFromPath(source), title,
        description: parsed.frontmatter.description ?? home?.hero?.tagline ?? text.slice(0, 200),
        body, headings, section: source.startsWith("reference/") ? "reference" : source.startsWith("guide/") ? "guide" : "home",
        ...(home ? { home } : {}),
      });
    } catch (error) {
      diagnostics.push({ kind: "content", source, message: error.message });
    }
  }
  const pageByRoute = new Map(pages.map(page => [page.route, page]));
  const checkedAssets = new Map();
  for (const link of links) {
    let message;
    if (link.asset) {
      if (!checkedAssets.has(link.route)) {
        try { checkedAssets.set(link.route, (await stat(path.join(root, "../public", link.route))).isFile()); }
        catch { checkedAssets.set(link.route, false); }
      }
      if (!checkedAssets.get(link.route)) message = `Missing public asset "${link.original}"`;
    } else if (!routes.has(link.route)) {
      message = `Unresolved internal link "${link.original}" (target: ${link.route})`;
    } else {
      const hashIndex = link.suffix.indexOf("#");
      if (hashIndex >= 0 && link.suffix.length > hashIndex + 1) {
        const fragment = decodeURIComponent(link.suffix.slice(hashIndex + 1));
        const page = pageByRoute.get(link.route);
        if (page && !page.headings.some(heading => heading.id === fragment) &&
            !page.body.includes(`id="${escapeHtml(fragment)}"`)) {
          message = `Unresolved anchor "${link.original}" (target: ${link.route}#${fragment})`;
        }
      }
    }
    if (message) diagnostics.push({ kind: "link", source: link.source, original: link.original, target: link.route, message });
  }
  if (diagnostics.length) {
    const error = new ContentCompilationError(diagnostics);
    error.obsoleteLinks = obsoleteLinks;
    throw error;
  }
  const md = createMarkdown();
  const search = pages.map(page => ({
    route: page.route, title: page.title,
    text: [page.home?.hero?.text, page.home?.hero?.tagline,
      ...(page.home?.features ?? []).flatMap(feature => [feature.title, feature.details]), searchText(page.body, md)]
      .filter(Boolean).join(" "),
  }));
  return { pages, search, obsoleteLinks };
}
