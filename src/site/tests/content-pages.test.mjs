import assert from "node:assert/strict";
import { mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { compileSite, ContentCompilationError, rebaseContentUrl, routeFromPath } from "../scripts/content-pages.mjs";
import { createNavigation, guideSidebar, navigation, referenceSidebar } from "../scripts/navigation.mjs";

const site = fileURLToPath(new URL("../", import.meta.url));
const root = path.join(site, ".generated", `content-pages-tests-${process.pid}`);
let sequence = 0;

async function fixture(files) {
  const directory = path.join(root, String(sequence++));
  for (const [filename, text] of Object.entries(files)) {
    const target = path.join(directory, filename);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, text);
  }
  return { contentDirectory: path.join(directory, "content"), packageVersion: "0.99.7", directory };
}
test.after(async () => rm(root, { recursive: true, force: true }));

test("renders Markdown fidelity, callouts, tables, explicit anchors and prefix-safe links", async () => {
  const options = await fixture({
    "content/index.md": "# Home\n\n[Guide](/guide/example.md?q=a#specific)\n",
    "content/guide/example.md": `# Example

Original prose with **bold**, *emphasis*, and \`Widget<T>\`.

## A heading {#specific}

## Repeated
## Repeated

::: tip Keep this title
Retain this **callout**.
:::

::: details Details title
Hidden but readable.
:::

| Column | Value |
| --- | --- |
| One | Two |

[Home](/) [External](https://example.com/a.md?q=1#x)

<a href="../index.md">Home HTML</a>

![Preview](/svg/example.svg)
`,
    "public/svg/example.svg": '<svg xmlns="http://www.w3.org/2000/svg"></svg>',
  });
  const result = await compileSite(options);
  const home = result.pages.find(page => page.route === "");
  const guide = result.pages.find(page => page.route === "guide/example/");
  assert.match(home.body, /href="__SITE_BASE__guide\/example\/\?q=a#specific"/);
  assert.match(guide.body, /href="__SITE_BASE__"/);
  assert.match(guide.body, /href="https:\/\/example.com\/a.md\?q=1#x"/);
  assert.match(guide.body, /src="__SITE_BASE__svg\/example.svg"/);
  assert.match(guide.body, /<strong>bold<\/strong>/);
  assert.match(guide.body, /<table>/);
  assert.match(guide.body, /<details class="custom-block details"><summary>Details title<\/summary>/);
  assert.deepEqual(guide.headings.map(heading => heading.id), ["example", "specific", "repeated", "repeated-1"]);
  assert.doesNotMatch(guide.body, /\{#specific\}/);
  assert.match(result.search.find(page => page.route === guide.route).text, /Original prose with bold, emphasis, and Widget<T>/);
});

test("maps static components, retains partial snippets, and links complete samples", async () => {
  const options = await fixture({
    "content/index.md": `<script setup>
const full = \`using Hex1b;
await app.RunAsync();\`
const partial = "v.Text(\\"Partial\\");"
</script>

# Components

<CodeBlock :code="full" command="dotnet run" example="complete" />

<StaticTerminalPreview svgPath="/svg/preview.svg" :code="partial" />

<StaticTerminalPreview :code="partial" />

<TerminalCommand command="dotnet add package Hex1b --version {{version}}" />

<TerminalDemo example="complete" title="Original demo title" />

<FlowDiagram />

<div><StaticCodeBlock :code="partial" /></div>

\`\`\`html
<CodeBlock :code="doNotResolve" />
\`\`\`
`,
    "public/svg/preview.svg": '<svg xmlns="http://www.w3.org/2000/svg"></svg>',
  });
  const { pages } = await compileSite({ ...options, samples: [{ id: "complete", title: "Full sample", page: "index.md", codeKey: "full", example: "complete" }] });
  const body = pages[0].body;
  assert.match(body, /href="__SITE_BASE__samples\/complete\/">Open sample/);
  assert.match(body, /v.Text\(&quot;Partial&quot;\);/);
  assert.match(body, /<details><summary>View code<\/summary>/);
  assert.match(body, /Hex1b --version 0.99.7/);
  assert.match(body, /Original demo title/);
  assert.match(body, /Step 4/);
  assert.match(body, /&lt;CodeBlock :code=&quot;doNotResolve&quot; \/&gt;/);
  assert.doesNotMatch(body, /<script|<CodeBlock|<StaticCodeBlock|<TerminalDemo|\/api\/version|WebSocket|\0/);
});

test("preserves home frontmatter wording and compiles install commands at build time", async () => {
  const options = await fixture({
    "content/index.md": `---
layout: home
hero:
  name: Hex1b
  text: The .NET Terminal Application Stack
  tagline: Original tagline!
  actions:
    - text: Get Started
      theme: brand
      link: /guide/
features:
  - title: Keep this feature
    details: All of this text.
    link: /guide/
---
<InstallGuide />`,
    "content/guide/index.md": "# Guide",
  });
  const { pages, search } = await compileSite(options);
  const home = pages.find(page => page.route === "");
  assert.equal(home.home.hero.text, "The .NET Terminal Application Stack");
  assert.equal(home.home.features[0].details, "All of this text.");
  assert.match(home.body, /dotnet add package Hex1b@0.99.7/);
  assert.match(home.body, /PackageReference Include=&quot;Hex1b&quot; Version=&quot;0.99.7&quot;/);
  assert.match(home.body, /#:package Hex1b@0.99.7/);
  assert.match(search.find(page => page.route === "").text, /Keep this feature All of this text/);
});

test("uses the recorded sample's Program.cs for complete demos but preserves partial snippets", async () => {
  const options = await fixture({
    "content/index.md": `<script setup>
const complete = "LegacyCompleteApi();"
const partial = "OriginalPartialApi();"
</script>

# Examples

<CodeBlock :code="complete" example="complete" />

<CodeBlock :code="partial" />

<StaticTerminalPreview :code="partial" />
`,
  });
  const { pages } = await compileSite({
    ...options,
    samples: [{
      id: "complete", title: "Recorded sample", page: "index.md", codeKey: "complete", example: "complete",
      files: [{ path: "Program.cs", content: 'CurrentApi("<Recorded source>");\n', language: "csharp" }],
    }],
  });
  assert.match(pages[0].body, /CurrentApi\(&quot;&lt;Recorded source&gt;&quot;\);/);
  assert.doesNotMatch(pages[0].body, /LegacyCompleteApi/);
  assert.equal(pages[0].body.match(/OriginalPartialApi\(\);/g)?.length, 2);
});

test("prioritizes page and codeKey identity over an earlier matching example ID", async () => {
  const options = await fixture({
    "content/index.md": `<script setup>
const complete = "OriginalProgram();"
const other = "OtherProgram();"
</script>

# Examples

<CodeBlock :code="complete" example="shared-example" />
`,
  });
  const { pages } = await compileSite({
    ...options,
    samples: [
      { id: "other-key", page: "index.md", codeKey: "other", example: "shared-example",
        files: [{ path: "Program.cs", content: "WrongProgram();" }] },
      { id: "correct-key", page: "index.md", codeKey: "complete",
        files: [{ path: "Program.cs", content: "CorrectProgram();" }] },
    ],
  });
  assert.match(pages[0].body, /CorrectProgram\(\);/);
  assert.match(pages[0].body, /samples\/correct-key\//);
  assert.doesNotMatch(pages[0].body, /WrongProgram/);
  assert.match(pages[0].body.match(/href="__SITE_BASE__samples\/[^"]+/)[0], /samples\/correct-key\//);
});

test("links cataloged fenced programs without changing unmatched partial fences", async () => {
  const options = await fixture({
    "content/index.md": `# Programs

\`\`\`bash
dotnet run
\`\`\`

\`\`\`csharp
OldCompleteProgram();
\`\`\`

\`\`\`csharp
OriginalPartialSnippet();
\`\`\`
`,
  });
  const { pages } = await compileSite({
    ...options,
    samples: [{
      id: "fenced-program", title: "Fenced program", page: "index.md", codeKey: "fence-1",
      files: [{ path: "Program.cs", content: "RecordedProgram();\n", language: "csharp" }],
    }],
  });
  assert.match(pages[0].body, /RecordedProgram\(\);/);
  assert.match(pages[0].body, /href="__SITE_BASE__samples\/fenced-program\/">Open sample/);
  assert.match(pages[0].body, /OriginalPartialSnippet\(\);/);
  assert.doesNotMatch(pages[0].body, /OldCompleteProgram/);
});

test("links complete imported preview programs to their recorded standalone source", async () => {
  const options = await fixture({
    "content/index.md": `<script setup>
import program from './program.cs?raw'
const partial = "OriginalPartialSnippet();"
</script>

# Previews

<StaticTerminalPreview :code="program" svgPath="/svg/program.svg" />

<StaticTerminalPreview :code="partial" />
`,
    "content/program.cs": "OriginalCompleteProgram();\n",
    "public/svg/program.svg": '<svg xmlns="http://www.w3.org/2000/svg"></svg>',
  });
  const { pages } = await compileSite({
    ...options,
    samples: [{
      id: "imported-program", title: "Imported program", page: "index.md", codeKey: "program",
      files: [{ path: "Program.cs", content: "RecordedImportedProgram();\n", language: "csharp" }],
    }],
  });
  assert.match(pages[0].body, /RecordedImportedProgram\(\);/);
  assert.match(pages[0].body, /href="__SITE_BASE__samples\/imported-program\/">Open sample/);
  assert.match(pages[0].body, /OriginalPartialSnippet\(\);/);
  assert.doesNotMatch(pages[0].body, /OriginalCompleteProgram/);
});

test("adds contextual sample links for full raw imports not referenced by an authored component", async () => {
  const source = `<script setup>
import basicSnippet from './basic.cs?raw'
const displayed = "DisplayedProgram();"
</script>

# Terminal

Original prose.

<CodeBlock :code="displayed" />
`;
  const options = await fixture({
    "content/index.md": source,
    "content/basic.cs": "ImportedProgram();\n",
  });
  const { pages, search } = await compileSite({
    ...options,
    samples: [{
      id: "terminal-basic-basic-snippet", title: "Terminal basic snippet",
      page: "index.md", codeKey: "basicSnippet",
      files: [{ path: "Program.cs", content: "RecordedImportedProgram();\n", language: "csharp" }],
    }],
  });
  assert.match(pages[0].body, /Original prose\./);
  assert.match(pages[0].body, /DisplayedProgram\(\);/);
  assert.match(pages[0].body, /Additional samples/);
  assert.match(pages[0].body, /href="__SITE_BASE__samples\/terminal-basic-basic-snippet\/">Open sample/);
  assert.doesNotMatch(pages[0].body, /ImportedProgram\(\);/);
  assert.match(search[0].text, /Terminal basic snippet/);
  assert.equal(await readFile(path.join(options.contentDirectory, "index.md"), "utf8"), source);
});

test("rejects unmatched catalog keys instead of disguising mapping mistakes as additional samples", async () => {
  const options = await fixture({ "content/index.md": "# Home" });
  await assert.rejects(compileSite({
    ...options,
    samples: [{ id: "typo", page: "index.md", codeKey: "missingBinding", title: "Typo" }],
  }), /no matching component, fence, or static binding "missingBinding"/);
});

test("links source-only integration samples without requiring or promising a recording", async () => {
  const options = await fixture({
    "content/index.md": `# Hosting

\`\`\`csharp
OriginalServerProgram();
\`\`\`
`,
  });
  const { pages } = await compileSite({
    ...options,
    samples: [{
      id: "web-host", title: "ASP.NET hosting", page: "index.md", codeKey: "fence-1",
      sourceOnly: true, prerequisites: "ASP.NET Core runtime and an available local port.",
      files: [{ path: "Program.cs", content: "CompleteServerProgram();\n", language: "csharp" }],
    }],
  });
  assert.match(pages[0].body, /CompleteServerProgram\(\);/);
  assert.match(pages[0].body, /href="__SITE_BASE__samples\/web-host\/">Open sample/);
  assert.doesNotMatch(pages[0].body, /OriginalServerProgram|Watch recording|Playback|<button/);
});

test("merges generated API references without replacing authored reference pages", async () => {
  const options = await fixture({
    "content/reference/index.md": "# Authored reference\n\n[Generic](Hex1b.WidgetContext-1.md#members)",
    "reference/index.md": "# Must not replace authored prose",
    "reference/Hex1b.WidgetContext-1.md": "# WidgetContext\\<T\\>\n\n## Members\n\nAPI prose.",
  });
  const { pages } = await compileSite({ ...options, referenceDirectory: path.join(options.directory, "reference") });
  assert.equal(pages.length, 2);
  assert.equal(pages.find(page => page.route === "reference/").title, "Authored reference");
  assert.equal(pages.find(page => page.route === "reference/Hex1b.WidgetContext-1/").title, "WidgetContext<T>");
});

test("fails with actionable diagnostics for missing pages, anchors, assets and samples", async () => {
  const options = await fixture({ "content/index.md": "# Home\n\n[Missing](missing.md)\n\n[Bad anchor](#missing)\n\n![Image](/svg/missing.svg)" });
  await assert.rejects(compileSite(options), error => {
    assert.ok(error instanceof ContentCompilationError);
    assert.equal(error.unresolvedLinks.length, 3);
    assert.ok(error.unresolvedLinks.every(item => item.source === "index.md" && item.original));
    return true;
  });
  const missingSample = await fixture({ "content/index.md": '# Home\n\n<TerminalDemo example="missing" />' });
  await assert.rejects(compileSite(missingSample), /Missing standalone sample/);
});

test("never emits runtime HTML or unsupported component properties", async () => {
  for (const html of [
    '<script src="https://example.org/client.js"></script>',
    '<img src="image.svg" onerror="alert(1)">',
    '<a href="javascript:alert(1)">unsafe</a>',
    '<CodeBlock code="x" unknown="x" />',
  ]) {
    const options = await fixture({ "content/index.md": `# Home\n\n${html}` });
    await assert.rejects(compileSite(options), /not supported|Unsupported|Only static/);
  }
});

test("canonicalizes paths without losing queries, anchors or deployment prefixes", () => {
  assert.equal(routeFromPath("index.md"), "");
  assert.equal(routeFromPath("guide/index.md"), "guide/");
  assert.equal(routeFromPath("reference/Hex1b.Widget-1.md"), "reference/Hex1b.Widget-1/");
  assert.equal(rebaseContentUrl("../layout.md?q=1#size", "guide/widgets/text.md"), "__SITE_BASE__guide/layout/?q=1#size");
  assert.equal(rebaseContentUrl("?q=1#size", "guide/widgets/text.md"), "__SITE_BASE__guide/widgets/text/?q=1#size");
  assert.equal(rebaseContentUrl("/", "guide/index.md"), "__SITE_BASE__");
  assert.equal(rebaseContentUrl("#local", "guide/index.md"), "#local");
  assert.equal(rebaseContentUrl("//example.org/logo.svg", "guide/index.md"), "//example.org/logo.svg");
  assert.throws(() => rebaseContentUrl("../../private", "guide/index.md"), /escapes site root/);
  assert.throws(() => rebaseContentUrl("/%2e%2e/private", "guide/index.md"), /escapes site root/);
  assert.throws(() => rebaseContentUrl("/%2fprivate", "guide/index.md"), /Invalid URL path/);
});

test("preserves legacy sidebar sections and adds alphabetized widget discovery", () => {
  const result = createNavigation([
    { route: "guide/widgets/text/", title: "Text" },
    { route: "guide/widgets/button/", title: "Button" },
    { route: "guide/widgets/", title: "Widgets" },
  ]);
  assert.deepEqual(result.nav, navigation);
  assert.deepEqual(guideSidebar.map(group => group.text), ["Overview", "Features", "Building TUIs", "Terminal Stack", "Observability", "Reference", "Tools"]);
  assert.deepEqual(referenceSidebar.map(group => group.text), ["API Reference", "CLI Reference", "Namespaces"]);
  assert.deepEqual(result.sidebar["/guide/"].at(-1).items.map(item => item.text), ["Button", "Text"]);
});

test("compiles the complete authored site with generated API references and the real sample catalog", async context => {
  const referenceDirectory = path.join(site, ".generated/reference");
  let references;
  try {
    references = await readdir(referenceDirectory);
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
    context.skip("Generate optional API Markdown before running the full-site integration test");
    return;
  }
  const samples = JSON.parse(await readFile(path.join(site, "samples/catalog.json"), "utf8"));
  const { packageVersion } = JSON.parse(await readFile(path.join(site, "site.config.json"), "utf8"));
  const { pages, search } = await compileSite({
    contentDirectory: path.join(site, "content"), referenceDirectory, samples, packageVersion,
  });
  const generatedCount = references.filter(file => file.endsWith(".md") && file !== "index.md" && file !== "cli.md").length;
  assert.equal(pages.length, 71 + generatedCount);
  assert.equal(search.length, pages.length);
  assert.equal(new Set(pages.map(page => page.route)).size, pages.length);
  const sampleIds = new Set(samples.map(sample => sample.id));
  const linkedSampleIds = new Set();
  for (const page of pages) {
    assert.doesNotMatch(page.body, /<script\b|<CodeBlock\b|<StaticTerminalPreview\b|<TerminalDemo\b/);
    for (const match of page.body.matchAll(/href="__SITE_BASE__samples\/([^/]+)\//g)) {
      assert.ok(sampleIds.has(match[1]), `Unknown sample link on ${page.route}: ${match[1]}`);
      linkedSampleIds.add(match[1]);
    }
  }
  assert.deepEqual([...linkedSampleIds].sort(), [...sampleIds].sort(), "Every catalog sample must have a contextual documentation link");
});
