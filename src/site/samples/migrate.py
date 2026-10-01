#!/usr/bin/env python3
"""Inventory legacy examples and bootstrap NEW standalone projects; never overwrite programs."""
import argparse
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent
REPO = ROOT.parents[2]
CONTENT = REPO / "src/content"
CONST = re.compile(r"const\s+(\w+)\s*=\s*`((?:\\.|[^`])*)`", re.S)
FENCE = re.compile(r"^```(?:csharp|cs)\s*\n(.*?)^```", re.M | re.S)
ATTR = re.compile(r"""([:\w-]+)=["']([^"']*)["']""")
FENCE_APPS = {
    ("guide/cli.md", 1), ("guide/tui.md", 1),
    ("guide/widgets/button.md", 3), ("guide/widgets/effectpanel.md", 1),
    ("guide/widgets/qrcode.md", 3), ("guide/widgets/notifications.md", 14),
    ("guide/terminal-emulator.md", 1),
}
EXTERNAL_FENCES = {
    ("guide/terminal-emulator.md", 2): "Headless build command demonstration; requires a buildable working directory and has no visible terminal presentation.",
    ("guide/terminal-emulator.md", 3): "Docker provisioning example requires Docker and pulls a container image; not an automatically recorded TUI.",
    ("guide/terminal-emulator.md", 4): "Complete Docker configuration example requires an explicitly configured host-data mount and container working directory.",
    ("guide/terminal-emulator.md", 5): "Complete Docker build example requires the externally supplied test-env/Dockerfile.",
    ("guide/using-the-emulator.md", 1): "ASP.NET hosting integration, not a console TUI; requires a web host and separate browser frontend.",
    ("guide/using-the-emulator.md", 2): "ASP.NET hosting integration requires external SSH/Docker workloads and a separate browser frontend.",
}
SOURCE_ONLY = {
    "terminal-emulator-fence-2": {
        "title": "Terminal Emulator — Headless Build Command",
        "prerequisites": "Requires the .NET 10 SDK and a project or solution in the current working directory for the child dotnet build command. The workload uses a headless presentation, so there is no meaningful terminal playback. Running it from this sample directory builds this sample; running with --project from another project directory builds that directory instead.",
    },
    "terminal-emulator-fence-3": {
        "title": "Terminal Emulator — Default Docker Container",
        "prerequisites": "Requires the Docker CLI, a running Docker daemon configured for Linux containers, permission to use it, and access to mcr.microsoft.com/dotnet/sdk:10.0 (or a previously pulled image). Running this example starts a container with an interactive shell; building this project never starts Docker.",
    },
    "terminal-emulator-fence-4": {
        "title": "Terminal Emulator — Configured Docker Container",
        "prerequisites": "Requires the Docker CLI and a running Linux-container daemon, plus ubuntu:24.04 locally or access to pull it. Before running, replace /host/data with an existing absolute host directory that you explicitly want mounted read-only at /container/data, and review the /app container working directory. No host data or services are provisioned by the build.",
    },
    "terminal-emulator-fence-5": {
        "title": "Terminal Emulator — Dockerfile Workload",
        "prerequisites": "Requires the Docker CLI, a running Linux-container daemon, and a Dockerfile at ./test-env/Dockerfile relative to the working directory. Supply your own Dockerfile and build-context files; the SDK_VERSION=10.0 argument is passed to that Dockerfile. Running this application builds an image and starts a container; dotnet build only compiles the application.",
    },
    "using-the-emulator-fence-1": {
        "title": "Using the Emulator — Browser Terminal Host",
        "prerequisites": "Requires the .NET 10 SDK/ASP.NET Core runtime, bash on PATH, and a separately supplied browser terminal client compatible with WebSocketPresentationAdapter at /ws/terminal. Put client assets in wwwroot if serving them from this app. This unauthenticated demonstration executes a local shell: bind only to loopback and never expose it publicly. The site build compiles but never starts the host.",
        "sdk": "Microsoft.NET.Sdk.Web",
    },
    "using-the-emulator-fence-2": {
        "title": "Using the Emulator — Browser Terminal Workloads",
        "prerequisites": "Requires the .NET 10 SDK/ASP.NET Core runtime and a separately supplied compatible browser terminal client. The /ws/starwars endpoint needs ssh on PATH and access to starwarstel.net; the /ws/cmatrix, /ws/pipes and /ws/asciiquarium endpoints need the Docker CLI, a running daemon and the images named in Program.cs. Client assets may be placed in wwwroot. Bind this unauthenticated command-execution demo only to loopback; do not expose it publicly. Services and images are never started or provisioned during site builds.",
        "sdk": "Microsoft.NET.Sdk.Web",
    },
    "figlet-custom-font": {
        "title": "FigletText — Custom Font File",
        "prerequisites": "Requires a valid FIGlet .flf font at fonts/colossal.flf relative to the working directory, or a changed path in Program.cs. Supply a font you are licensed to use; this sample does not include or download that external asset. The font is loaded only when you explicitly run the application, never during compilation.",
    },
}
PARTIAL_IMPORTS = {
    "figlet-custom-font.cs": "Requires the unprovided fonts/colossal.flf asset.",
    "markdown-code-block.cs": "Widget expression containing example code in a Markdown string, not an application.",
    "terminal-fallback.cs": "Widget expression with undefined ctx and RestartTerminal; intentionally demonstrates fallback configuration.",
}
PROJECT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hex1b" Version="0.166.0" />
  </ItemGroup>
