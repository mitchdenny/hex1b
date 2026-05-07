import { Hmp1Client } from "./hmp1-client.js";

const sessionPicker = document.getElementById("session-picker");
const connectBtn = document.getElementById("connect");
const disconnectBtn = document.getElementById("disconnect");
const takePrimaryBtn = document.getElementById("take-primary");
const statusBadge = document.getElementById("status-badge");
const dimsLabel = document.getElementById("dims");
const peersList = document.getElementById("peers");
const myIdLabel = document.getElementById("my-id");
const banner = document.getElementById("banner");
const terminalContainer = document.getElementById("terminal");

let term = null;
let fitAddon = null;
let resizeObserver = null;
let client = null;

// --- Primary-mode sizing controls ----------------------------------------
//
// In primary mode we drive the producer's PTY dims, so we expose a footer
// with two mutually-exclusive sizing modes:
//
//   "font"   (Auto)  : user controls font size with +/- buttons; FitAddon
//                      picks cols×rows to fill the available stage at that
//                      font. Window resize → fit → new cols×rows broadcast.
//
//   "fixed"  (preset): user picks a grid (e.g. 80×24) from the dropdown;
//                      we compute the largest font that makes that grid
//                      fill the stage and lock cols×rows. Window resize →
//                      recompute font, cols×rows stay fixed (no broadcast).
//
// The font ratios (cellW/cellH per font px) are calibrated from the
// rendered .xterm-screen so we can predict optimal font without iterating.
// Monospace fonts scale linearly with font size; xterm rounds to integer
// pixels, so we update the ratios after each render to absorb drift.
const MIN_FONT_PX = 4;
const MAX_FONT_PX = 72;
const DEFAULT_FONT_PX = 13;
const SIZE_PRESETS = [
  { value: "auto",   label: "Auto",   cols: 0,   rows: 0  },
  { value: "80x24",  label: "80×24",  cols: 80,  rows: 24 },
  { value: "80x30",  label: "80×30",  cols: 80,  rows: 30 },
  { value: "100x30", label: "100×30", cols: 100, rows: 30 },
  { value: "132x30", label: "132×30", cols: 132, rows: 30 },
  { value: "132x50", label: "132×50", cols: 132, rows: 50 },
];

let sizeMode = "font";               // "font" | "fixed"
let fixedDims = null;                 // {cols, rows} when sizeMode === "fixed"
let currentFontPx = DEFAULT_FONT_PX;
let cellWRatio = 0;                   // CSS px of a single cell width per font px
let cellHRatio = 0;                   // CSS px of a single cell height per font px

// Monotonic layout generation. Each applyRoleAwareLayout() call bumps
// this; deferred RAF callbacks capture the current value and bail if
// they're stale. Prevents a fixed-mode RAF (or secondary-mode
// measureAndScale RAF) from overwriting a newer layout's body/.xterm
// styles after the user switches modes, takes/loses primary, or the
// container resizes between the schedule and the callback.
let layoutGeneration = 0;

async function loadSessions() {
  const r = await fetch("/api/sessions");
  const data = await r.json();
  sessionPicker.innerHTML = "";
  for (const s of data) {
    const opt = document.createElement("option");
    opt.value = s.name;
    opt.textContent = s.name;
    sessionPicker.appendChild(opt);
  }
}

function renderRoster() {
  const me = client?.peerId ?? null;
  const primary = client?.primaryPeerId ?? null;
  const list = [];
  if (me !== null) {
    list.push({ peerId: me, displayName: "you", isSelf: true });
  }
  for (const p of client?.peers ?? []) {
    list.push({ peerId: p.peerId, displayName: p.displayName ?? "", isSelf: false });
  }

  peersList.innerHTML = "";
  for (const p of list) {
    const li = document.createElement("li");
    const tag = p.peerId === primary ? " ◀ primary" : "";
    li.textContent = `${p.peerId} (${p.displayName || "—"})${tag}`;
    if (p.isSelf) li.classList.add("self");
    if (p.peerId === primary) li.classList.add("primary");
    peersList.appendChild(li);
  }

  myIdLabel.textContent = me ?? "—";
}

