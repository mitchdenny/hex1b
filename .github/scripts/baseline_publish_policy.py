#!/usr/bin/env python3
"""Identify automated baseline maintenance runs that must not publish packages."""

import json
import os
import re
import subprocess
from pathlib import Path


PROJECT = "src/Hex1b/Hex1b.csproj"
BEGIN = b"<!-- BEGIN: bot-managed baseline -->"
END = b"<!-- END: bot-managed baseline -->"


def command(*args):
    return subprocess.run(args, check=True, stdout=subprocess.PIPE).stdout


def revision(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{40}", value):
        raise ValueError(f"Invalid commit SHA: {value!r}")
    return value


def is_automated_baseline_pr(pr, repository, target):
    head = pr.get("head") or {}
    base = pr.get("base") or {}
    slug = target.replace("/", "-")
    return (
        re.fullmatch(r"main|release/[0-9]+\.[0-9]+", target) is not None
        and (pr.get("user") or {}).get("login") == "github-actions[bot]"
        and (head.get("repo") or {}).get("full_name") == repository
        and (base.get("repo") or {}).get("full_name") == repository
        and base.get("ref") == target
        and re.fullmatch(
            rf"bot/baseline-bump/{re.escape(slug)}(?:-to-[0-9]+\.[0-9]+\.[0-9]+)?",
            head.get("ref", ""),
        ) is not None
    )


def baseline_parts(content):
    if content.count(BEGIN) != 1 or content.count(END) != 1:
        return None
    prefix, _, remainder = content.partition(BEGIN)
    if END not in remainder:
        return None
    body, _, suffix = remainder.partition(END)
    return prefix, body, suffix


def baseline_only_change(before, after):
    changes = command(
        "git", "diff", "--no-ext-diff", "--no-renames", "--name-status", "-z",
        before, after, "--",
    )
    if changes != f"M\0{PROJECT}\0".encode():
        return False

    # A content-only exception must not hide a mode or file-type change.
    for sha in (before, after):
        entry = command("git", "ls-tree", sha, "--", PROJECT)
        if not entry.startswith(b"100644 blob "):
            return False

    old = baseline_parts(command("git", "show", f"{before}:{PROJECT}"))
    new = baseline_parts(command("git", "show", f"{after}:{PROJECT}"))
    return (
        old is not None
        and new is not None
        and old[0] == new[0]
        and old[2] == new[2]
        and re.fullmatch(
            rb"\s*<PackageValidationBaselineVersion>[0-9]+\.[0-9]+\.[0-9]+"
            rb"</PackageValidationBaselineVersion>\s*",
            new[1],
        ) is not None
    )


def skip_publish(event_name, event, repository):
    if event_name == "pull_request":
        pr = event["pull_request"]
        target = pr["base"]["ref"]
        if not is_automated_baseline_pr(pr, repository, target):
            return False
        head = revision(pr["head"]["sha"])
        base = revision(pr["base"]["sha"])
        before = revision(command("git", "merge-base", base, head).decode().strip())
        return baseline_only_change(before, head)

    if event_name != "push":
        return False
    if event.get("created") or event.get("deleted") or event.get("forced"):
        return False
    before = revision(event["before"])
    after = revision(event["after"])
    if before == "0" * 40 or after == "0" * 40:
        return False
    ref = event["ref"]
    if not ref.startswith("refs/heads/"):
        return False
    target = ref.removeprefix("refs/heads/")
    if not baseline_only_change(before, after):
        return False

    # Query only baseline-only pushes. Metadata survives edited squash titles
    # and distinguishes the bot's merged PR from manually authored changes.
    pages = json.loads(command(
        "gh", "api", "--paginate", "--slurp",
        f"repos/{repository}/commits/{after}/pulls",
    ))
    return any(
        pr.get("merged_at")
        and pr.get("merge_commit_sha") == after
        and is_automated_baseline_pr(pr, repository, target)
        for page in pages
        for pr in page
    )


def main():
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
    skip = bool(skip_publish(
        os.environ["GITHUB_EVENT_NAME"], event, os.environ["GITHUB_REPOSITORY"],
    ))
    with Path(os.environ["GITHUB_OUTPUT"]).open("a") as output:
        output.write(f"skip_publish={str(skip).lower()}\n")
    print(
        "Automated baseline-only change: skipping package publishing."
        if skip else "Normal publishing policy applies."
    )


if __name__ == "__main__":
    main()
