export const escapeHtml = value => String(value ?? "").replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");

export function pageBase(route) {
  return route ? "../".repeat(route.split("/").filter(Boolean).length) : "./";
}

function localLink(link) {
  if (/^(?:[a-z][a-z\d+.-]*:|#|\/\/)/iu.test(link)) return link;
  const [, pathname, suffix] = /^([^?#]*)(.*)$/u.exec(link);
  const path = pathname.replace(/^\//u, "").replace(/\.md$/u, "");
  return `__SITE_BASE__${path ? path.replace(/\/?$/u, "/") : ""}${suffix}`;
}

function sidebarItems(items, route) {
  return items.map(item => {
    const link = item.link?.replace(/^\//u, "").replace(/\/?$/u, "/");
    return `<li>${item.link
      ? `<a href="${escapeHtml(localLink(item.link))}"${link === route ? ' aria-current="page"' : ""}>${escapeHtml(item.text)}</a>`
      : `<span class="sidebar-group">${escapeHtml(item.text)}</span>`}
      ${item.items ? `<ul>${sidebarItems(item.items, route)}</ul>` : ""}</li>`;
  }).join("");
}

function homeContent(home) {
  const hero = home.hero;
  return `<section class="home-hero">
    <div><p class="hero-name">${escapeHtml(hero.name)}</p><h1>${escapeHtml(hero.text)}</h1>
    <p class="hero-tagline">${escapeHtml(hero.tagline)}</p><div class="hero-actions">
      ${hero.actions.map(action => `<a class="site-button ${action.theme === "brand" ? "primary" : ""}" href="${escapeHtml(localLink(action.link))}">${escapeHtml(action.text)}</a>`).join("")}
    </div></div>
    <div class="hero-terminal" aria-hidden="true"><div class="terminal-chrome"><i></i><i></i><i></i><span>Hex1b</span></div>
      <div class="terminal-illustration"><span>› dotnet run</span>
        <div class="hero-ui-buttons"><i></i><i></i><i></i></div>
        <div class="hero-ui-line"></div><div class="hero-ui-line short"></div>
        <div class="hero-ui-list"><div></div><div></div><div></div></div>
      </div></div>
  </section>
  <section class="feature-grid">${home.features.map(feature => `<a class="feature-card" href="${escapeHtml(localLink(feature.link))}">
    <span class="feature-icon" aria-hidden="true">${escapeHtml(feature.icon)}</span><h2>${escapeHtml(feature.title)}</h2>
    <p>${escapeHtml(feature.details)}</p><span class="feature-link">${escapeHtml(feature.linkText)}</span></a>`).join("")}</section>`;
}

export function sampleContent(sample) {
  const first = sample.files.find(file => file.path === "Program.cs") ?? sample.files[0];
  return `<div data-sample-manifest="recordings/${escapeHtml(sample.id)}/sample.json">
    <p class="eyebrow">Standalone sample</p><h1>${escapeHtml(sample.title)}</h1>
    <p>${escapeHtml(sample.description ?? "Explore the source and run this standalone sample locally.")}</p>
    <p id="sample-error" role="alert" hidden></p>
    ${sample.sourceOnly ? `<section class="custom-block info"><p class="custom-block-title">Source-only sample</p>
      <p>${escapeHtml(sample.prerequisites)}</p><p>This example is intended to run in your own environment; no service is started by this site.</p></section>` :
    `<section class="sample-recording" aria-labelledby="sample-title">
      <div class="section-heading"><h2 id="sample-title">${escapeHtml(sample.title)}</h2><a id="download" download hidden>Download recording</a></div>
      <p id="description"></p><div id="terminal" aria-label="Recorded terminal output"></div>
      <div class="controls" aria-label="Playback controls"><button id="play" disabled>Play</button><button id="pause" disabled>Pause</button><button id="restart" disabled>Restart</button>
        <progress id="progress" value="0" max="1" aria-label="Playback progress"></progress><output id="time">0.0 / 0.0 s</output></div>
      <p id="status" role="status" aria-live="polite">Loading recorded terminal output...</p><p id="metrics" class="muted"></p>
      <details><summary>Current frame as text</summary><pre id="screen-text"></pre></details>
    </section>`}
    <section aria-labelledby="source-title"><div class="section-heading"><h2 id="source-title">Run it yourself</h2>
      <button id="clone" class="site-button" aria-expanded="false" aria-controls="clone-panel" disabled>Clone sample</button></div>
      <p>${sample.sourceOnly ? "Clone these source files to run the example with the prerequisites above." :
        "The source files below are the standalone project used to produce this recording. Capture is handled by a separate driver."}</p>
      <div id="clone-panel" class="clone-panel" role="region" aria-label="Clone sample" hidden>
        <p>A read-only Git snapshot served entirely as static files.</p><div class="section-heading"><span class="muted">Bash / PowerShell</span><button id="copy-clone" class="site-button">Copy clone command</button></div>
        <pre id="clone-command" aria-label="Git clone command"></pre><p>Then run with the .NET 10 SDK:</p><pre id="clone-run"></pre>
        <p class="muted">Pinned NuGet dependencies. No push, shallow clones or evolving history.</p><p id="clone-status" role="status" aria-live="polite"></p>
      </div><p class="muted">From the sample directory:</p><pre id="command">${escapeHtml(sample.command ?? "dotnet run")}</pre>
      <div id="source-browser" class="source-browser"><aside class="file-explorer" aria-label="Sample file explorer"><div class="explorer-heading">Explorer</div><div id="files" role="tree" aria-label="Sample files"></div></aside>
        <div class="source-pane"><div class="source-toolbar"><span id="source-path">${escapeHtml(first.path)}</span><span id="source-language" class="muted">Read only</span></div>
        <div id="source-editor" hidden></div><pre id="source-fallback">${escapeHtml(first.content)}</pre></div></div>
      <p id="source-status" class="muted" role="status" aria-live="polite">The source editor loads when it comes into view.</p>
      <noscript>${sample.files.map(file => `<details><summary>${escapeHtml(file.path)}</summary><pre><code>${escapeHtml(file.content)}</code></pre></details>`).join("")}</noscript>
    </section></div>`;
}

export function renderDocument({ page, sidebar = [], javascript, stylesheets, packageVersion }) {
  const base = pageBase(page.route);
  const isHome = Boolean(page.home);
  const body = `${isHome ? homeContent(page.home) : ""}${page.body}`;
  const outline = (page.headings ?? []).filter(heading => heading.level === 2 || heading.level === 3);
  return `<!doctype html>
<html lang="en" data-site-base="${base}"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapeHtml(page.title)}${isHome ? "" : " | Hex1b"}</title><meta name="description" content="${escapeHtml(page.description)}"><meta name="robots" content="noindex">
<link rel="icon" type="image/svg+xml" href="${base}logo.svg">
<script>(()=>{const q=new URLSearchParams(location.search).get("scoutTheme");let saved;try{saved=localStorage.getItem("hex1b-theme")}catch(e){console.warn("Theme preference unavailable",e)}document.documentElement.dataset.theme=q||saved||(matchMedia("(prefers-color-scheme: dark)").matches?"dark":"light")})();</script>
${stylesheets.map(path => `<link rel="stylesheet" href="${base}${path}">`).join("\n")}
</head><body class="${isHome ? "home-page" : "document-page"}">
<a class="skip-link" href="#main-content">Skip to content</a>
<header class="site-header"><div class="header-inner"><a class="site-brand" href="${base}"><img src="${base}logo.svg" alt="" width="28" height="28"><span>Hex1b</span></a>
<button class="search-trigger site-button" id="open-search">Search <kbd>⌘ K</kbd></button>
<nav class="top-nav" aria-label="Main navigation"><a href="${base}guide/">Guide</a><a href="${base}reference/">API Reference</a><a href="${base}samples/">Samples</a><a href="https://github.com/mitchdenny/hex1b">GitHub</a></nav>
<button id="theme-toggle" class="site-button" aria-label="Switch color theme">Theme</button>
${isHome ? "" : '<button id="menu-toggle" class="site-button menu-toggle" aria-controls="sidebar" aria-expanded="false">Menu</button>'}</div>
<nav class="mobile-top-nav" aria-label="Mobile main navigation"><a href="${base}guide/">Guide</a><a href="${base}reference/">API Reference</a><a href="${base}samples/">Samples</a><a href="https://github.com/mitchdenny/hex1b">GitHub</a></nav></header>
<div class="${isHome ? "home-layout" : "docs-layout"}">
${isHome ? "" : `<aside id="sidebar" class="sidebar"><nav aria-label="Section navigation"><ul>${sidebarItems(sidebar, page.route)}</ul></nav></aside>`}
<main id="main-content" class="${isHome ? "home-content" : "article"}">${body}</main>
${isHome ? "" : `<aside class="page-outline" aria-label="On this page"><p>On this page</p><ul>${outline.map(heading => `<li class="outline-level-${heading.level}"><a href="#${escapeHtml(heading.id)}">${escapeHtml(heading.text)}</a></li>`).join("")}</ul></aside>`}
</div><footer class="site-footer"><div>Released under the MIT License.</div><div>Copyright © 2025 Mitch Denny</div><div class="muted">Standalone samples target Hex1b ${escapeHtml(packageVersion)}.</div></footer>
<dialog id="search-dialog" aria-labelledby="search-title"><div class="section-heading"><h2 id="search-title">Search documentation</h2><button id="close-search" class="site-button">Close</button></div>
<label for="search-input">Search guides, API reference and samples</label><input id="search-input" type="search" autocomplete="off"><p id="search-status" role="status">Type to search.</p><ul id="search-results"></ul></dialog>
<script type="module" src="${base}${javascript}"></script></body></html>`.replaceAll("__SITE_BASE__", base);
}