function renderStatus() {
  if (!client) {
    statusBadge.className = "badge offline";
    statusBadge.textContent = "offline";
    dimsLabel.textContent = "—";
    banner.style.display = "none";
    takePrimaryBtn.disabled = true;
    // Hide the footer too — no producer to drive when offline.
    updateFooterControls();
    return;
  }

  if (client.isPrimary) {
    statusBadge.className = "badge primary";
    statusBadge.textContent = "PRIMARY";
    takePrimaryBtn.disabled = true;
  } else if (client.primaryPeerId === null) {
    statusBadge.className = "badge no-primary";
    statusBadge.textContent = "no primary";
    takePrimaryBtn.disabled = false;
  } else {
    statusBadge.className = "badge viewer";
    statusBadge.textContent = "viewer";
    takePrimaryBtn.disabled = false;
  }

  dimsLabel.textContent = `${client.width}×${client.height}`;

  // The "dims mismatch" warning made sense when viewers were stuck at
  // the producer's grid; now viewers scale-to-fit so the only thing the
  // user might want to know is that they're observing a remotely-driven
  // grid. Hide the banner outright — the badge already conveys role.
  banner.style.display = "none";

  // Keep the primary-mode footer in sync with role changes. Cheap to
  // call when not primary (just toggles display:none).
  updateFooterControls();
}

function ensureTerminal() {
  if (term) return;

  // Build the frame structure inside #terminal so the visual chrome
  // (header, border, footer, drop shadow) lives on a non-transformed
  // wrapper:
  //
  //   #terminal               (flex centring host, in styles.css)
  //     #terminal-frame       (the card — border + shadow + radius)
  //       #terminal-titlebar  (header — title text)
  //       #terminal-body      (xterm host; pinned to scaled bounds in
  //                            secondary mode, fills available space in
  //                            primary font mode, hugs natural dims in
  //                            primary fixed mode; clips overflow)
  //       #terminal-footer    (primary-mode controls; hidden in
  //                            secondary mode)
  //
  // Putting border/shadow on #terminal-frame keeps them at fixed CSS
  // pixel sizes regardless of the CSS transform we apply to the .xterm
  // for secondary lock-and-scale (transforms scale shadows too, which
  // made the previous shadow appear to "change shape").
  const frame = document.createElement("div");
  frame.id = "terminal-frame";

  const titlebar = document.createElement("div");
  titlebar.id = "terminal-titlebar";
  const titleText = document.createElement("span");
  titleText.id = "terminal-title";
  titleText.textContent = sessionPicker.value || "terminal";
  titlebar.appendChild(titleText);

  const body = document.createElement("div");
  body.id = "terminal-body";

  const footer = buildFooter();

  frame.appendChild(titlebar);
  frame.appendChild(body);
  frame.appendChild(footer);
  terminalContainer.appendChild(frame);

  term = new window.Terminal({
    cursorBlink: true,
    fontFamily: 'Menlo, Consolas, "DejaVu Sans Mono", monospace',
    fontSize: currentFontPx,
    theme: {
      background: "#0d1117",
      foreground: "#c9d1d9",
      cursor: "#58a6ff",
      selectionBackground: "#1f6feb55",
    },
    allowProposedApi: true,
  });
  fitAddon = new window.FitAddon.FitAddon();
  term.loadAddon(fitAddon);
  term.open(body);
  // Defer the initial layout a frame so the container has its laid-out
  // size, then calibrate cell ratios from the just-rendered grid so the
  // first switch into fixed mode can compute an optimal font without
  // needing to iterate.
  requestAnimationFrame(() => {
    calibrateRatios();
    applyRoleAwareLayout();
  });

  // OSC 0 / OSC 2 / OSC 1 — terminal apps (PowerShell, bash via PROMPT_COMMAND,
  // ssh, vim, top, …) push window/icon titles via these escape sequences.
  // xterm.js parses them and fires onTitleChange with the new string.
  term.onTitleChange((newTitle) => {
    titleText.textContent = newTitle || sessionPicker.value || "terminal";
  });

  term.onData((s) => client?.sendInput(s));
  // Note: term.onResize fires whenever fitAddon.fit() OR a manual term.resize()
  // changes the xterm grid. We forward to the producer via sendResize, but
  // Hmp1Client.sendResize() silently no-ops when we're not primary, so
  // viewers' fit() calls don't disturb the producer. We also re-render the
  // status line so the dimension display in the top status bar updates.
  term.onResize(({ cols, rows }) => {
    client?.sendResize(cols, rows);
    renderStatus();
    // Cell ratios may have drifted by integer-pixel rounding — refresh
    // calibration from the new render so future fixed-mode font calcs
    // stay accurate.
    calibrateRatios();
    updateFooterControls();
  });

  // Re-layout on any container size change (window resize, sidebar collapse,
  // banner showing/hiding, devtools opening/closing, …).
  if (typeof ResizeObserver !== "undefined") {
    resizeObserver = new ResizeObserver(() => applyRoleAwareLayout());
    resizeObserver.observe(terminalContainer);
  } else {
    window.addEventListener("resize", applyRoleAwareLayout);
  }
}

