import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { compileSite } from "./content-pages.mjs";
import { sidebarFor } from "./navigation.mjs";
import { escapeHtml, renderDocument, sampleContent } from "./site-shell.mjs";

const site = fileURLToPath(new URL("../", import.meta.url));
const dist = join(site, "dist");
const config = JSON.parse(await readFile(join(site, "site.config.json"), "utf8"));
const samples = JSON.parse(await readFile(join(site, ".generated/samples.json"), "utf8"));
const manifest = JSON.parse(await readFile(join(dist, ".vite/manifest.json"), "utf8"));
const entry = manifest["src/client.ts"];
if (!entry?.isEntry || !entry.file) throw new Error("Missing Vite client entry");
const { pages, search } = await compileSite({
  contentDirectory: join(site, "content"),
  referenceDirectory: join(site, ".generated/reference"),
  samples, packageVersion: config.packageVersion,
});
samples.sort((a, b) => a.title.localeCompare(b.title));
const sampleSidebar = [{ text: "Samples", items: [
  { text: "Overview", link: "/samples/" },
  ...samples.map(sample => ({ text: sample.title, link: `/samples/${sample.id}/` })),
] }];
pages.push({
  route: "samples/", title: "Samples", description: "Standalone .NET terminal samples with recorded output and source.",
  headings: [],
  body: `<h1>Samples</h1><p>Watch recorded terminal output, explore the source files, and clone a standalone project to run locally.</p>
    <div class="samples-grid">${samples.map(sample => `<a class="sample-card" href="__SITE_BASE__samples/${escapeHtml(sample.id)}/">
      <h2>${escapeHtml(sample.title)}</h2><p>${escapeHtml(sample.description ?? "Standalone .NET sample")}</p>
      <span class="feature-link">${sample.sourceOnly ? "Source, prerequisites and clone" : "Recording, source and clone"}</span></a>`).join("")}</div>`,
});
for (const sample of samples) {
  pages.push({ route: `samples/${sample.id}/`, title: sample.title, description: sample.description, headings: [],
    body: sampleContent(sample) });
  search.push({ route: `samples/${sample.id}/`, title: sample.title,
    text: `${sample.description ?? ""} ${sample.files.find(file => file.path === "Program.cs")?.content ?? ""}` });
}
const routes = new Set();
const documents = [];
for (const page of pages) {
  if (routes.has(page.route)) throw new Error(`Duplicate page route: ${page.route}`);
  routes.add(page.route);
  const html = renderDocument({
    page, sidebar: page.route.startsWith("samples/") ? sampleSidebar : sidebarFor(page.route),
    javascript: entry.file, stylesheets: entry.css ?? [], packageVersion: config.packageVersion,
  });
  const path = join(dist, page.route, "index.html");
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, html);
  documents.push({ page, html });
}
await writeFile(join(dist, "search.json"), JSON.stringify(search));
const missing = new Set();
for (const { page, html } of documents) {
  for (const match of html.matchAll(/\b(?:href|src)="([^"]+)"/gu)) {
    const value = match[1].replaceAll("&amp;", "&");
    if (/^(?:[a-z][a-z\d+.-]*:|#|\/\/)/iu.test(value)) continue;
    const url = new URL(value, `https://static.invalid/${page.route}`);
    const path = join(dist, decodeURIComponent(url.pathname));
    try {
      const info = await stat(path);
      if (info.isDirectory()) await stat(join(path, "index.html"));
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
      missing.add(`${page.route || "/"} -> ${value}`);
    }
  }
}
if (missing.size) {
  await writeFile(join(site, ".generated/missing-links.json"), JSON.stringify([...missing], null, 2));
  throw new Error(`Broken static links (${missing.size}):\n${[...missing].slice(0, 40).join("\n")}`);
}
console.log(`Generated ${pages.length} static pages, ${samples.length} samples and a local search index.`);
