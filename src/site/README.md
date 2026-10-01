# Static documentation site

This is the first migration of the Hex1b documentation to a pure Vite, static
multi-page site. It retains the existing documentation structure and teal/navy
branding while integrating native HWT playback, Monaco and cloneable samples.
It does not change `src/content`, `src/Hex1b.Website`, the live deployment, or any
publication workflow. Pages remain marked `noindex` during the migration.
The new site runs separately; it is not yet registered in `apphost.cs`.

`content/` starts as a byte-for-byte copy of the authored Markdown and snippets.
Build-time adapters render its existing VitePress components without shipping
Vue, VitePress or a server backend. Complete demos live in standalone projects
under `samples/`; partial API snippets remain inline. API reference Markdown is
generated separately into `.generated/reference`, never into the legacy site.

## Build and view

From the repository root, with the .NET 10 SDK, Node.js 22.6+, npm and Git installed.
Recording the shell examples also requires `bash` on PATH:

```sh
npm ci --prefix src/web-terminal
npm ci --prefix src/site
npm run build --prefix src/site
npm run preview --prefix src/site
```

Open the local address printed by Vite. Guide and API Reference are pre-rendered
HTML; Samples contains the standalone projects. On a sample page, press **Play**
to start its recording.
**Pause** freezes the playback clock; **Restart** restores the first frame and
pauses. Playback retains the last captured frame instead of clearing the screen
when the recorded process exits.

The build:

1. Builds the native web terminal and copies its browser bundle (including its
   workers) and bundled Nerd Font into generated static assets.
2. Runs the existing API documentation generator with an isolated output path.
3. Generates the three referenced SVG previews missing from the legacy assets.
4. Builds the standalone sample catalog, then uses `driver/` to capture each
   project in a separate PTY under a `Hex1bTerminal`. The original Pixel Postcards
   proof of concept retains its dedicated graphics scenario. Source-only examples
   are compiled but never launched.
5. Validates each recording and packages the same project files into a static
   Git snapshot. Recordings and source manifests go under `public/recordings`.
6. Bundles the client with Vite, then emits an `index.html` for every authored,
   generated API, and sample route. A local search index is emitted alongside
   the pages. Internal asset/page links are checked before the build succeeds.

Serve **all of `dist/`** using an ordinary static HTTP server. There is no ASP.NET
process, WebSocket endpoint, npm service, or .NET runtime involved in playback.
The HTML references adjacent assets; it is not a single self-contained HTML file
and must be served over HTTP(S), not opened with `file://`. Relative asset paths
support hosting below a URL prefix, including a GitHub Pages project path.

`npm run dev --prefix src/site` performs a complete build and opens Vite preview.
This initial migration deliberately previews the real static output, without
an SPA fallback or HMR server. For faster iteration after the first complete build:

```sh
# Markdown, styling or client changes:
npm run build:pages --prefix src/site

# Rebuild selected samples, followed by updated pages:
npm run samples --prefix src/site -- button-basic button-counter
npm run build:pages --prefix src/site
```

Omit sample IDs for a complete sample rebuild; targeted builds require the initial
complete build's manifests. Refresh the preview after rebuilding. Run
`npm run reference --prefix src/site` after library API changes, and the complete
build when changing the terminal protocol/player.
Both search and page navigation work below a GitHub Pages project prefix.

Generated recordings, repositories and copied player assets are ignored by Git.

## Cloneable samples

**Clone sample** shows a `git clone` command for the current host and deployment
prefix, plus a copy button and local run commands (Bash or PowerShell). The clone
contains the exact files displayed in Monaco, including the project, README,
license and `.gitignore`. The sample pins the published Hex1b `0.166.0` NuGet
package; neither the Hex1b source tree nor the recording driver is needed to run it.

The build creates a genuine Git repository using Git plumbing, packs its objects,
and runs `git update-server-info`. Only `HEAD`, the `main` ref, discovery metadata,
and object packs/indexes are published. No hooks, local configuration, credentials
or parent repository history are exported. Client Git automatically falls back
from smart HTTP to the dumb HTTP transport: ordinary static GETs, with no CGI,
Git daemon, custom endpoint or authentication service.

Each export has one synthetic commit with fixed build-author metadata and a fixed
timestamp. URLs under `repositories/<sample>/<content-hash>.git` include a digest
of all published bytes, not just the commit, so different pack encodings cannot
collide in caches. Unchanged builds produce the same snapshot. These are
**read-only downloads**, not repositories with an evolving history: no push,
shallow clone, or updates from `git pull`. A new sample version gets a new URL.
Preserving old URLs across fresh deployments requires a future retention policy.