// Builds the primary-mode footer DOM and wires button/select events.
// The footer is only displayed in primary mode (toggled in
// updateFooterControls); buttons/select are inert until shown.
function buildFooter() {
  const footer = document.createElement("div");
  footer.id = "terminal-footer";
  footer.style.display = "none"; // hidden until we become primary

  // Font controls
  const fontGroup = document.createElement("div");
  fontGroup.className = "footer-group";
  const fontLabel = document.createElement("span");
  fontLabel.className = "footer-label";
  fontLabel.textContent = "Font";

  const fontMinus = document.createElement("button");
  fontMinus.id = "font-minus";
  fontMinus.textContent = "−";
  fontMinus.title = "Decrease font size";
  fontMinus.addEventListener("click", () => setFontSize(currentFontPx - 1));

  const fontDisplay = document.createElement("span");
  fontDisplay.id = "font-display";
  fontDisplay.textContent = `${currentFontPx}`;

  const fontPlus = document.createElement("button");
  fontPlus.id = "font-plus";
  fontPlus.textContent = "+";
  fontPlus.title = "Increase font size";
  fontPlus.addEventListener("click", () => setFontSize(currentFontPx + 1));

  fontGroup.append(fontLabel, fontMinus, fontDisplay, fontPlus);

  // Size dropdown
  const sizeGroup = document.createElement("div");
  sizeGroup.className = "footer-group";
  const sizeLabel = document.createElement("span");
  sizeLabel.className = "footer-label";
  sizeLabel.textContent = "Size";

  const sizeSelect = document.createElement("select");
  sizeSelect.id = "size-select";
  for (const p of SIZE_PRESETS) {
    const o = document.createElement("option");
    o.value = p.value;
    o.textContent = p.label;
    sizeSelect.appendChild(o);
  }
  sizeSelect.addEventListener("change", (e) => {
    const v = e.target.value;
    if (v === "auto") {
      setSizeMode("font", null);
    } else {
      const preset = SIZE_PRESETS.find((p) => p.value === v);
      if (preset) setSizeMode("fixed", { cols: preset.cols, rows: preset.rows });
    }
  });

  sizeGroup.append(sizeLabel, sizeSelect);

  // Live dims readout (right-aligned via margin-left:auto in CSS)
  const dimsReadout = document.createElement("span");
  dimsReadout.id = "footer-dims";
  dimsReadout.innerHTML = `<span class="value">—</span> × <span class="value">—</span>`;

  footer.append(fontGroup, sizeGroup, dimsReadout);
  return footer;
}

function safeFit() {
  try { fitAddon?.fit(); } catch { /* ignore — happens during teardown */ }
}

