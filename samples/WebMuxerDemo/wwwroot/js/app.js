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
  term.open(terminalContainer);
  // Defer the initial layout a frame so the container has its laid-out size.
  requestAnimationFrame(() => applyRoleAwareLayout());

  term.onData((s) => client?.sendInput(s));
  // Note: term.onResize fires whenever fitAddon.fit() OR a manual term.resize()
  // changes the xterm grid. We forward to the producer via sendResize, but
  // Hmp1Client.sendResize() silently no-ops when we're not primary, so
  // viewers' fit() calls don't disturb the producer. We also re-render the
  // status line so the "dims mismatch" banner re-evaluates.
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

// Sizes the xterm display based on the current role:
//
//  - Primary or no-primary: fitAddon.fit() — grid grows/shrinks to fill
//    the available container, font stays at its native 13px. The producer
//    will get a Resize from term.onResize → client.sendResize.
//
//  - Secondary (someone else is primary): lock the xterm grid to the
//    producer's cols×rows so we render exactly what the primary sees,
//    then apply a CSS transform: scale() to the xterm root element so
//    the rendered grid fills the available container without distortion.
//    Letterbox horizontally or vertically as needed (preserves aspect).
//    The transform doesn't change the xterm grid size — input still works
//    and there's no layout reflow on the producer side.
function applyRoleAwareLayout() {
  if (!term || !fitAddon) return;

  const root = term.element;
  if (!root) return;

  const haveProducerDims = !!client && client.width > 0 && client.height > 0;
  const isSecondary = !!client && !client.isPrimary && haveProducerDims;

  if (!isSecondary) {
    // Primary, no-primary, or pre-handshake: clear any previous scaling
    // and fill the container at native font size.
    if (root.style.transform) root.style.transform = "";
    safeFit();
    return;
  }

  // 1. Lock our grid to the producer's dims. After this, term.element's
  //    natural size (offsetWidth/Height — unaffected by CSS transforms)
  //    is whatever cols×cellWidth × rows×cellHeight pixels work out to.
  if (term.cols !== client.width || term.rows !== client.height) {
    try { term.resize(client.width, client.height); } catch { /* ignore */ }
  }

  // 2. Measure the natural laid-out xterm root size and compute the scale
  //    needed to fit it into the container while preserving aspect.
  //    offsetWidth/Height return the layout box without transforms applied,
  //    so we can leave the previous scale in place during measurement and
  //    avoid a one-frame visual flash from resetting it.
  const naturalWidth = root.offsetWidth;
  const naturalHeight = root.offsetHeight;
  const containerWidth = terminalContainer.clientWidth;
  const containerHeight = terminalContainer.clientHeight;

  if (naturalWidth <= 0 || naturalHeight <= 0 ||
      containerWidth <= 0 || containerHeight <= 0) {
    return;
  }

  const scale = Math.min(
    containerWidth / naturalWidth,
    containerHeight / naturalHeight);

  if (scale <= 0) return;

  // Skip when current transform is essentially the same to avoid layout
  // thrashing on every ResizeObserver tick.
  const target = `scale(${scale})`;
  if (root.style.transform !== target) {
    root.style.transformOrigin = "top left";
    root.style.transform = target;
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

  // We want the producer to switch to *our* available space, not the
  // currently-locked producer dims. Temporarily clear the scale and
  // fit-to-container so term.cols/rows reflect what we'd want as primary,
  // then send RequestPrimary with those dims. The server will accept and
  // broadcast a Resize back, after which applyRoleAwareLayout (called
  // from onRoleChange) will leave us at native scale because we're now
  // primary.
  if (term.element) term.element.style.transform = "";
  safeFit();
  client.requestPrimary(term.cols, term.rows);
}

connectBtn.addEventListener("click", connect);
disconnectBtn.addEventListener("click", disconnect);
takePrimaryBtn.addEventListener("click", takePrimary);

await loadSessions();
renderStatus();
renderRoster();
