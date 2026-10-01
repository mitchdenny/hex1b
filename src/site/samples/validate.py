#!/usr/bin/env python3
"""Build independent sample copies with repository MSBuild props/targets disabled."""
import concurrent.futures
import json
import pathlib
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent
OUTPUT = ROOT / ".validation"
OUTPUT.mkdir(exist_ok=True)
catalog = json.loads((ROOT / "catalog.json").read_text())
selected = set(sys.argv[1:])


def build(entry):
    sample_id = entry["id"]
    target = OUTPUT / sample_id
    target.mkdir(exist_ok=True)
    source = ROOT / sample_id
    for path in source.rglob("*"):
        if path.is_file() and not any(part in ("bin", "obj", ".git") for part in path.relative_to(source).parts):
            dest = target / path.relative_to(source)
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, dest)
    command = [
        "dotnet", "build", "Sample.csproj", "--nologo", "-v:q",
        "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false",
    ]
    result = subprocess.run(command, cwd=target, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (OUTPUT / f"{sample_id}.log").write_text(result.stdout)
    print(f"{'PASS' if result.returncode == 0 else 'FAIL'} {sample_id}", flush=True)
    return {"id": sample_id, "success": result.returncode == 0}


with concurrent.futures.ThreadPoolExecutor(max_workers=3) as executor:
    results = list(executor.map(build, [entry for entry in catalog if not selected or entry["id"] in selected]))
(OUTPUT / "results.json").write_text(json.dumps(results, indent=2) + "\n")
failed = [result["id"] for result in results if not result["success"]]
print(f"Built {len(results)} independent samples; {len(failed)} failures.")
sys.exit(1 if failed else 0)
