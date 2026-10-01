#!/usr/bin/env python3
"""Explicit, idempotent compatibility migration for extracted Hex1b 0.166.0 programs."""
import html
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent


def adapt(code):
    notes = []
    decoded = html.unescape(code)
    if decoded != code:
        notes.append("Decoded HTML entities in the displayed C# (for example &gt; to >).")
        code = decoded
    if re.search(r"\.Text\(`[^`]*`\)", code):
        code = re.sub(r"\.Text\(`([^`]*)`\)", lambda m: '.Text($"' + m[1].replace("${", "{").replace('"', '\\"') + '")', code)
        notes.append("Corrected JavaScript-style backtick interpolation in the legacy C# sample to C# interpolated strings.")
    if ".WithHex1bApp((app, options) =>" in code:
        assignments = re.findall(r"^\s*(options\.\w+\s*=\s*[^;]+;)\s*$", code, re.M)
        code = re.sub(r"^\s*options\.\w+\s*=\s*[^;]+;\s*$", "", code, flags=re.M)
        code = code.replace(".WithHex1bApp((app, options) =>", ".WithHex1bApp(options => { " + " ".join(assignments) + " }, (Hex1bApp app) =>")
        notes.append("Updated WithHex1bApp to the separate eager options/configure-app callbacks in Hex1b 0.166.0.")
    code = re.sub(r"(\.WithHex1bApp\(options => \{[^}]*\}, )app =>", r"\1(Hex1bApp app) =>", code)
    # Legacy Border's title was its final argument. Preserve the complete expression.
    while match := re.search(r",\s*title:\s*", code):
        start = match.end()
        depth = 0
        quoted = False
        escaped = False
        end = start
        while end < len(code):
            char = code[end]
            if quoted:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == '"':
                    quoted = False
            elif char == '"':
                quoted = True
            elif char == "(":
                depth += 1
            elif char == ")":
                if depth == 0:
                    break
                depth -= 1
            end += 1
        if end == len(code):
            raise ValueError("Unterminated title argument")
        title = code[start:end].strip()
        code = code[:match.start()] + ").Title(" + title + ")" + code[end + 1:]
        if "Replaced Border's removed title argument with fluent .Title(...)." not in notes:
            notes.append("Replaced Border's removed title argument with fluent .Title(...).")
    return code, notes


def main():
    catalog = json.loads((ROOT / "catalog.json").read_text())
    changed = 0
    for entry in catalog:
        path = ROOT / entry["id"] / "Program.cs"
        if not path.exists():
            continue
        old = path.read_text()
        code, notes = adapt(old)
        if code != old:
            path.write_text(code)
            changed += 1
        if notes:
            original = entry.get("notes", "")
            entry["notes"] = " ".join([original, *notes]).strip()
            readme = path.parent / "README.md"
            text = readme.read_text().split("## Adaptations")[0]
            readme.write_text(text + "## Adaptations\n\n" + entry["notes"] + "\n")
    (ROOT / "catalog.json").write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n")
    print(f"Adapted {changed} projects.")


if __name__ == "__main__":
    main()
