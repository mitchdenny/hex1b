import {
  createDefaultScrollbarRenderer, renderDefaultScrollbar, renderDefaultScrollbarTooltip,
  type TerminalScrollbarRenderer, type TerminalScrollbarTooltipRenderer
} from "@hex1b/web-terminal";

/** Keep theme colors live while making each part independently translucent. */
export const styledDefaultScrollbar = createDefaultScrollbarRenderer({
  track: { opacity: 0.12 },
  thumb: { opacity: 0.7 },
  markers: { opacity: 0.85 }
});

/** A synchronous painter with a longer, quadratic fade; hit testing stays library-owned. */
export const softFadeScrollbar: TerminalScrollbarRenderer = frame => {
  const { interaction, now } = frame;
  const active = interaction.near || interaction.hovered || interaction.dragging || interaction.focused;
  const elapsed = Math.max(0, now - interaction.lastActivityAt - 1200);
  const remaining = active ? 1 : Math.max(0, 1 - elapsed / 700);
  const opacity = interaction.reducedMotion ? (active || elapsed === 0 ? 1 : 0) : remaining * remaining;
  renderDefaultScrollbar({ ...frame, opacity });
  return !active && opacity > 0;
};

/** Draw a narrow rail, square thumb with grips, and stretching diamond marks from the same frame. */
export const customCanvasScrollbar: TerminalScrollbarRenderer = frame => {
  if (frame.opacity <= 0) return;
  const { context, track, thumb, colors } = frame;
  const alpha = context.globalAlpha * frame.opacity;
  context.save();
  try {
    context.globalAlpha = alpha * 0.35;
    context.fillStyle = colors.thumb;
    const railWidth = Math.min(2, track.width);
    context.fillRect(track.left + (track.width - railWidth) / 2, track.top, railWidth, track.height);

    context.globalAlpha = alpha;
    for (const { marker, bounds, color } of frame.markers) {
      const x = bounds.left + bounds.width / 2, y = bounds.top + bounds.height / 2;
      const radiusX = bounds.width / 2, radiusY = bounds.height / 2;
      context.fillStyle = color ?? (marker.exitCode != null && marker.exitCode !== 0 ? colors.error : colors.marker);
      if (marker.color) context.fillStyle = marker.color;
      context.beginPath();
      context.moveTo(x, y - radiusY);
      context.lineTo(x + radiusX, y);
      context.lineTo(x, y + radiusY);
      context.lineTo(x - radiusX, y);
      context.closePath();
      context.fill();
      if (frame.hoveredMarker?.marker.id === marker.id) {
        context.strokeStyle = colors.thumb;
        context.lineWidth = 1;
        context.stroke();
      }
    }
    const inset = Math.min(1, thumb.width / 4);
    const width = thumb.width - inset * 2;
    context.fillStyle = colors.thumb;
    context.fillRect(thumb.left + inset, thumb.top, width, thumb.height);
    context.fillStyle = colors.track;
    for (const offset of [-3, 0, 3]) {
      const top = thumb.top + thumb.height / 2 + offset;
      if (top >= thumb.top && top + 1 <= thumb.top + thumb.height)
        context.fillRect(thumb.left + inset + width / 4, top, width / 2, 1);
    }
    if (frame.interaction.focused && !frame.interaction.dragging) {
      context.strokeStyle = colors.thumb;
      context.lineWidth = 1;
      context.strokeRect(thumb.left + 0.5, thumb.top + 0.5,
        Math.max(0, thumb.width - 1), Math.max(0, thumb.height - 1));
    }
  } finally {
    context.restore();
  }
};

/** Decorate safely formatted built-in details; mounting and placement remain library-owned. */
export const customScrollbarTooltip: TerminalScrollbarTooltipRenderer = context => {
  const element = renderDefaultScrollbarTooltip(context);
  element.classList.add("demo-scrollbar-tooltip");
  Object.assign(element.style, {
    background: "var(--cp-surface)",
    color: "var(--cp-text)",
    borderColor: "var(--cp-border-strong)",
    borderLeft: "3px solid var(--cp-accent)",
    boxShadow: "var(--cp-shadow)"
  });
  const heading = document.createElement("div");
  heading.className = "demo-scrollbar-tooltip-heading";
  const source = document.createElement("strong");
  source.textContent = context.marker.source === "custom" ? "Bookmark" : "Shell mark";
  const position = document.createElement("span");
  position.textContent = context.marker.row === null ? "Unavailable" : `Row ${context.marker.row}`;
  heading.append(source, position);
  element.prepend(heading);
  return element;
};
