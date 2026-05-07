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
}

function ensureTerminal() {
  if (term) return;

  // Build the frame structure inside #terminal so the visual chrome
  // (header, border, drop shadow) lives on a non-transformed wrapper:
  //
  //   #terminal               (flex centring host, in styles.css)
  //     #terminal-frame       (the card — border + shadow + radius)
  //       #terminal-titlebar  (header — title text)
  //       #terminal-body      (xterm host; pinned to scaled bounds in
  //                            secondary mode, fills available space in
  //                            primary mode; clips overflow)
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

  frame.appendChild(titlebar);
  frame.appendChild(body);
  terminalContainer.appendChild(frame);

  term = new window.Terminal({
    cursorBlink: true,
    fontFamily: 'Menlo, Consolas, "DejaVu Sans Mono", monospace',
    fontSize: 13,
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
  // Defer the initial layout a frame so the container has its laid-out size.
  requestAnimationFrame(() => applyRoleAwareLayout());

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

function safeFit() {
  try { fitAddon?.fit(); } catch { /* ignore — happens during teardown */ }
}

// Returns the available CSS pixel space inside #terminal that the
// terminal body can grow into (i.e., #terminal minus the titlebar
// height and the frame border thickness).
const FRAME_BORDER_PX = 2;
function getAvailableBodySpace() {
  const titlebar = document.getElementById("terminal-titlebar");
  const titlebarH = titlebar ? titlebar.offsetHeight : 0;
  const stageW = terminalContainer.clientWidth;
  const stageH = terminalContainer.clientHeight;
  return {
    width: Math.max(0, stageW - FRAME_BORDER_PX * 2),
    height: Math.max(0, stageH - titlebarH - FRAME_BORDER_PX * 2),
  };
}

// Sizes the xterm display based on the current role:
//
//  - Primary or no-primary: pin #terminal-body to the available stage
//    space (so FitAddon measures the right area), then fitAddon.fit() —
//    grid grows/shrinks to fill at native font size. The producer will
//    get a Resize from term.onResize → client.sendResize.
//
//  - Secondary (someone else is primary): lock the xterm grid to the
//    producer's cols×rows so we render exactly what the primary sees,
//    then apply a CSS transform: scale() to .xterm so the rendered grid
//    fills the available space without distortion. Pin #terminal-body
//    to the SCALED visible bounds so the frame card hugs the content
//    (no empty layout space around the scaled grid). Letterbox
//    horizontally or vertically as needed (preserves aspect).
//
// Measurements use the inner .xterm-screen element, which xterm.js's
// renderer explicitly sizes to cols×cellWidth × rows×cellHeight pixels.
function applyRoleAwareLayout() {
  if (!term || !fitAddon) return;

  const root = term.element;
  if (!root) return;
  const body = root.parentElement;
  if (!body) return;

  const haveProducerDims = !!client && client.width > 0 && client.height > 0;
  const isSecondary = !!client && !client.isPrimary && haveProducerDims;
  const { width: availableW, height: availableH } = getAvailableBodySpace();

  if (!isSecondary) {
    // Primary, no-primary, or pre-handshake: clear the secondary
    // pinning on .xterm and pin the body to the available stage
    // space so fit() gets a sensible measurement.
    if (root.style.transform || root.style.width || root.style.height) {
      root.style.transform = "";
      root.style.transformOrigin = "";
      root.style.width = "";
      root.style.height = "";
    }
    const bodyW = `${availableW}px`;
    const bodyH = `${availableH}px`;
    if (body.style.width !== bodyW || body.style.height !== bodyH) {
      body.style.width = bodyW;
      body.style.height = bodyH;
    }
    safeFit();
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
  if (needsResize) {
    requestAnimationFrame(() => measureAndScale(availableW, availableH));
  } else {
    measureAndScale(availableW, availableH);
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
    if (client.isPrimary && term) {
      // We are now primary. Push our local dims so the producer matches us.
      // applyRoleAwareLayout below will fitAddon.fit() into the container,
      // which fires term.onResize → client.sendResize so the producer
      // catches up. We also send explicitly here in case our local grid
      // hadn't changed.
      client.sendResize(term.cols, term.rows);
    }
    renderStatus();
    renderRoster();
    applyRoleAwareLayout();
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
