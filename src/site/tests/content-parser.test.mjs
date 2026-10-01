import assert from "node:assert/strict";
import { mkdir, readFile, readdir, rm, symlink, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { parseContent, readContent } from "../scripts/content-parser.mjs";

const site = fileURLToPath(new URL("../", import.meta.url));
const fixture = path.join(site, ".generated", `content-parser-tests-${process.pid}`);

test.before(async () => {
  await mkdir(path.join(fixture, "content", "guide", "snippets"), { recursive: true });
  await writeFile(path.join(fixture, "content", "guide", "snippets", "example.cs"), 'v.Text("<Hello>");\n');
  await writeFile(path.join(fixture, "private.cs"), "not documentation");
});
test.after(async () => rm(fixture, { recursive: true, force: true }));

const options = () => ({
  filePath: path.join(fixture, "content", "guide", "example.md"),
  contentDirectory: path.join(fixture, "content"),
});

test("extracts literal bindings and raw imports without executing JavaScript", async () => {
  const parsed = await parseContent(`---
title: Literal title
---
<script setup lang="ts">
import snippet from './snippets/example.cs?raw'
const sample: string = \`using Hex1b;
v.Text("quoted \\"value\\"");\`
</script>

# Example

<CodeBlock :code="sample" example="example" />
<StaticTerminalPreview :code="snippet" />
`, options());
  assert.equal(parsed.frontmatter.title, "Literal title");
  assert.equal(parsed.bindings.snippet, 'v.Text("<Hello>");\n');
  assert.match(parsed.bindings.sample, /using Hex1b;/);
  assert.equal(parsed.components[0].codeKey, "sample");
  assert.equal(parsed.components[0].props.code, parsed.bindings.sample);
  assert.equal(parsed.components[1].props.code, parsed.bindings.snippet);
  assert.doesNotMatch(parsed.markdown, /<script|const sample|import snippet/);
});

test("leaves fenced component examples and inline generic code untouched", async () => {
  const parsed = await parseContent(`# Example

\`\`\`html
<UnknownWidget :code="missing" />
<script setup>throw new Error("not run");</script>
\`\`\`

Use \`WidgetContext<T>\` and \`<CodeBlock />\`.
`, options());
  assert.equal(parsed.components.length, 0);
  assert.match(parsed.tokens.find(token => token.type === "fence").content, /<UnknownWidget/);
});

test("exports one-based C# fence keys excluding other languages and component slots", async () => {
  const parsed = await parseContent(`\`\`\`bash
dotnet run
\`\`\`

<CodeBlock>
\`\`\`csharp
SlotCode();
\`\`\`
</CodeBlock>

::: details Example
\`\`\`csharp
StandaloneProgram();
\`\`\`
:::

\`\`\`csharp
SecondProgram();
\`\`\`
`, options());
  assert.deepEqual(parsed.fences.map(fence => [fence.codeKey, fence.language]), [
    ["markdown-fence-0", "bash"], ["fence-1", "csharp"], ["fence-2", "csharp"],
  ]);
  assert.equal(parsed.fences[1].code, "StandaloneProgram();\n");
  assert.equal(parsed.components[0].props.code, "SlotCode();\n");
});

test("supports multiline properties, code slots, and components nested in HTML", async () => {
  const parsed = await parseContent(`<CodeBlock
  lang="xml"
  title="A > B">
<template #code>
\`\`\`xml
<Widget />
</CodeBlock>
\`\`\`
</template>
</CodeBlock>

<div><StaticCodeBlock code="partial snippet" /></div>
`, options());
  assert.equal(parsed.components.length, 2);
  assert.equal(parsed.components[0].props.title, "A > B");
  assert.equal(parsed.components[0].props.code, "<Widget />\n</CodeBlock>\n");
  assert.equal(parsed.components[1].props.code, "partial snippet");
});

test("fails explicitly for unknown components and unresolved bindings", async () => {
  await assert.rejects(parseContent("<UnknownWidget />", options()), /Unknown documentation component/);
  await assert.rejects(parseContent('<CodeBlock :code="missing" />', options()), /Unsupported dynamic documentation expression: missing/);
  await assert.rejects(parseContent('<CodeBlock @click="doSomething" code="x" />', options()), /Runtime directive/);
});

test("rejects dynamic script statements and template expressions", async () => {
  for (const script of [
    'const code = fetch("https://example.org")',
    "const code = `${process.env.SECRET}`",
    "globalThis.__contentCompilerRan = true",
    'import code from "node:fs"',
  ]) {
    await assert.rejects(parseContent(`<script setup>\n${script}\n</script>`, options()), /Unsupported/);
  }
  assert.equal(globalThis.__contentCompilerRan, undefined);
});

test("accepts only YAML frontmatter rather than gray-matter's executable engines", async () => {
  await assert.rejects(parseContent("---javascript\n(globalThis.__contentCompilerRan = true)\n---\n", options()), /Only YAML frontmatter/);
  await assert.rejects(parseContent("---\nvalue: !!js/function >\n  function () { return true }\n---", options()), /unknown tag/);
  assert.equal(globalThis.__contentCompilerRan, undefined);
});

test("restricts raw imports to content files including through symlinks", async () => {
  await assert.rejects(parseContent('<script setup>\nimport code from "../../private.cs?raw"\n</script>', options()), /escapes content directory/);
  await symlink(path.join(fixture, "private.cs"), path.join(fixture, "content", "guide", "outside.cs"));
  await assert.rejects(parseContent('<script setup>\nimport code from "./outside.cs?raw"\n</script>', options()), /symlink escapes content directory/);
});

test("parses every authored page while preserving copied source bytes", async () => {
  const content = path.join(site, "content");
  let pages = 0;
  let components = 0;
  async function walk(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) await walk(file);
      else if (entry.name.endsWith(".md")) {
        const before = await readFile(file);
        const parsed = await readContent(file, { contentDirectory: content });
        assert.deepEqual(await readFile(file), before);
        const original = path.join(site, "../content", path.relative(content, file));
        assert.deepEqual(await readFile(original), before, `Authored prose changed: ${file}`);
        assert.ok(parsed.components.every(component => component.name !== "CodeBlock" || typeof component.props.code === "string"));
        pages++;
        components += parsed.components.length;
      }
    }
  }
  await walk(content);
  assert.equal(pages, 71);
  assert.equal(components, 215);
});
