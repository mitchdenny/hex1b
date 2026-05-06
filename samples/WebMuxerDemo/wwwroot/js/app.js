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

  if (term && !client.isPrimary
      && (client.width !== term.cols || client.height !== term.rows)) {
    banner.textContent =
      `⚠ producer is ${client.width}×${client.height}, your terminal is ${term.cols}×${term.rows} — click "Take Control" to drive resizing`;
    banner.style.display = "block";
  } else {
    banner.style.display = "none";
  }
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
  fitAddon.fit();

  term.onData((s) => client?.sendInput(s));
  term.onResize(({ cols, rows }) => client?.sendResize(cols, rows));

  window.addEventListener("resize", () => {
    try { fitAddon.fit(); } catch { /* ignore */ }
  });
}

function disposeTerminal() {
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
  };

  client.onRoleChange = () => {
    if (client.isPrimary && term) {
      // We are now primary. Push our local dims so the producer matches us.
      client.sendResize(term.cols, term.rows);
    }
    renderStatus();
    renderRoster();
  };

  client.onPeerJoin = () => renderRoster();
  client.onPeerLeave = () => renderRoster();

  client.onResize = (cols, rows) => {
    renderStatus();
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
  if (!client || !term) return;
  client.requestPrimary(term.cols, term.rows);
}

connectBtn.addEventListener("click", connect);
disconnectBtn.addEventListener("click", disconnect);
takePrimaryBtn.addEventListener("click", takePrimary);

await loadSessions();
renderStatus();
renderRoster();
