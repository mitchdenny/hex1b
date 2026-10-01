import assert from "node:assert/strict";
import { test } from "node:test";
import { createFileTree, sourceFiles } from "../src/sample-files.ts";

const file = (path, content = "verbatim\r\nsource") => ({ path, content, language: "csharp" });

test("Virtual filesystem groups nested paths, sorts folders first and preserves exact contents", () => {
  const files = [file("Program.cs"), file("Models/State.cs"), file("Demo.csproj"), file("Art/Scene/Palette.cs")];
  const tree = createFileTree(files);
  assert.equal(tree.name, "Demo");
  assert.deepEqual(tree.children.map(entry => entry.name), ["Art", "Models", "Demo.csproj", "Program.cs"]);
  assert.deepEqual(sourceFiles(tree).map(entry => entry.path),
    ["Art/Scene/Palette.cs", "Models/State.cs", "Demo.csproj", "Program.cs"]);
  assert.equal(sourceFiles(tree)[0].file.content, "verbatim\r\nsource");
  assert.equal(files[0].path, "Program.cs");
});

test("Virtual paths normalize Windows separators without rewriting source", () => {
  const original = file("Models\\State.cs");
  const entry = sourceFiles(createFileTree([original]))[0];
  assert.equal(entry.path, "Models/State.cs");
  assert.equal(entry.file.path, "Models/State.cs");
  assert.equal(original.path, "Models\\State.cs");
  assert.equal(entry.file.content, original.content);
});

test("Empty, absolute, traversal and duplicate paths are rejected explicitly", () => {
  assert.throws(() => createFileTree([]), /no source/);
  for (const path of ["", "/Program.cs", "../Program.cs", "./Program.cs", "a//b.cs", "a/../b.cs", "C:\\Program.cs", "a/\u0000.cs"]) {
    assert.throws(() => createFileTree([file(path)]), /Invalid sample file path/);
  }
  assert.throws(() => createFileTree([file("a.cs"), file("a.cs")]), /Duplicate/);
  assert.throws(() => createFileTree([file("a\\b.cs"), file("a/b.cs")]), /Duplicate/);
});

test("Files cannot also be virtual folders, regardless of manifest ordering", () => {
  assert.throws(() => createFileTree([file("app"), file("app/Program.cs")]), /directory/);
  assert.throws(() => createFileTree([file("app/Program.cs"), file("app")]), /conflicting/);
});
