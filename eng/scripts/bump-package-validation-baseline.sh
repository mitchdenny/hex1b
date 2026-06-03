#!/usr/bin/env bash
# Rewrites the bot-managed <PackageValidationBaselineVersion> block in
# src/Hex1b/Hex1b.csproj on $BRANCH and opens (or updates) a PR that bumps it
# to $VERSION. Idempotent: if the file on $BRANCH already pins to $VERSION the
# script exits 0 without touching git or the PR.
#
# Inputs:
#   $1 BRANCH  — the base branch to PR into (e.g. main, release/1.0)
#   $2 VERSION — the stable X.Y.Z to set as the baseline
#
# Requires: gh CLI authenticated as a user/bot with PR-write on the repo.

set -euo pipefail

BRANCH="${1:?branch required}"
VERSION="${2:?version required}"

CSPROJ="src/Hex1b/Hex1b.csproj"
BEGIN_MARKER="<!-- BEGIN: bot-managed baseline -->"
END_MARKER="<!-- END: bot-managed baseline -->"

if ! git ls-remote --exit-code --heads origin "$BRANCH" >/dev/null 2>&1; then
  echo "::notice::Branch '$BRANCH' does not exist on origin; skipping."
  exit 0
fi

# Branch name for the PR: stable per (base-branch, version) so re-runs update
# the same PR instead of fanning out duplicates. Slashes in BRANCH are
# flattened so the bot branch is always a single path segment.
BRANCH_SLUG="${BRANCH//\//-}"
PR_BRANCH="bot/baseline-bump/${BRANCH_SLUG}-to-${VERSION}"

WORKTREE="$(mktemp -d)"
trap 'rm -rf "$WORKTREE"' EXIT

git worktree add --detach "$WORKTREE" "origin/$BRANCH"
pushd "$WORKTREE" >/dev/null

if [[ ! -f "$CSPROJ" ]]; then
  echo "::notice::$CSPROJ not present on $BRANCH; skipping."
  popd >/dev/null
  exit 0
fi

if ! grep -qF "$BEGIN_MARKER" "$CSPROJ" || ! grep -qF "$END_MARKER" "$CSPROJ"; then
  echo "::error::bot-managed baseline block not found in $CSPROJ on $BRANCH."
  exit 1
fi

# Detect the current pinned version inside the block (if any) to short-circuit
# noop runs.
CURRENT="$(awk -v b="$BEGIN_MARKER" -v e="$END_MARKER" '
  $0 ~ b {in_block=1; next}
  $0 ~ e {in_block=0}
  in_block {print}
' "$CSPROJ" \
  | grep -oE '<PackageValidationBaselineVersion>[^<]+</PackageValidationBaselineVersion>' \
  | head -n1 \
  | sed -E 's@</?PackageValidationBaselineVersion>@@g' || true)"

if [[ "$CURRENT" == "$VERSION" ]]; then
  echo "Baseline on $BRANCH already pinned to $VERSION; nothing to do."
  popd >/dev/null
  exit 0
fi

# Rewrite the block. We replace every line strictly between the markers with
# a single active baseline element, preserving the leading indentation of the
# BEGIN marker so the diff stays minimal.
INDENT="$(grep -n -F "$BEGIN_MARKER" "$CSPROJ" | head -n1 | cut -d: -f1)"
INDENT_PREFIX="$(awk -v ln="$INDENT" 'NR==ln {match($0, /^[[:space:]]*/); print substr($0, 1, RLENGTH); exit}' "$CSPROJ")"

python3 - "$CSPROJ" "$BEGIN_MARKER" "$END_MARKER" "$INDENT_PREFIX" "$VERSION" <<'PY'
import sys, pathlib
path, begin, end, indent, version = sys.argv[1:]
text = pathlib.Path(path).read_text()
lines = text.splitlines(keepends=True)
out = []
inside = False
replaced = False
for line in lines:
    if not inside and begin in line:
        out.append(line)
        inside = True
        continue
    if inside and end in line:
        if not replaced:
            out.append(f"{indent}<PackageValidationBaselineVersion>{version}</PackageValidationBaselineVersion>\n")
            replaced = True
        out.append(line)
        inside = False
        continue
    if inside:
        # Drop existing block contents.
        continue
    out.append(line)
pathlib.Path(path).write_text("".join(out))
PY

if git diff --quiet -- "$CSPROJ"; then
  echo "Rewrite produced no diff on $BRANCH; nothing to commit."
  popd >/dev/null
  exit 0
fi

git checkout -B "$PR_BRANCH"
git add "$CSPROJ"
git commit -m "ci: bump PackageValidationBaselineVersion to $VERSION on $BRANCH"
git push --force-with-lease origin "$PR_BRANCH"

PR_TITLE="Bump package validation baseline to $VERSION on $BRANCH"
PR_BODY=$(cat <<EOF
Automated bump of \`<PackageValidationBaselineVersion>\` in \`src/Hex1b/Hex1b.csproj\` to **$VERSION**, triggered by the publication of a stable release on \`$BRANCH\`.

* Previous baseline: \`${CURRENT:-<none>}\`
* New baseline: \`$VERSION\`

Merging this PR makes the next build on \`$BRANCH\` validate the public API against the \`Hex1b\` $VERSION package on nuget.org. See \`.github/workflows/baseline-bump.yml\` for the bump rules.
EOF
)

if EXISTING_PR=$(gh pr view "$PR_BRANCH" --json number --jq .number 2>/dev/null); then
  echo "PR #$EXISTING_PR already exists for $PR_BRANCH; force-push above updated it."
else
  gh pr create \
    --base "$BRANCH" \
    --head "$PR_BRANCH" \
    --title "$PR_TITLE" \
    --body "$PR_BODY" \
    --label baseline-bump || gh pr create \
      --base "$BRANCH" \
      --head "$PR_BRANCH" \
      --title "$PR_TITLE" \
      --body "$PR_BODY"
fi

popd >/dev/null