// Returns the available CSS pixel space inside #terminal that the
// terminal body can grow into (i.e., #terminal minus the titlebar
// height, the footer height when shown, and the frame border thickness).
const FRAME_BORDER_PX = 2;
function getAvailableBodySpace() {
  const titlebar = document.getElementById("terminal-titlebar");
  const footer = document.getElementById("terminal-footer");
  const titlebarH = titlebar ? titlebar.offsetHeight : 0;
  // Footer is hidden in secondary mode (display:none → offsetHeight 0)
  // so this branch handles both modes uniformly.
  const footerH = footer ? footer.offsetHeight : 0;
  const stageW = terminalContainer.clientWidth;
  const stageH = terminalContainer.clientHeight;
  return {
    width: Math.max(0, stageW - FRAME_BORDER_PX * 2),
    height: Math.max(0, stageH - titlebarH - footerH - FRAME_BORDER_PX * 2),
  };
}

// Sizes the xterm display based on the current role and (in primary
// mode) the current sizing mode:
//
//  - Secondary (someone else is primary): lock the xterm grid to the
//    producer's cols×rows so we render exactly what the primary sees,
//    then apply a CSS transform: scale() to .xterm so the rendered grid
//    fills the available space without distortion. Pin #terminal-body
//    to the SCALED visible bounds so the frame card hugs the content
//    (no empty layout space around the scaled grid). Letterbox
//    horizontally or vertically as needed (preserves aspect).
//
//  - Primary, font-driven (sizeMode === "font"): pin #terminal-body
//    to the available stage space (so FitAddon measures the right
//    area), then fitAddon.fit() — grid grows/shrinks to fill at the
//    user's chosen font size. The producer will get a Resize from
//    term.onResize → client.sendResize.
//
//  - Primary, fixed (sizeMode === "fixed"): cols×rows are locked to
//    the user's preset; we compute the largest font that lets that
//    grid fit the available space, set fontSize, and call term.resize
//    to force the grid back to the chosen dims (xterm may have changed
//    them when we changed the font). Pin #terminal-body to the natural
//    rendered dims so the frame card hugs the chosen grid (with the
//    grey gradient stage showing around it as letterboxing).
//
// Measurements use the inner .xterm-screen element, which xterm.js's
// renderer explicitly sizes to cols×cellWidth × rows×cellHeight pixels.
function applyRoleAwareLayout() {
  if (!term || !fitAddon) return;

  const root = term.element;
  if (!root) return;
  const body = root.parentElement;
  if (!body) return;

  // Bump generation: any RAF callbacks queued by prior layout calls
  // become stale and will bail when they run.
  const generation = ++layoutGeneration;

  // Footer visibility depends on isPrimary, and getAvailableBodySpace
  // subtracts the footer height — so update controls FIRST.
  updateFooterControls();

  const haveProducerDims = !!client && client.width > 0 && client.height > 0;
  const isSecondary = !!client && !client.isPrimary && haveProducerDims;
  const { width: availableW, height: availableH } = getAvailableBodySpace();

  if (!isSecondary) {
    // Primary, no-primary, or pre-handshake: clear any secondary
    // pinning on .xterm so it can flow naturally inside body.
    if (root.style.transform || root.style.width || root.style.height) {
      root.style.transform = "";
      root.style.transformOrigin = "";
      root.style.width = "";
      root.style.height = "";
    }

    if (sizeMode === "fixed" && fixedDims) {
      // Compute the largest font that lets fixedDims fit the stage,
      // set it, and resize the grid back to the fixed dims (xterm may
      // have reflowed them when fontSize changed).
      const optFont = computeOptimalFont(
        fixedDims.cols, fixedDims.rows, availableW, availableH);
      if (term.options.fontSize !== optFont) {
        term.options.fontSize = optFont;
      }
      currentFontPx = optFont;
      if (term.cols !== fixedDims.cols || term.rows !== fixedDims.rows) {
        try { term.resize(fixedDims.cols, fixedDims.rows); } catch { /* ignore */ }
      }
      // Defer body pinning until after the renderer catches up to the
      // new fontSize/dims. Bail if the layout has been superseded
      // (mode/role/dims/window changed before the RAF ran).
      const expectedCols = fixedDims.cols;
      const expectedRows = fixedDims.rows;
      requestAnimationFrame(() => {
        if (generation !== layoutGeneration) return;
        if (sizeMode !== "fixed" || !fixedDims) return;
        if (fixedDims.cols !== expectedCols || fixedDims.rows !== expectedRows) return;
        pinBodyToNatural(root, body);
      });
    } else {
      // Font-driven: pin body to available, fit() picks cols×rows.
      const bodyW = `${availableW}px`;
      const bodyH = `${availableH}px`;
      if (body.style.width !== bodyW || body.style.height !== bodyH) {
        body.style.width = bodyW;
        body.style.height = bodyH;
      }
      // If the user has changed font size since the last layout, push
      // it into xterm before fit() so it picks the right cell size.
      if (term.options.fontSize !== currentFontPx) {
        term.options.fontSize = currentFontPx;
      }
      safeFit();
    }
    updateFooterControls();
    return;
  }

  // Lock our grid to the producer's dims so xterm draws exactly what
  // the primary sees. Important: do this BEFORE measuring, otherwise
  // we'd scale based on stale dims.
  const needsResize = term.cols !== client.width || term.rows !== client.height;
  if (needsResize) {
    try { term.resize(client.width, client.height); } catch { /* ignore */ }
  }

  // If we just resized, the renderer may not have written the new
  // .xterm-screen dimensions yet. Defer measurement to the next frame.
  // Re-read available space inside the RAF (don't use captured values)
  // and bail if the layout has been superseded.
  if (needsResize) {
    requestAnimationFrame(() => {
      if (generation !== layoutGeneration) return;
      const fresh = getAvailableBodySpace();
      measureAndScale(fresh.width, fresh.height);
    });
  } else {
    measureAndScale(availableW, availableH);
  }
}

