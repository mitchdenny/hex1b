# New Hex1b site

This is a vanilla Vite placeholder proving the GitHub Pages build/publication
path. It does not replace `src/content`, `src/Hex1b.Website`, or `hex1b.dev`.
The production base is `/hex1b/` for https://mitchdenny.github.io/hex1b/.

Use .NET 10, Node 24, Aspire CLI 13.6.1, and a running Docker or Podman
runtime. From the repository root:

```sh
# Build and check the static bundle locally; no credentials or remote effects.
aspire do build-github-pages-new-site --apphost ./apphost.cs --non-interactive

# Preview the extracted production build at http://localhost:4173/hex1b/.
npm ci --prefix src/site
npm run preview --prefix src/site -- --outDir ../../.aspire/github-pages/new-site/dist
```

The root AppHost declares the Vite app separately, just as the Aspire React
starter does:

```csharp
var newSite = builder.AddViteApp("new-site-vite", "./src/site").WithNpm(installCommand: "ci");
builder.AddGitHubPages("new-site", newSite, "mitchdenny/hex1b");
```

`new-site-vite` runs the development server during local development.
During publication, Aspire's standard `build-new-site-vite` step builds its
build-only container image. The Pages step copies the resource's declared
container files (`/app/dist` for Vite) from a stopped container, validates them,
and saves them in `.aspire/github-pages/new-site/dist`. Only those extracted
files are published. The container is never started; it and any anonymous
volumes are removed even on failure or cancellation. No image is pushed to a
registry, and no separate host-side Vite build is used.

## Publication

`.github/workflows/github-pages.yml` validates changes and runs:

```sh
aspire do publish-github-pages-new-site --apphost ./apphost.cs --non-interactive
```

This is a **live publication command**, not just artifact generation. Its only
build dependency is the new Vite site's container image and its native Aspire
build/runtime prerequisites. It does not build the old documentation, unrelated
containers, or library packages, and it is not part of the default Azure deploy.

The command must run inside a GitHub Actions JavaScript action context. The
workflow uses `actions/github-script` so its child Aspire process inherits the
artifact runtime credentials. That action obtains and masks the OIDC token
before passing it through the child's environment, not command-line arguments.
The repository-local Node helper packages the bundle in `artifact.tar`, uploads
it with `@actions/artifact` under a run/attempt-specific name, and creates and
polls the GitHub Pages deployment. Reruns do not overwrite immutable artifacts. It
requires `contents: read`, `pages: write`, `id-token: write`, and the
`github-pages` environment. No PAT or Azure credentials are used.

Before the first deployment, a maintainer must explicitly approve and enable
**Settings > Pages > Source: GitHub Actions**, without a custom domain, and
configure the `github-pages` environment's deployment rules to allow the
intended main and same-repository PR refs. The workflow does not enable Pages
or modify environment protection itself; it fails if Pages is not configured.
Do not weaken existing protection rules without approval.
After those approvals, set the repository Actions variable
`HEX1B_PAGES_PUBLICATION_ENABLED` to `true` to activate the publication job.
Until then, CI runs validation only, so opening a PR cannot activate the new
environment or attempt the first publication. The named Aspire command itself
is still a live command when run with the required Actions credentials.

During initial bring-up, main pushes and same-repository PR updates affecting
these files automatically publish. Manual dispatch can also publish the selected
branch. **Every run replaces the same public URL**; PRs are not isolated previews,
and the deployed version can be a PR version. Fork and Dependabot PRs are
build-only because they do not have publication credentials. Never use
`pull_request_target` to publish checked-out PR code. Publications are serialized
without canceling an in-progress deployment. Once bring-up is complete, remove
the temporary PR publication policy from the workflow.

## Focused checks

```sh
dotnet test tests/Aspire.Hosting.GitHubPages.Tests/Aspire.Hosting.GitHubPages.Tests.csproj
npm ci --prefix .github/scripts/github-pages
npm test --prefix .github/scripts/github-pages
aspire do publish-github-pages-new-site --apphost ./apphost.cs --list-steps --non-interactive
```

The last command lists steps without running publication. The selected graph
should contain `publish-github-pages-new-site`, its extraction/validation
dependency `build-github-pages-new-site`, and Aspire's native
`build-new-site-vite` container build with its prerequisites. It should not
contain Azure preparation or unrelated resource builds.