</Project>
"""

BACKENDS = {
    "layout": "LayoutExample",
    "theming": "ThemingExample",
    "navigator": "NavigatorExample",
    "terminal-basic": "TerminalBasicExample",
}


def backend_program(sample_id):
    name = BACKENDS[sample_id]
    code = (REPO / "src/Hex1b.Website/Examples" / f"{name}.cs").read_text()
    code = code.replace("using Microsoft.Extensions.Logging;\n", "")
    code = code.replace("namespace Hex1b.Website.Examples;\n", "")
    code = re.sub(r"public class " + name + r"\(ILogger<" + name + r"> logger\) : \w+", f"internal class {name}", code)
    code = re.sub(r"    private readonly ILogger<.*?> _logger = logger;\n", "", code)
    code = re.sub(r"\s*_logger\.Log\w+\([\s\S]*?\);", "", code)
    code = code.replace("public override ", "public ")
    code = code.replace("    [ThreadStatic]\n", "")
    code = code.replace("private static ThemingState? _currentSession", "private ThemingState? _currentSession")
    code = code.replace("// Thread-local session state for each websocket connection", "// Theme state shared by this application's widget builder and theme provider.")
    if sample_id == "terminal-basic":
        code = code.replace("IHex1bAppTerminalWorkloadAdapter workloadAdapter, ", "")
        code = code.replace("workloadAdapter.Width - 4, workloadAdapter.Height - 8", "96, 22")
        code = code.replace("                WorkloadAdapter = workloadAdapter,\n", "")
        main = f"await new {name}().RunAsync(CancellationToken.None);\n\n"
    else:
        main = f"var sample = new {name}();\nvar builder = sample.CreateWidgetBuilder();\n"
        if sample_id == "theming":
            main += "await using var app = new Hex1bApp(_ => builder(), new Hex1bAppOptions\n{\n    ThemeProvider = sample.CreateThemeProvider()\n});\n"
        else:
            main += "await using var app = new Hex1bApp(_ => builder());\n"
        main += "await app.RunAsync();\n\n"
    class_start = code.index("/// <summary>")
    return code[:class_start] + main + code[class_start:]


def slug(value):
    return re.sub(r"[^a-z0-9]+", "-", re.sub(r"([a-z])([A-Z])", r"\1-\2", value).lower()).strip("-")


def cook(value):
    return re.sub(r"\\([\\`$])", r"\1", value)


def inventory():
    records = []
    for path in sorted(CONTENT.rglob("*.md")):
        page = path.relative_to(CONTENT).as_posix()
        if page.startswith("reference/") and page not in {"reference/index.md", "reference/cli.md"}:
            continue
        text = path.read_text()
        constants = {m[1]: cook(m[2]) for m in CONST.finditer(text)}
        page_title = re.search(r"^# (.+)$", text, re.M)
        title = page_title[1] if page_title else path.stem
        for match in re.finditer(r"<(CodeBlock|TerminalDemo)\b([\s\S]*?)/>", text):
            attrs = dict(ATTR.findall(match[2]))
            key = attrs.get(":code", "")
            example = attrs.get("example")
            code = constants.get(key)
            record = {
                "id": example or slug(f"{path.stem}-{key.removesuffix('Code')}"),
                "title": attrs.get("exampleTitle", attrs.get("title", f"{title} — {key.removesuffix('Code')}")),
                "page": page, "codeKey": key, "kind": match[1],
                "status": "standalone", "code": code,
            }
            if example:
                record["example"] = example
            if code is None:
                record["reason"] = "Extract complete backend widget implementation into a normal console app."
            records.append(record)
        for match in re.finditer(r"""import\s+(\w+)\s+from\s+["'](.*?\.cs)\?raw["']""", text):
            code = (path.parent / match[2]).read_text()
            name = pathlib.Path(match[2]).name
            complete = "await terminal.RunAsync();" in code or "await app.RunAsync();" in code
            status = "standalone" if complete and name not in PARTIAL_IMPORTS else "inline"
            if name == "figlet-custom-font.cs":
                status = "source-only"
            records.append({
                "id": slug(f"{path.stem}-{match[1].removesuffix('Snippet')}"),
                "title": f"{title} — {match[1].removesuffix('Snippet')}",
                "page": page, "codeKey": match[1], "kind": "raw-import",
                "source": (path.parent / match[2]).relative_to(CONTENT).as_posix(),
                "status": status, "code": code,
                "reason": PARTIAL_IMPORTS.get(name, "" if status == "standalone" else "Partial API/widget snippet; remains inline."),
            })
        for index, match in enumerate(FENCE.finditer(text), 1):
            identity = (page, index)
            status = "standalone" if identity in FENCE_APPS else "inline"
            reason = EXTERNAL_FENCES.get(identity, "" if status == "standalone" else "Partial API, configuration, test, or conceptual snippet; remains inline.")
            if identity in EXTERNAL_FENCES:
                status = "source-only"
            elif identity not in FENCE_APPS and re.search(r"(?:await\s+\w+\.RunAsync\(|\bapp\.Run\()", match[1]):
                if not re.search(r"\[Test(?:Class|Method)\]|\[Theory\]|/\*", match[1]):
                    status = "needs-review"
                    reason = "Application entry point detected; explicitly classify this new fence before publishing."
            records.append({
                "id": slug(f"{path.stem}-fence-{index}"),
                "title": f"{title} — example {index}",
                "page": page, "codeKey": f"fence-{index}", "kind": "fence",
                "status": status, "code": match[1], "reason": reason,
            })
        for key, code in constants.items():
            if "RunAsync(" in code and not re.search(r""":code=["']""" + re.escape(key) + r"""["']""", text):
                records.append({
                    "id": slug(f"{path.stem}-{key}"), "title": f"{title} — {key}",
                    "page": page, "codeKey": key, "kind": "unused-constant",
                    "status": "not-displayed", "code": code,
                    "reason": "Legacy script declaration is never rendered in the page; not a displayed demo.",
                })
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-new", action="store_true", help="Create missing projects; preserve existing source and catalog edits.")
    args = parser.parse_args()
    records = inventory()
    old_catalog = {entry["id"]: entry for entry in json.loads((ROOT / "catalog.json").read_text())} if (ROOT / "catalog.json").exists() else {}
    catalog = []
    seen = set()
    for record in records:
        if record["status"] not in {"standalone", "source-only"}:
            continue
        sample_id = record["id"]
        if sample_id in seen:
            sample_id += "-" + slug(record["codeKey"])
            record["id"] = sample_id
        seen.add(sample_id)
        entry = {key: record[key] for key in ("id", "title", "page", "codeKey", "example") if key in record}
        entry.update(project=f"samples/{sample_id}/Sample.csproj", columns=100, rows=30)
        source_only = SOURCE_ONLY.get(sample_id)
        project_text = PROJECT
        if source_only:
            entry.update(title=source_only["title"], sourceOnly=True,
                         prerequisites=source_only["prerequisites"], actions=[])
            sdk = source_only.get("sdk", "Microsoft.NET.Sdk")
            project_text = PROJECT.replace("Microsoft.NET.Sdk", sdk)
            if sdk == "Microsoft.NET.Sdk.Web":
                entry["notes"] = "Uses Microsoft.NET.Sdk.Web to supply ASP.NET Core framework references and implicit imports; the displayed program is copied unchanged. Hex1b remains pinned to 0.166.0."
            else:
                imports = ["using Hex1b;"]
                if sample_id == "figlet-custom-font":
                    imports.append("using Hex1b.Widgets;")
                record["code"] = "\n".join(imports) + "\n\n" + record["code"]
                entry["notes"] = "Added the namespace imports omitted by the surrounding guide; the application logic is unchanged."
        if sample_id in BACKENDS:
            entry["notes"] = (
                f"Complete logic from src/Hex1b.Website/Examples/{BACKENDS[sample_id]}.cs; "
                "removed ASP.NET/logger/base-class plumbing and used a normal console entry point."
            )
            if sample_id == "terminal-basic":
                entry["notes"] += " The displayed snippet omits RestartTerminal; the full backend implementation supplies restart, cleanup, and keyboard shortcuts. Embedded terminal dimensions default to 96×22."
            if sample_id == "navigator":
                entry["notes"] += " Preserved the documentation's navigator ID (the backend calls it navigation)."
            record["code"] = backend_program(sample_id)
        if record.get("reason"):
            entry.setdefault("notes", record["reason"])
        entry.update(old_catalog.get(sample_id, {}))
        catalog.append(entry)
        if args.write_new:
            directory = ROOT / sample_id
            directory.mkdir(exist_ok=True)
            prerequisites_section = ""
            if source_only:
                prerequisites_section = f"""## Prerequisites and source-only policy

{entry['prerequisites']}

This project is compiled and exported for source browsing and cloning only. Site builds and smoke tests must never execute it; there are intentionally no recording or playback controls.

Build safely without starting the application:

```sh
dotnet build
```
"""
            files = {
                "Sample.csproj": project_text, ".gitignore": "bin/\nobj/\n",
                "LICENSE": (REPO / "LICENSE").read_text(),
                "README.md": f"""# {entry['title']}

{"After satisfying the prerequisites below, explicitly run this source-only sample with .NET 10 SDK or later:" if source_only else "Run this standalone sample with .NET 10 SDK or later:"}

```sh
{"dotnet run -- --urls http://127.0.0.1:5000" if source_only and source_only.get("sdk") == "Microsoft.NET.Sdk.Web" else "dotnet run"}
```

The project uses the published **Hex1b 0.166.0** NuGet package. No checkout of
the Hex1b repository, local project references, or site server is required.
Press Ctrl+C to stop the application.

{prerequisites_section}
## Source

Extracted from `src/content/{record['page']}`, binding `{record['codeKey'] or 'TerminalDemo'}`.
{f"Imported source: `src/content/{record['source']}`." if record.get('source') else ''}
The original documentation is preserved unchanged; this directory owns the runnable source.
Recording and static Git export are handled separately by the site build pipeline.

## Adaptations

{entry.get('notes', 'No API adaptations; the displayed program is copied unchanged.')}
""",
            }
            if record["code"] is not None:
                files["Program.cs"] = record["code"].strip() + "\n"
            for name, content in files.items():
                target = directory / name
                if not target.exists():
                    target.write_text(content)
    if args.write_new:
        (ROOT / "catalog.json").write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n")
        (ROOT / "inventory.json").write_text(json.dumps([{k: v for k, v in r.items() if k != "code"} for r in records], indent=2, ensure_ascii=False) + "\n")
    counts = {}
    for record in records:
        counts[record["status"]] = counts.get(record["status"], 0) + 1
    print(json.dumps(counts))
    if counts.get("needs-review"):
        raise SystemExit("Unclassified application fences require review.")


if __name__ == "__main__":
    main()
