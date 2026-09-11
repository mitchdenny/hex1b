#!/usr/bin/env python3
"""Fetch only pinned VHS tape text, license, and oracle extraction inputs."""

import concurrent.futures
import hashlib
import json
from pathlib import Path
import urllib.request

REF = "c073383b5de0b1f57bf514113029c306bc986539"
ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "tests/Hex1b.Tests/TestData/Tape"
WORK = Path(__file__).resolve().parent / ".work"


def get(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Hex1b-TapeConformance"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def main():
    tree = json.loads(get(f"https://api.github.com/repos/charmbracelet/vhs/git/trees/{REF}?recursive=1"))
    if tree.get("truncated"):
        raise RuntimeError("Upstream tree is truncated")
    paths = sorted(item["path"] for item in tree["tree"] if item["path"].endswith(".tape"))
    if len(paths) != 106:
        raise RuntimeError(f"Pinned inventory changed: {len(paths)} tapes")
    inputs = ["LICENSE", "go.mod", "lexer/lexer.go", "lexer/lexer_test.go",
              "parser/parser.go", "parser/parser_test.go", "token/token.go", "token/token_test.go"]
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        contents = dict(zip(paths + inputs, pool.map(
            lambda path: get(f"https://raw.githubusercontent.com/charmbracelet/vhs/{REF}/{path}"),
            paths + inputs)))
    records = []
    for path, content in contents.items():
        destination = DATA / "upstream" / path if path.endswith(".tape") or path == "LICENSE" else WORK / "upstream" / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
        records.append({"path": path, "sha256": hashlib.sha256(content).hexdigest(),
                        "bytes": len(content), "ref": REF})
    DATA.mkdir(parents=True, exist_ok=True)
    (DATA / "provenance.json").write_text(json.dumps({
        "repository": "https://github.com/charmbracelet/vhs",
        "ref": REF, "license": "MIT", "files": records,
    }, indent=2) + "\n")
    print(f"Fetched {len(paths)} tapes ({sum(len(contents[p]) for p in paths)} bytes), license, and oracle source inputs")


if __name__ == "__main__":
    main()