// Sets #terminal-body to exactly the rendered grid's pixel dims so the
// frame card hugs the visible terminal content (no layout slack around
// it). Used in primary fixed mode and indirectly by measureAndScale.
function pinBodyToNatural(root, body) {
  if (!root || !body) return;
  const screenEl =
    root.querySelector(".xterm-screen") ||
    root.querySelector("canvas.xterm-text-layer") ||
    root;
  const w = screenEl.offsetWidth;
  const h = screenEl.offsetHeight;
  if (w > 0 && h > 0) {
    const bodyW = `${w}px`;
    const bodyH = `${h}px`;
    if (body.style.width !== bodyW || body.style.height !== bodyH) {
      body.style.width = bodyW;
      body.style.height = bodyH;
    }
  }
  // Re-calibrate cell ratios from the freshly rendered grid so future
  // optimal-font calculations use up-to-date numbers (xterm rounds cell
  // sizes to integer pixels per font px, which can drift slightly).
  calibrateRatios();
}

// Stores cell width/height per CSS px of font size, derived from the
// currently rendered .xterm-screen. Called on initial render and after
// every term.onResize so we always have fresh metrics for fixed-mode
// font calculations.
function calibrateRatios() {
  if (!term || !term.element) return;
  const screenEl = term.element.querySelector(".xterm-screen");
  if (!screenEl) return;
  const w = screenEl.offsetWidth;
  const h = screenEl.offsetHeight;
  const fs = term.options.fontSize || currentFontPx;
  if (w > 0 && h > 0 && term.cols > 0 && term.rows > 0 && fs > 0) {
    cellWRatio = (w / term.cols) / fs;
    cellHRatio = (h / term.rows) / fs;
  }
}

// Returns the largest integer font size in [MIN, MAX] such that
// cols × cellW(font) ≤ availW AND rows × cellH(font) ≤ availH.
// Falls back to current font size if we haven't calibrated yet.
function computeOptimalFont(cols, rows, availW, availH) {
  if (cellWRatio <= 0 || cellHRatio <= 0) return currentFontPx;
  if (cols <= 0 || rows <= 0 || availW <= 0 || availH <= 0) return currentFontPx;
  const fsW = availW / (cols * cellWRatio);
  const fsH = availH / (rows * cellHRatio);
  const fs = Math.floor(Math.min(fsW, fsH));
  return Math.max(MIN_FONT_PX, Math.min(MAX_FONT_PX, fs));
}