Serve the repository files verbatim, including extensionless `HEAD` and `info/refs`,
binary packs, and requests with query strings. Do not rewrite missing Git paths
to the site's HTML. `.nojekyll` is included for hosts that would otherwise apply
Jekyll filtering. GitHub Pages deployment is still separate work; the spike
exercises real clones against local static HTTP hosting at root and `/hex1b/`.

`npm run samples --prefix src/site` generates the catalog's recordings and
repositories. Sample scenarios initially capture the starting state, with
keyboard actions only where explicitly declared; feature demonstrations can be refined
independently of the source projects.

`npm run record --prefix src/site` generates the original Pixel Postcards proof
of concept's recording and repository.
`npm run repositories --prefix src/site` regenerates only that proof of concept's
repository from an existing recording manifest without running the sample again.

## Source browser

The source manifest backs a read-only virtual filesystem: nested paths become
folders in an Explorer sidebar. Click a file or use arrow keys and Enter to open
it. The sample's `Program.cs` is selected initially.

Monaco loads when the source browser approaches the viewport (or receives
keyboard focus), separately from terminal playback. Each file has its own model
and retains its selection, folding and scroll state. C# and XML syntax
highlighting, line numbers, selection/copy, folding and find run locally.
This is not an editable playground or a C# language-server/IntelliSense setup.

Vite emits Monaco's editor worker and grammar chunks alongside the site, with
no CDN requests or server endpoint. The editor follows the page's light/dark
theme. Plain text remains available during loading, or with an explicit error
message if the editor cannot load. A scoped dependency override supplies a
patched DOMPurify version instead of Monaco's older pinned version.

## What the recording proves

Each sample owns its application UI and graphics. The separate driver owns input
automation, readiness checks, capture, and human-readable dwell time. The website
shows the actual sample files, not another copy of its implementation in Markdown.

The driver stores complete HWT1 binary messages in a small JSON envelope:

```text
{
  format: "hex1b-hwt-recording",
  version: 1,
  durationMs: number,
  frames: [{ timeMs: number, data: "base64 HWT1 message" }]
}
```

The first timestamp is zero and the first message is a full baseline. Subsequent
messages retain their order, resource dependencies, and relative capture times.
The driver acknowledges captured frames locally; browser playback does not
emulate a server or fabricate WebSocket acknowledgements.

`WebTerminal.mountRecording` uses the same worker, decoder, image handling and
GPU renderer as the live terminal. It fetches a local static recording instead
of connecting a WebSocket. The worker presents one frame at a time and never
skips deltas to catch up. It scales the recorded viewport rather than asking a
nonexistent producer to resize/reflow.

## Current migration boundaries

- **Same-build only:** neither HWT1 nor this envelope is a stable archival format.
  Regenerate recordings and deploy the matching player together.
- The entire recording is fetched and validated before the first presentation.
  Limits are 64 MiB, 10,000 frames, and ten minutes.
- Play, pause, and restart are supported; seeking, speed controls, chunked
  downloads, and compression are not implemented.
- This is captured visible state, not a lossless ANSI event log. Live HWT
  invalidations may coalesce before capture. Animation is represented by the
  states actually captured, not by a second browser-side graphics protocol clock.
- Recorded blink uses playback time and freezes while paused. Terminal input,
  interactive scrollback, and server-backed text selection are unavailable.
  The host exposes the current visible frame as selectable plain text instead.
- Playback has the same browser requirements as the native web terminal:
  WebGPU or WebGL2, workers, transferable OffscreenCanvas, and worker font loading.
- The site serves the browser bundle directly, without an ASP.NET asset endpoint.
- Complete hosting, Docker, and external-asset examples are cloneable source-only
  samples with prerequisites. Their projects are built, but the site build never
  starts their services or provisions their environments. These pages deliberately
  have no playback controls; Monaco and Git cloning remain available.
- Authored prose is intentionally preserved, including terminology that may
  still describe live demos. Full sample projects may require small API updates
  to build against the pinned package; their README files record these changes.
- The main build does not fetch package versions in the browser. The installation
  instructions use `site.config.json` at build time.
- GitHub Pages publication, historical sample retention, and release-aligned
  site updates remain separate steps.

## Checks

```sh
npm run build --prefix src/web-terminal
npm test --prefix src/web-terminal
npm test --prefix src/site
npm run build --prefix src/site
```

Inspect the production site with the browser network panel: only static page,
module, font and recording requests should occur. Confirm the counter changes,
both labeled graphic panels render, pausing freezes progress, and restarting
restores the initial state.
