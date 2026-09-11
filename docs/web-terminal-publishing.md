# Publishing `@hex1b/web-terminal`

`@hex1b/web-terminal` is the experimental browser client for Hex1b's
[HWT1 protocol](web-terminal-protocol.md). Use it with the **matching Hex1b NuGet
version from the same build**. It is a paired client, not an independently
versioned protocol implementation: there is no promise of wire compatibility
between arbitrary browser-client and server versions.

The package name is **`@hex1b/web-terminal`**. Main and release builds publish
to npmjs; PR builds provide a downloadable tarball, not an npm registry
publication. Its source repository remains `mitchdenny/hex1b`, and its
`package.json` contains:

```json
{
  "name": "@hex1b/web-terminal",
  "repository": {
    "type": "git",
    "url": "git+https://github.com/mitchdenny/hex1b.git"
  }
}
```

Do not add a fixed `publishConfig.registry` or `publishConfig.tag`: the workflow
chooses the destination and channel explicitly.

## Versions and channels

[Deploy](../.github/workflows/build-deploy.yml) computes a version once using the
existing [version action](../.github/actions/version/action.yml). The npm build
consumes **exactly `needs.version.outputs.version`**, just like the NuGet builds.
It does not derive a separate npm version from tags, commits, or `package.json`.

| Build | Distribution | Version from the common version job | npm dist-tag |
| --- | --- | --- | --- |
| PR | `npm-web-terminal` workflow artifact | `BASE-pr.NUM.RUN.ATTEMPT.SHA` | None |
| `main` | `https://registry.npmjs.org` | `BASE-alpha.RUN.ATTEMPT.SHA` | `alpha` |
| `release/X.Y` preview | `https://registry.npmjs.org` | `BASE-beta.RUN.ATTEMPT.SHA` | `beta` |
| Manual dispatch on `release/X.Y` with `release=true` | `https://registry.npmjs.org` | `BASE` | `latest` |

`BASE`, the PR number, run number, attempt, and short SHA are all supplied by the
existing version action. Prereleases never move `latest`. Fork and Dependabot
PRs still build, test, and upload the npm artifact, but **do not run the
credential-bearing NuGet publish job**. Same-repository PRs continue publishing
NuGet previews to GitHub Packages. Publishing jobs run only in the canonical
repository.
If a short SHA is entirely numeric and starts with zero, the shared action
prefixes that identifier with `g` to satisfy npm's SemVer rules. This
normalization applies to both npm and NuGet, not just one distribution.

A stable release tags the exact commit built from `release/X.Y`, not the
current tip of `main`. Publishing uses the `production` environment, so its
required-reviewer approval also applies to beta packages.

The dedicated `build-web-terminal` job uses Node.js 24 to install dependencies,
stamp both package manifests using
`npm version "$PACKAGE_VERSION" --no-git-tag-version --allow-same-version --ignore-scripts`,
build, test, and pack. It also restores `samples/WebTerminalDemo` and uses the
sample's build command to compile the library once, check the playground's
TypeScript consumer, and copy its browser assets. It then tests and packs that
same library build, uploading the tested `.tgz` as `npm-web-terminal`.
The .NET test/package jobs depend on that job. Publishing downloads this artifact
for npmjs and uses `npm publish <tarball> --ignore-scripts`; it does not install development
dependencies, rebuild, or run package lifecycle scripts with publishing credentials.
No npm version-stamping commit or git tag is created.

## One-time manual npmjs bootstrap: `0.1.0`

First, ensure your **npmjs** account has permission to publish public packages in
the npm organization `hex1b`, and configure interactive login and two-factor
authentication. This npm organization is independent of any GitHub organization.

Leave the repository Actions variable `NPM_PUBLISH_ENABLED` unset or set to
`false`. This disables only automated **npmjs** publication; build artifacts
and NuGet publishing remain available.

From the repository root, using Node.js 24 and npm **11.5.1 or newer**:

```bash
cd src/web-terminal
npm ci --registry=https://registry.npmjs.org
npm version 0.1.0 --no-git-tag-version --allow-same-version --ignore-scripts
npm run build
npm test
mkdir -p ../../artifacts/npm-bootstrap
npm pack --ignore-scripts --pack-destination ../../artifacts/npm-bootstrap --registry=https://registry.npmjs.org

# Inspect the exact tarball you will publish.
tar -tzf ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz
tar -xOf ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz package/package.json
npm publish ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz --dry-run --ignore-scripts --access public --tag latest --registry=https://registry.npmjs.org
```

Verify that the package contains `dist/index.js`, TypeScript declarations,
relative worker/modules/font assets, README, and MIT license, and does not
contain credentials, development dependencies, or unrelated sample files.
`prepack` also supports ordinary `npm pack`; the commands above skip lifecycle
scripts because you already built and tested the contents.

Then, **only when ready to publish**, authenticate interactively and publish that
same reviewed tarball:

```bash
npm login --scope=@hex1b --registry=https://registry.npmjs.org
npm whoami --registry=https://registry.npmjs.org
npm publish ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz --ignore-scripts --access public --tag latest --registry=https://registry.npmjs.org
npm view @hex1b/web-terminal@0.1.0 version --registry=https://registry.npmjs.org
```

