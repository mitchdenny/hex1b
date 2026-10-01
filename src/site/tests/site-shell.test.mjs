import assert from "node:assert/strict";
import { test } from "node:test";
import { pageBase, renderDocument, sampleContent } from "../scripts/site-shell.mjs";

test("Static documents resolve all first-party shell assets relative to their page", () => {
  for (const [route, base] of [["", "./"], ["guide/", "../"], ["guide/widgets/button/", "../../../"]]) {
    assert.equal(pageBase(route), base);
    const html = renderDocument({
      page: { route, title: "Buttons <&>", description: "A sample", body: '<a href="__SITE_BASE__samples/button-basic/">Sample</a>', headings: [] },
      sidebar: [{ text: "Guide", items: [{ text: "Button", link: "/guide/widgets/button" }] }],
      javascript: "assets/client.js", stylesheets: ["assets/client.css"], packageVersion: "0.166.0",
    });
    assert.ok(html.includes(`src="${base}assets/client.js"`));
    assert.ok(html.includes(`href="${base}samples/button-basic/"`));
    assert.ok(html.includes(`data-site-base="${base}"`));
    assert.ok(html.includes(`href="${base}guide/widgets/button/"`));
    assert.ok(html.includes("Buttons &lt;&amp;&gt;"));
    assert.ok(!html.includes("__SITE_BASE__"));
    assert.ok(!html.includes("vitepress"));
  }
});

test("Sample documents contain source without requiring client rendering", () => {
  const body = sampleContent({
    id: "test-sample", title: "Example", command: "dotnet run",
    files: [{ path: "Program.cs", content: 'Console.WriteLine("<tag>");', language: "csharp" }],
  });
  assert.ok(body.includes('data-sample-manifest="recordings/test-sample/sample.json"'));
  assert.ok(body.includes("Console.WriteLine(&quot;&lt;tag&gt;&quot;);"));
  assert.ok(body.includes("<noscript>"));
  assert.ok(body.includes("Clone sample"));
});

test("Environment-dependent samples expose source and prerequisites without fake playback", () => {
  const body = sampleContent({
    id: "hosting", title: "Hosting", sourceOnly: true, prerequisites: "Requires a local ASP.NET host.",
    files: [{ path: "Program.cs", content: "app.Run();", language: "csharp" }],
  });
  assert.ok(body.includes("Requires a local ASP.NET host."));
  assert.ok(body.includes("Clone sample"));
  assert.ok(body.includes('id="source-browser"'));
  assert.ok(!body.includes('id="terminal"'));
  assert.ok(!body.includes('id="play"'));
});