// Called when the user clicks the font +/- buttons. Always switches to
// font-driven mode (per UX: +/- in fixed mode is disabled, but defending
// in case it were ever bound differently).
function setFontSize(newSize) {
  newSize = Math.max(MIN_FONT_PX, Math.min(MAX_FONT_PX, newSize));
  if (newSize === currentFontPx && sizeMode === "font") return;
  currentFontPx = newSize;
  sizeMode = "font";
  fixedDims = null;
  if (term) term.options.fontSize = currentFontPx;
  applyRoleAwareLayout();
}

// Called when the user picks an option from the size dropdown. mode is
// "font" (Auto) or "fixed"; dims is {cols, rows} for fixed mode.
function setSizeMode(mode, dims) {
  if (mode === sizeMode &&
      ((mode === "font") ||
       (mode === "fixed" && dims && fixedDims &&
        dims.cols === fixedDims.cols && dims.rows === fixedDims.rows))) {
    return;
  }
  sizeMode = mode;
  fixedDims = mode === "fixed" ? dims : null;
  applyRoleAwareLayout();
}

// Refreshes the footer's visibility (primary-only) and the state of its
// controls (font display, button disabled state, dropdown selection,
// dims readout). Idempotent — safe to call from anywhere.
function updateFooterControls() {
  const footer = document.getElementById("terminal-footer");
  if (!footer) return;

  const isPrimary = !!client && client.isPrimary;
  // Show footer in primary mode only. Use display:flex (matches CSS).
  footer.style.display = isPrimary ? "flex" : "none";

  const fontDisplay = document.getElementById("font-display");
  const fontMinus = document.getElementById("font-minus");
  const fontPlus = document.getElementById("font-plus");
  const sizeSelect = document.getElementById("size-select");
  const dimsReadout = document.getElementById("footer-dims");

  if (fontDisplay) fontDisplay.textContent = `${currentFontPx}`;

  // +/- disabled in fixed mode (font is purely auto-derived there).
  if (fontMinus) fontMinus.disabled = sizeMode === "fixed";
  if (fontPlus) fontPlus.disabled = sizeMode === "fixed";

  if (sizeSelect) {
    const expected = sizeMode === "fixed" && fixedDims
      ? `${fixedDims.cols}x${fixedDims.rows}`
      : "auto";
    if (sizeSelect.value !== expected) sizeSelect.value = expected;
  }

  if (dimsReadout) {
    const c = term && term.cols ? term.cols : "—";
    const r = term && term.rows ? term.rows : "—";
    dimsReadout.innerHTML =
      `<span class="value">${c}</span> × <span class="value">${r}</span>`;
  }
}

function measureAndScale(availableW, availableH) {
  if (!term || !client) return;
  const root = term.element;
  if (!root) return;
  const body = root.parentElement;
  if (!body) return;

  // Measure .xterm-screen which xterm sizes to natural grid pixels.
  // Fall back to the text canvas if .xterm-screen isn't present
  // (transitional state during teardown).
  const screenEl =
    root.querySelector(".xterm-screen") ||
    root.querySelector("canvas.xterm-text-layer") ||
    root;
  const naturalWidth = screenEl.offsetWidth;
  const naturalHeight = screenEl.offsetHeight;

  if (naturalWidth <= 0 || naturalHeight <= 0 ||
      availableW <= 0 || availableH <= 0) {
    return;
  }

  const scale = Math.min(
    availableW / naturalWidth,
    availableH / naturalHeight);

  if (scale <= 0) return;

  // Pin .xterm to natural pixels and apply the scale transform with
  // origin top-left. The body (sized to scaled bounds, below) clips any
  // visual overflow that would occur if the renderer hasn't caught up.
  const xtermTransform = `scale(${scale})`;
  const xtermW = `${naturalWidth}px`;
  const xtermH = `${naturalHeight}px`;
  if (root.style.transform !== xtermTransform ||
      root.style.width !== xtermW ||
      root.style.height !== xtermH) {
    root.style.transformOrigin = "top left";
    root.style.transform = xtermTransform;
    root.style.width = xtermW;
    root.style.height = xtermH;
  }

  // Pin the body to the scaled visible bounds so the frame card hugs
  // the content. Math.floor (not round) + clamp to availableW/H so we
  // can never produce a body 1px wider than the stage from sub-pixel
  // accumulation — a 1px overflow re-triggers ResizeObserver in a tight
  // loop and looks like the terminal is bouncing.
  const bodyW = `${Math.min(availableW, Math.floor(naturalWidth * scale))}px`;
  const bodyH = `${Math.min(availableH, Math.floor(naturalHeight * scale))}px`;
  if (body.style.width !== bodyW || body.style.height !== bodyH) {
    body.style.width = bodyW;
    body.style.height = bodyH;
  }
}