Complete npm's browser/2FA prompts. Do not put an OTP or login credential into
the repository or a CI secret. Check any existing local `@hex1b:registry` mapping:
it must point to npmjs for this bootstrap.

This initial `0.1.0` publication creates the npm package so you can configure its
trusted publisher. It is **independent of the common CI version stream** and
does not imply compatibility with a historical Hex1b NuGet `0.1.0`; use its
corresponding source build until paired CI packages are available.
**Do not create a `v0.1.0` release git tag for this bootstrap.** Release tags are
inputs to NuGet's version action and would affect the shared version stream.
Do not reset subsequent CI versions to `0.1.0`.

## Enable npm trusted publishing

After the bootstrap, open `@hex1b/web-terminal` on npmjs → **Settings** →
**Trusted publishing / Trusted Publisher**, select **GitHub Actions**, and enter:

| npm setting | Exact value |
| --- | --- |
| Organization or user | `mitchdenny` |
| Repository | `hex1b` |
| Workflow filename | `build-deploy.yml` |
| Environment name | `production` |

Use only the workflow filename, not `.github/workflows/build-deploy.yml`.
If the settings form offers allowed actions, allow direct **`npm publish`**;
this workflow does not use staged publishing.
The organization/user here identifies the **GitHub source repository owner**,
not the npm organization `hex1b`. Keep `repository.url` exactly as shown above.

The workflow uses a GitHub-hosted Ubuntu runner, Node.js 24, and
`id-token: write` in the `production` environment. It verifies npm is at least
11.5.1 before publishing. npm exchanges the OIDC identity for short-lived
credentials; **no npmjs access-token secret or `NODE_AUTH_TOKEN` is configured**.
Trusted publishing automatically supplies provenance for eligible public
repositories/packages.

Configure the GitHub `production` environment's review and branch protections
as appropriate, ensuring the intended `main` and `release/X.Y` publications are
allowed. Then set the repository Actions variable **`NPM_PUBLISH_ENABLED=true`**
to activate npmjs publishing. This guide and workflow do not configure accounts,
secrets, variables, or trusted publishers for you.

After verifying trusted publication works, npm recommends **Require two-factor
authentication and disallow tokens** in the package's publishing-access settings.
This does not disable trusted publishing.

See npm's [trusted-publisher documentation](https://docs.npmjs.com/trusted-publishers/)
for current requirements and troubleshooting.

## PR preview artifacts

PR builds do not publish the npm package to GitHub Packages or npmjs. They
still build, test, and pack `@hex1b/web-terminal` with the shared PR version,
then upload the tarball as the **`npm-web-terminal`** workflow artifact.
There is no npm preview dist-tag or npm publishing-token prerequisite.

Download that artifact from the PR's Deploy workflow run in GitHub Actions,
or use the GitHub CLI. Replace `RUN_ID` and `VERSION` below with the values
from that run:

```bash
gh run download RUN_ID --repo mitchdenny/hex1b --name npm-web-terminal --dir artifacts/npm
npm install ./artifacts/npm/hex1b-web-terminal-VERSION.tgz --save-exact
```

Authenticate the GitHub CLI with `gh auth login` if needed to download workflow
artifacts. Installing the downloaded tarball does not require a GitHub Packages
registry login. Preserve the tarball if you need it beyond the workflow's
artifact retention period.

Use the browser tarball with the matching Hex1b NuGet packages from the same
run. Same-repository PRs continue publishing those NuGet packages to GitHub
Packages, and the PR comment contains their feed/authentication instructions.
Fork and Dependabot PRs do not publish NuGet packages.

## Failure handling and retries

The workflow serializes publishing per branch. The existing stale-beta check
applies to **both** npm and NuGet: if the final `vBASE` tag already exists, neither
publishes that beta.

For npmjs, the helper first queries the exact package version in the registry.
An already published version is skipped; only an npm `E404`/HTTP 404
means absent. Authentication, authorization, network, rate-limit, malformed
responses, and server failures stop the job rather than trigger a blind publish.

For main and release builds, enabled npm publication runs **before NuGet**.
If npm fails, NuGet has not been pushed by that job; if NuGet subsequently fails,
retrying the failed publish job reuses the artifact, skips an existing npm
version, and uses NuGet's duplicate-safe push.
Existing npm versions are not republished or retagged on retry. Review any
partially completed stable GitHub release/baseline dispatch separately.

Prefer rerunning **failed jobs**, retaining the successful version/build job
outputs and artifacts. Rerunning the whole workflow can compute a different
version because attempt numbers and release tags are inputs to the version
action. npm does not permit reusing a published name/version, even after
unpublishing.

Further npm references:
[publishing scoped public packages](https://docs.npmjs.com/creating-and-publishing-scoped-public-packages/),
[`npm publish`, dry runs, and version immutability](https://docs.npmjs.com/cli/v11/commands/npm-publish/),
and [dist-tags](https://docs.npmjs.com/adding-dist-tags-to-packages/).