function disposeTerminal() {
  if (resizeObserver) {
    try { resizeObserver.disconnect(); } catch { /* ignore */ }
    resizeObserver = null;
  } else {
    window.removeEventListener("resize", applyRoleAwareLayout);
  }
  if (term) {
    try { term.dispose(); } catch { /* ignore */ }
    term = null;
    fitAddon = null;
  }
  terminalContainer.innerHTML = "";
}

function connect() {
  if (client) return;
  const session = sessionPicker.value;
  if (!session) return;

  ensureTerminal();
  term.clear();

  const proto = window.location.protocol === "https:" ? "wss" : "ws";
  const url = `${proto}://${window.location.host}/ws/${encodeURIComponent(session)}`;

  client = new Hmp1Client({
    url,
    displayName: `browser-${Math.random().toString(36).slice(2, 7)}`,
    defaultRole: "viewer",
  });

  client.onOpen = () => {
    connectBtn.disabled = true;
    disconnectBtn.disabled = false;
    sessionPicker.disabled = true;
  };

  client.onScreenBytes = (bytes) => {
    if (term) term.write(bytes);
  };

  client.onHello = () => {
    renderStatus();
    renderRoster();
    applyRoleAwareLayout();
  };

  client.onRoleChange = () => {
    renderStatus();
    renderRoster();
    // Run layout FIRST so fixed-mode (if active) can resize the grid
    // to fixedDims; the resulting term.onResize will sendResize the
    // correct dims to the producer. Then send an explicit fallback in
    // case nothing changed (e.g. font-driven mode where local dims
    // already happen to match what we want broadcast). Sending after
    // layout avoids broadcasting stale secondary-locked dims first
    // and then immediately overriding them.
    applyRoleAwareLayout();
    if (client.isPrimary && term) {
      client.sendResize(term.cols, term.rows);
    }
  };

  client.onPeerJoin = () => renderRoster();
  client.onPeerLeave = () => renderRoster();

  client.onResize = (cols, rows) => {
    renderStatus();
    // Producer's grid changed (only happens via primary's Resize). For
    // secondaries this is the trigger to re-lock-and-scale to the new dims.
    applyRoleAwareLayout();
  };

  client.onExit = (code) => {
    if (term) term.write(`\r\n[workload exited with code ${code}]\r\n`);
  };

  client.onClose = () => {
    client = null;
    connectBtn.disabled = false;
    disconnectBtn.disabled = true;
    sessionPicker.disabled = false;
    renderStatus();
    renderRoster();
  };

  client.connect();
}

function disconnect() {
  client?.close();
  // onClose handler will reset UI.
}

function takePrimary() {
  if (!client || !term || !fitAddon) return;

  // Clear ALL of the secondary's lock-and-scale styling so
  // applyRoleAwareLayout() can switch to primary layout cleanly: clear
  // the inline styles on .xterm AND on #terminal-body (otherwise the
  // body stays pinned to the old scaled bounds and the frame card
  // doesn't grow to the available space).
  if (term.element) {
    term.element.style.transform = "";
    term.element.style.transformOrigin = "";
    term.element.style.width = "";
    term.element.style.height = "";
    const body = term.element.parentElement;
    if (body) {
      body.style.width = "";
      body.style.height = "";
    }
  }
  applyRoleAwareLayout();
  client.requestPrimary(term.cols, term.rows);
}

connectBtn.addEventListener("click", connect);
disconnectBtn.addEventListener("click", disconnect);
takePrimaryBtn.addEventListener("click", takePrimary);

await loadSessions();
renderStatus();
renderRoster();
