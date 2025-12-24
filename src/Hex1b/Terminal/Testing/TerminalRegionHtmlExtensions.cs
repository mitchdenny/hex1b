using System.Text;
using System.Text.Json;
using System.Web;

namespace Hex1b.Terminal.Testing;

/// <summary>
/// Extension methods for rendering terminal regions to interactive HTML format.
/// </summary>
public static class TerminalRegionHtmlExtensions
{
    /// <summary>
    /// Renders the terminal region to an interactive HTML string with cell inspection.
    /// </summary>
    /// <param name="region">The terminal region to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An HTML string with embedded SVG and JavaScript for cell inspection.</returns>
    public static string ToHtml(this IHex1bTerminalRegion region, TerminalSvgOptions? options = null)
    {
        options ??= TerminalRegionSvgExtensions.DefaultOptions;
        return RenderToHtml(region, options, cursorX: null, cursorY: null);
    }

    /// <summary>
    /// Renders the terminal snapshot to an interactive HTML string with cell inspection.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An HTML string with embedded SVG and JavaScript for cell inspection.</returns>
    public static string ToHtml(this Hex1bTerminalSnapshot snapshot, TerminalSvgOptions? options = null)
    {
        options ??= TerminalRegionSvgExtensions.DefaultOptions;
        return RenderToHtml(snapshot, options, snapshot.CursorX, snapshot.CursorY);
    }

    private static string RenderToHtml(IHex1bTerminalRegion region, TerminalSvgOptions options, int? cursorX, int? cursorY)
    {
        var cellWidth = options.CellWidth;
        var cellHeight = options.CellHeight;
        var svgWidth = region.Width * cellWidth;
        var svgHeight = region.Height * cellHeight;

        // Build cell data as JSON for JavaScript
        var cellData = BuildCellDataJson(region);

        // For HTML, create options with grids enabled (we'll hide them via CSS initially)
        var htmlSvgOptions = new TerminalSvgOptions
        {
            FontFamily = options.FontFamily,
            FontSize = options.FontSize,
            CellWidth = options.CellWidth,
            CellHeight = options.CellHeight,
            DefaultBackground = options.DefaultBackground,
            DefaultForeground = options.DefaultForeground,
            CursorColor = options.CursorColor,
            ShowCellGrid = true,  // Include in SVG so it can be toggled via CSS
            ShowPixelGrid = true, // Include in SVG so it can be toggled via CSS
            CellGridColor = options.CellGridColor,
            PixelGridColor = options.PixelGridColor
        };

        // Get the SVG content with grids included (hidden via CSS initially)
        var svgContent = region is Hex1bTerminalSnapshot snapshot
            ? snapshot.ToSvg(htmlSvgOptions)
            : region.ToSvg(htmlSvgOptions);

        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Terminal Snapshot Inspector</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body {");
        sb.AppendLine("      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;");
        sb.AppendLine("      background: #1a1a2e;");
        sb.AppendLine("      color: #eee;");
        sb.AppendLine("      min-height: 100vh;");
        sb.AppendLine("      padding: 20px;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      flex-direction: column;");
        sb.AppendLine("    }");
        sb.AppendLine("    h1 {");
        sb.AppendLine("      font-size: 1.5rem;");
        sb.AppendLine("      margin-bottom: 10px;");
        sb.AppendLine("      color: #00d4ff;");
        sb.AppendLine("    }");
        sb.AppendLine("    .info-bar {");
        sb.AppendLine("      font-size: 0.85rem;");
        sb.AppendLine("      color: #888;");
        sb.AppendLine("      margin-bottom: 20px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .container {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      gap: 20px;");
        sb.AppendLine("      flex-wrap: wrap;");
        sb.AppendLine("      flex: 1;");
        sb.AppendLine("      min-height: 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    .svg-container {");
        sb.AppendLine("      position: relative;");
        sb.AppendLine("      border: 2px solid #333;");
        sb.AppendLine("      border-radius: 8px;");
        sb.AppendLine("      overflow: hidden;");
        sb.AppendLine("      background: #0d0d0d;");
        sb.AppendLine("      flex: 1;");
        sb.AppendLine("      min-height: 0;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      justify-content: center;");
        sb.AppendLine("      padding: 20px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .svg-container svg {");
        sb.AppendLine("      display: block;");
        sb.AppendLine("      max-width: 100%;");
        sb.AppendLine("      max-height: 100%;");
        sb.AppendLine("      width: auto;");
        sb.AppendLine("      height: auto;");
        sb.AppendLine("      image-rendering: pixelated;");
        sb.AppendLine("    }");
        sb.AppendLine("    .cell-highlight {");
        sb.AppendLine("      position: absolute;");
        sb.AppendLine("      pointer-events: none;");
        sb.AppendLine("      border: 1px solid #00d4ff;");
        sb.AppendLine("      border-radius: 2px;");
        sb.AppendLine("      opacity: 0;");
        sb.AppendLine("      transition: opacity 0.1s, box-shadow 0.2s;");
        sb.AppendLine("      box-shadow: 0 0 0 rgba(0, 212, 255, 0);");
        sb.AppendLine("    }");
        sb.AppendLine("    .cell-highlight.visible {");
        sb.AppendLine("      opacity: 1;");
        sb.AppendLine("    }");
        sb.AppendLine("    .cell-highlight.shimmer {");
        sb.AppendLine("      box-shadow: 0 0 12px rgba(0, 212, 255, 0.8), inset 0 0 8px rgba(0, 212, 255, 0.4);");
        sb.AppendLine("      border-color: #00ffff;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip {");
        sb.AppendLine("      position: fixed;");
        sb.AppendLine("      background: #2d2d44;");
        sb.AppendLine("      border: 1px solid #444;");
        sb.AppendLine("      border-radius: 8px;");
        sb.AppendLine("      padding: 12px 16px;");
        sb.AppendLine("      font-size: 13px;");
        sb.AppendLine("      box-shadow: 0 4px 20px rgba(0,0,0,0.5);");
        sb.AppendLine("      pointer-events: none;");
        sb.AppendLine("      opacity: 0;");
        sb.AppendLine("      transition: opacity 0.15s;");
        sb.AppendLine("      z-index: 1000;");
        sb.AppendLine("      min-width: 280px;");
        sb.AppendLine("      max-width: 400px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip.visible {");
        sb.AppendLine("      opacity: 1;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-header {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      gap: 12px;");
        sb.AppendLine("      margin-bottom: 10px;");
        sb.AppendLine("      padding-bottom: 10px;");
        sb.AppendLine("      border-bottom: 1px solid #444;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-char {");
        sb.AppendLine("      font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;");
        sb.AppendLine("      font-size: 28px;");
        sb.AppendLine("      width: 44px;");
        sb.AppendLine("      height: 44px;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      justify-content: center;");
        sb.AppendLine("      border-radius: 6px;");
        sb.AppendLine("      border: 1px solid #555;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-position {");
        sb.AppendLine("      color: #888;");
        sb.AppendLine("      font-size: 12px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-section {");
        sb.AppendLine("      margin-bottom: 8px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-section:last-child {");
        sb.AppendLine("      margin-bottom: 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-label {");
        sb.AppendLine("      color: #888;");
        sb.AppendLine("      font-size: 11px;");
        sb.AppendLine("      text-transform: uppercase;");
        sb.AppendLine("      letter-spacing: 0.5px;");
        sb.AppendLine("      margin-bottom: 3px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-value {");
        sb.AppendLine("      font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;");
        sb.AppendLine("      font-size: 12px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-row {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      gap: 16px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .tooltip-row > div {");
        sb.AppendLine("      flex: 1;");
        sb.AppendLine("    }");
        sb.AppendLine("    .color-swatch {");
        sb.AppendLine("      display: inline-block;");
        sb.AppendLine("      width: 14px;");
        sb.AppendLine("      height: 14px;");
        sb.AppendLine("      border-radius: 3px;");
        sb.AppendLine("      border: 1px solid #666;");
        sb.AppendLine("      vertical-align: middle;");
        sb.AppendLine("      margin-right: 6px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .attr-badge {");
        sb.AppendLine("      display: inline-block;");
        sb.AppendLine("      padding: 2px 6px;");
        sb.AppendLine("      margin: 2px;");
        sb.AppendLine("      border-radius: 4px;");
        sb.AppendLine("      font-size: 11px;");
        sb.AppendLine("      background: #3d3d5c;");
        sb.AppendLine("    }");
        sb.AppendLine("    .attr-badge.bold { font-weight: bold; }");
        sb.AppendLine("    .attr-badge.italic { font-style: italic; }");
        sb.AppendLine("    .attr-badge.underline { text-decoration: underline; }");
        sb.AppendLine("    .attr-badge.strikethrough { text-decoration: line-through; }");
        sb.AppendLine("    .attr-badge.dim { opacity: 0.5; }");
        sb.AppendLine("    .attr-badge.blink { background: #5c5c3d; }");
        sb.AppendLine("    .attr-badge.reverse { background: #5c3d5c; }");
        sb.AppendLine("    .attr-badge.hidden { background: #3d3d3d; }");
        sb.AppendLine("    .attr-badge.overline { text-decoration: overline; }");
        sb.AppendLine("    .theme-controls {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      gap: 20px;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      margin-bottom: 20px;");
        sb.AppendLine("      flex-wrap: wrap;");
        sb.AppendLine("    }");
        sb.AppendLine("    .theme-presets {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      gap: 8px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .theme-btn {");
        sb.AppendLine("      padding: 8px 16px;");
        sb.AppendLine("      border: 1px solid #444;");
        sb.AppendLine("      border-radius: 6px;");
        sb.AppendLine("      background: #2d2d44;");
        sb.AppendLine("      color: #eee;");
        sb.AppendLine("      cursor: pointer;");
        sb.AppendLine("      font-size: 13px;");
        sb.AppendLine("      transition: all 0.2s;");
        sb.AppendLine("    }");
        sb.AppendLine("    .theme-btn:hover {");
        sb.AppendLine("      background: #3d3d5c;");
        sb.AppendLine("    }");
        sb.AppendLine("    .theme-btn.active {");
        sb.AppendLine("      background: #00d4ff;");
        sb.AppendLine("      color: #000;");
        sb.AppendLine("      border-color: #00d4ff;");
        sb.AppendLine("    }");
        sb.AppendLine("    .color-pickers {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      gap: 16px;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("    }");
        sb.AppendLine("    .color-pickers label {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      gap: 8px;");
        sb.AppendLine("      font-size: 13px;");
        sb.AppendLine("      color: #888;");
        sb.AppendLine("    }");
        sb.AppendLine("    .color-pickers input[type=\"color\"] {");
        sb.AppendLine("      width: 40px;");
        sb.AppendLine("      height: 28px;");
        sb.AppendLine("      border: 1px solid #444;");
        sb.AppendLine("      border-radius: 4px;");
        sb.AppendLine("      cursor: pointer;");
        sb.AppendLine("      background: transparent;");
        sb.AppendLine("    }");
        sb.AppendLine("    .grid-controls {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      gap: 16px;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("    }");
        sb.AppendLine("    .grid-controls label {");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      gap: 6px;");
        sb.AppendLine("      font-size: 13px;");
        sb.AppendLine("      color: #888;");
        sb.AppendLine("      cursor: pointer;");
        sb.AppendLine("    }");
        sb.AppendLine("    .grid-controls input[type=\"checkbox\"] {");
        sb.AppendLine("      width: 16px;");
        sb.AppendLine("      height: 16px;");
        sb.AppendLine("      cursor: pointer;");
        sb.AppendLine("    }");
        sb.AppendLine("    .svg-container .cell-grid { display: none; }");
        sb.AppendLine("    .svg-container.show-cell-grid .cell-grid { display: block; }");
        sb.AppendLine("    .svg-container .pixel-grid { display: none; }");
        sb.AppendLine("    .svg-container.show-pixel-grid .pixel-grid { display: block; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <h1>Terminal Snapshot Inspector</h1>");
        sb.AppendLine($"  <div class=\"info-bar\">Size: {region.Width} × {region.Height} cells | Hover over cells to inspect</div>");
        sb.AppendLine("  <div class=\"theme-controls\">");
        sb.AppendLine("    <div class=\"theme-presets\">");
        sb.AppendLine("      <button id=\"btn-dark\" class=\"theme-btn active\">Dark Mode</button>");
        sb.AppendLine("      <button id=\"btn-light\" class=\"theme-btn\">Light Mode</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"color-pickers\">");
        sb.AppendLine("      <label>Background: <input type=\"color\" id=\"bg-picker\" value=\"#1e1e1e\"></label>");
        sb.AppendLine("      <label>Foreground: <input type=\"color\" id=\"fg-picker\" value=\"#d4d4d4\"></label>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"grid-controls\">");
        sb.AppendLine("      <label><input type=\"checkbox\" id=\"cell-grid-toggle\"> Cell Grid</label>");
        sb.AppendLine("      <label><input type=\"color\" id=\"cell-grid-color\" value=\"#808080\"></label>");
        sb.AppendLine("      <label><input type=\"checkbox\" id=\"pixel-grid-toggle\"> Pixel Grid</label>");
        sb.AppendLine("      <label><input type=\"color\" id=\"pixel-grid-color\" value=\"#404040\"></label>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <div class=\"svg-container\" id=\"svg-container\">");
        sb.AppendLine(svgContent);
        sb.AppendLine($"      <div class=\"cell-highlight\" id=\"cell-highlight\"></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"tooltip\" id=\"tooltip\"></div>");
        sb.AppendLine();
        sb.AppendLine("  <script>");
        sb.AppendLine($"    const BASE_CELL_WIDTH = {cellWidth};");
        sb.AppendLine($"    const BASE_CELL_HEIGHT = {cellHeight};");
        sb.AppendLine($"    const SVG_WIDTH = {svgWidth};");
        sb.AppendLine($"    const SVG_HEIGHT = {svgHeight};");
        sb.AppendLine($"    const COLS = {region.Width};");
        sb.AppendLine($"    const ROWS = {region.Height};");
        sb.AppendLine($"    const cellData = {cellData};");
        sb.AppendLine();
        sb.AppendLine("    const container = document.getElementById('svg-container');");
        sb.AppendLine("    const highlight = document.getElementById('cell-highlight');");
        sb.AppendLine("    const tooltip = document.getElementById('tooltip');");
        sb.AppendLine("    const svg = container.querySelector('svg');");
        sb.AppendLine();
        sb.AppendLine("    // Track current cell for shimmer effect");
        sb.AppendLine("    let currentCellX = -1;");
        sb.AppendLine("    let currentCellY = -1;");
        sb.AppendLine();
        sb.AppendLine("    // Scale SVG to fill container while maintaining aspect ratio");
        sb.AppendLine("    function updateSvgScale() {");
        sb.AppendLine("      const containerRect = container.getBoundingClientRect();");
        sb.AppendLine("      const padding = 40; // 20px padding on each side");
        sb.AppendLine("      const availableWidth = containerRect.width - padding;");
        sb.AppendLine("      const availableHeight = containerRect.height - padding;");
        sb.AppendLine("      const scaleX = availableWidth / SVG_WIDTH;");
        sb.AppendLine("      const scaleY = availableHeight / SVG_HEIGHT;");
        sb.AppendLine("      const scale = Math.min(scaleX, scaleY);");
        sb.AppendLine("      svg.style.width = (SVG_WIDTH * scale) + 'px';");
        sb.AppendLine("      svg.style.height = (SVG_HEIGHT * scale) + 'px';");
        sb.AppendLine("    }");
        sb.AppendLine("    updateSvgScale();");
        sb.AppendLine("    window.addEventListener('resize', updateSvgScale);");
        sb.AppendLine();
        sb.AppendLine("    function rgbToHex(r, g, b) {");
        sb.AppendLine("      return '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('');");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function rgbToHsl(r, g, b) {");
        sb.AppendLine("      r /= 255; g /= 255; b /= 255;");
        sb.AppendLine("      const max = Math.max(r, g, b), min = Math.min(r, g, b);");
        sb.AppendLine("      let h, s, l = (max + min) / 2;");
        sb.AppendLine("      if (max === min) { h = s = 0; }");
        sb.AppendLine("      else {");
        sb.AppendLine("        const d = max - min;");
        sb.AppendLine("        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);");
        sb.AppendLine("        switch (max) {");
        sb.AppendLine("          case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;");
        sb.AppendLine("          case g: h = ((b - r) / d + 2) / 6; break;");
        sb.AppendLine("          case b: h = ((r - g) / d + 4) / 6; break;");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("      return `hsl(${Math.round(h * 360)}, ${Math.round(s * 100)}%, ${Math.round(l * 100)}%)`;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function getAttributeBadges(attrs) {");
        sb.AppendLine("      const badges = [];");
        sb.AppendLine("      if (attrs & 1) badges.push('<span class=\"attr-badge bold\">Bold</span>');");
        sb.AppendLine("      if (attrs & 2) badges.push('<span class=\"attr-badge dim\">Dim</span>');");
        sb.AppendLine("      if (attrs & 4) badges.push('<span class=\"attr-badge italic\">Italic</span>');");
        sb.AppendLine("      if (attrs & 8) badges.push('<span class=\"attr-badge underline\">Underline</span>');");
        sb.AppendLine("      if (attrs & 16) badges.push('<span class=\"attr-badge blink\">Blink</span>');");
        sb.AppendLine("      if (attrs & 32) badges.push('<span class=\"attr-badge reverse\">Reverse</span>');");
        sb.AppendLine("      if (attrs & 64) badges.push('<span class=\"attr-badge hidden\">Hidden</span>');");
        sb.AppendLine("      if (attrs & 128) badges.push('<span class=\"attr-badge strikethrough\">Strike</span>');");
        sb.AppendLine("      if (attrs & 256) badges.push('<span class=\"attr-badge overline\">Overline</span>');");
        sb.AppendLine("      return badges.length > 0 ? badges.join('') : '<span style=\"color:#666\">None</span>';");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function formatColorInfo(color, label) {");
        sb.AppendLine("      if (!color) return `<span style=\"color:#666\">Default</span>`;");
        sb.AppendLine("      const hex = rgbToHex(color.r, color.g, color.b);");
        sb.AppendLine("      const hsl = rgbToHsl(color.r, color.g, color.b);");
        sb.AppendLine("      return `<span class=\"color-swatch\" style=\"background:${hex}\"></span>${hex}<br>` +");
        sb.AppendLine("             `<span style=\"color:#888;font-size:11px\">rgb(${color.r}, ${color.g}, ${color.b})<br>${hsl}</span>`;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function renderTooltip(cell, x, y) {");
        sb.AppendLine("      const char = cell.c === ' ' ? '␣' : (cell.c === '\\0' ? '∅' : (cell.c === '' ? '⋯' : cell.c));");
        sb.AppendLine("      const codePoint = cell.c.codePointAt(0) || 0;");
        sb.AppendLine("      const fgStyle = cell.fg ? `color:rgb(${cell.fg.r},${cell.fg.g},${cell.fg.b});` : '';");
        sb.AppendLine("      const bgStyle = cell.bg ? `background:rgb(${cell.bg.r},${cell.bg.g},${cell.bg.b});` : 'background:#1e1e1e;';");
        sb.AppendLine("      const seqInfo = cell.seq ? `Seq: ${cell.seq}` : 'Seq: 0';");
        sb.AppendLine("      const timeInfo = cell.t ? new Date(cell.t).toLocaleTimeString() : '-';");
        sb.AppendLine("      const hasSixel = cell.sixel;");
        sb.AppendLine();
        sb.AppendLine("      return `");
        sb.AppendLine("        <div class=\"tooltip-header\">");
        sb.AppendLine("          <div class=\"tooltip-char\" style=\"${fgStyle}${bgStyle}\">${char}</div>");
        sb.AppendLine("          <div>");
        sb.AppendLine("            <div style=\"font-size:16px;font-weight:500\">Cell (${x}, ${y})</div>");
        sb.AppendLine("            <div class=\"tooltip-position\">Column ${x}, Row ${y}</div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"tooltip-section\">");
        sb.AppendLine("          <div class=\"tooltip-label\">Character</div>");
        sb.AppendLine("          <div class=\"tooltip-value\">");
        sb.AppendLine("            '${cell.c === '\\0' ? '\\\\0 (null)' : (cell.c === '' ? '(continuation)' : cell.c)}' &nbsp;|&nbsp; ");
        sb.AppendLine("            U+${codePoint.toString(16).toUpperCase().padStart(4, '0')} &nbsp;|&nbsp; ");
        sb.AppendLine("            Dec: ${codePoint}");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"tooltip-row\">");
        sb.AppendLine("          <div class=\"tooltip-section\">");
        sb.AppendLine("            <div class=\"tooltip-label\">Foreground</div>");
        sb.AppendLine("            <div class=\"tooltip-value\">${formatColorInfo(cell.fg, 'fg')}</div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"tooltip-section\">");
        sb.AppendLine("            <div class=\"tooltip-label\">Background</div>");
        sb.AppendLine("            <div class=\"tooltip-value\">${formatColorInfo(cell.bg, 'bg')}</div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"tooltip-section\">");
        sb.AppendLine("          <div class=\"tooltip-label\">Attributes (${cell.a})</div>");
        sb.AppendLine("          <div class=\"tooltip-value\">${getAttributeBadges(cell.a)}</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"tooltip-section\">");
        sb.AppendLine("          <div class=\"tooltip-label\">Write Order</div>");
        sb.AppendLine("          <div class=\"tooltip-value\">${seqInfo} &nbsp;|&nbsp; ${timeInfo}</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        ${hasSixel ? `");
        sb.AppendLine("        <div class=\"tooltip-section\">");
        sb.AppendLine("          <div class=\"tooltip-label\">Sixel Graphics</div>");
        sb.AppendLine("          <div class=\"tooltip-value\">");
        sb.AppendLine("            ${cell.sixel.origin ? '<span class=\"attr-badge\" style=\"background:#4e9a06\">Origin</span>' : '<span class=\"attr-badge\">Continuation</span>'}");
        sb.AppendLine("            ${cell.sixel.w}×${cell.sixel.h} cells");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>` : ''}");
        sb.AppendLine("      `;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    container.addEventListener('mousemove', (e) => {");
        sb.AppendLine("      const svgRect = svg.getBoundingClientRect();");
        sb.AppendLine("      const scaleX = svgRect.width / SVG_WIDTH;");
        sb.AppendLine("      const scaleY = svgRect.height / SVG_HEIGHT;");
        sb.AppendLine("      const cellWidth = BASE_CELL_WIDTH * scaleX;");
        sb.AppendLine("      const cellHeight = BASE_CELL_HEIGHT * scaleY;");
        sb.AppendLine("      const x = Math.floor((e.clientX - svgRect.left) / cellWidth);");
        sb.AppendLine("      const y = Math.floor((e.clientY - svgRect.top) / cellHeight);");
        sb.AppendLine();
        sb.AppendLine("      if (x >= 0 && x < COLS && y >= 0 && y < ROWS) {");
        sb.AppendLine("        const cell = cellData[y][x];");
        sb.AppendLine();
        sb.AppendLine("        // Check if we moved to a new cell");
        sb.AppendLine("        const cellChanged = (x !== currentCellX || y !== currentCellY);");
        sb.AppendLine("        currentCellX = x;");
        sb.AppendLine("        currentCellY = y;");
        sb.AppendLine();
        sb.AppendLine("        // Position highlight relative to SVG");
        sb.AppendLine("        const svgOffset = svg.getBoundingClientRect();");
        sb.AppendLine("        const containerOffset = container.getBoundingClientRect();");
        sb.AppendLine("        highlight.style.left = (svgOffset.left - containerOffset.left + x * cellWidth) + 'px';");
        sb.AppendLine("        highlight.style.top = (svgOffset.top - containerOffset.top + y * cellHeight) + 'px';");
        sb.AppendLine("        highlight.style.width = cellWidth + 'px';");
        sb.AppendLine("        highlight.style.height = cellHeight + 'px';");
        sb.AppendLine("        highlight.classList.add('visible');");
        sb.AppendLine();
        sb.AppendLine("        // Shimmer effect when moving to a new cell");
        sb.AppendLine("        if (cellChanged) {");
        sb.AppendLine("          highlight.classList.add('shimmer');");
        sb.AppendLine("          setTimeout(() => highlight.classList.remove('shimmer'), 200);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Update and position tooltip");
        sb.AppendLine("        tooltip.innerHTML = renderTooltip(cell, x, y);");
        sb.AppendLine("        tooltip.classList.add('visible');");
        sb.AppendLine();
        sb.AppendLine("        // Position tooltip to avoid viewport edges");
        sb.AppendLine("        let tooltipX = e.clientX + 15;");
        sb.AppendLine("        let tooltipY = e.clientY + 15;");
        sb.AppendLine("        const tooltipRect = tooltip.getBoundingClientRect();");
        sb.AppendLine("        if (tooltipX + tooltipRect.width > window.innerWidth - 10) {");
        sb.AppendLine("          tooltipX = e.clientX - tooltipRect.width - 15;");
        sb.AppendLine("        }");
        sb.AppendLine("        if (tooltipY + tooltipRect.height > window.innerHeight - 10) {");
        sb.AppendLine("          tooltipY = e.clientY - tooltipRect.height - 15;");
        sb.AppendLine("        }");
        sb.AppendLine("        tooltip.style.left = tooltipX + 'px';");
        sb.AppendLine("        tooltip.style.top = tooltipY + 'px';");
        sb.AppendLine("      } else {");
        sb.AppendLine("        highlight.classList.remove('visible');");
        sb.AppendLine("        tooltip.classList.remove('visible');");
        sb.AppendLine("        currentCellX = -1;");
        sb.AppendLine("        currentCellY = -1;");
        sb.AppendLine("      }");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    container.addEventListener('mouseleave', () => {");
        sb.AppendLine("      highlight.classList.remove('visible');");
        sb.AppendLine("      tooltip.classList.remove('visible');");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    // Theme controls");
        sb.AppendLine("    const bgPicker = document.getElementById('bg-picker');");
        sb.AppendLine("    const fgPicker = document.getElementById('fg-picker');");
        sb.AppendLine("    const btnDark = document.getElementById('btn-dark');");
        sb.AppendLine("    const btnLight = document.getElementById('btn-light');");
        sb.AppendLine("    const cellGridToggle = document.getElementById('cell-grid-toggle');");
        sb.AppendLine("    const pixelGridToggle = document.getElementById('pixel-grid-toggle');");
        sb.AppendLine("    const cellGridColorPicker = document.getElementById('cell-grid-color');");
        sb.AppendLine("    const pixelGridColorPicker = document.getElementById('pixel-grid-color');");
        sb.AppendLine();
        sb.AppendLine("    let currentDefaultBg = '#1e1e1e';");
        sb.AppendLine("    let currentDefaultFg = '#d4d4d4';");
        sb.AppendLine("    let cellGridCustomColor = false;");
        sb.AppendLine("    let pixelGridCustomColor = false;");
        sb.AppendLine();
        sb.AppendLine("    // Compute contrasting color for grids based on background luminance");
        sb.AppendLine("    function getContrastingGridColor(bgColor, alpha) {");
        sb.AppendLine("      const hex = bgColor.replace('#', '');");
        sb.AppendLine("      const r = parseInt(hex.substr(0, 2), 16);");
        sb.AppendLine("      const g = parseInt(hex.substr(2, 2), 16);");
        sb.AppendLine("      const b = parseInt(hex.substr(4, 2), 16);");
        sb.AppendLine("      const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;");
        sb.AppendLine("      const base = luminance > 0.5 ? 0 : 255;");
        sb.AppendLine("      const adjusted = Math.round(base * alpha + (luminance > 0.5 ? 255 : 0) * (1 - alpha));");
        sb.AppendLine("      const hex2 = adjusted.toString(16).padStart(2, '0');");
        sb.AppendLine("      return `#${hex2}${hex2}${hex2}`;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function updateGridColors(bgColor) {");
        sb.AppendLine("      if (!cellGridCustomColor) {");
        sb.AppendLine("        const cellColor = getContrastingGridColor(bgColor, 0.5);");
        sb.AppendLine("        cellGridColorPicker.value = cellColor;");
        sb.AppendLine("        svg.querySelectorAll('.cell-grid line').forEach(el => el.setAttribute('stroke', cellColor));");
        sb.AppendLine("      }");
        sb.AppendLine("      if (!pixelGridCustomColor) {");
        sb.AppendLine("        const pixelColor = getContrastingGridColor(bgColor, 0.25);");
        sb.AppendLine("        pixelGridColorPicker.value = pixelColor;");
        sb.AppendLine("        svg.querySelectorAll('.pixel-grid line').forEach(el => el.setAttribute('stroke', pixelColor));");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    function updateTheme(newBg, newFg) {");
        sb.AppendLine("      // Update the main background rect (first rect in SVG)");
        sb.AppendLine("      const bgRect = svg.querySelector('rect');");
        sb.AppendLine("      if (bgRect) bgRect.setAttribute('fill', newBg);");
        sb.AppendLine();
        sb.AppendLine("      // Update all cell background rects that use default background");
        sb.AppendLine("      const allRects = svg.querySelectorAll('g.terminal-text rect');");
        sb.AppendLine("      allRects.forEach(rect => {");
        sb.AppendLine("        const currentFill = rect.getAttribute('fill');");
        sb.AppendLine("        if (currentFill === currentDefaultBg) {");
        sb.AppendLine("          rect.setAttribute('fill', newBg);");
        sb.AppendLine("        }");
        sb.AppendLine("      });");
        sb.AppendLine();
        sb.AppendLine("      // Update text elements using default foreground");
        sb.AppendLine("      const allText = svg.querySelectorAll('g.terminal-text text');");
        sb.AppendLine("      allText.forEach(text => {");
        sb.AppendLine("        const currentFill = text.getAttribute('fill');");
        sb.AppendLine("        if (currentFill === currentDefaultFg) {");
        sb.AppendLine("          text.setAttribute('fill', newFg);");
        sb.AppendLine("        }");
        sb.AppendLine("      });");
        sb.AppendLine();
        sb.AppendLine("      currentDefaultBg = newBg;");
        sb.AppendLine("      currentDefaultFg = newFg;");
        sb.AppendLine("      bgPicker.value = newBg;");
        sb.AppendLine("      fgPicker.value = newFg;");
        sb.AppendLine();
        sb.AppendLine("      // Update grid colors based on new background");
        sb.AppendLine("      updateGridColors(newBg);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    bgPicker.addEventListener('input', (e) => {");
        sb.AppendLine("      updateTheme(e.target.value, currentDefaultFg);");
        sb.AppendLine("      btnDark.classList.remove('active');");
        sb.AppendLine("      btnLight.classList.remove('active');");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    fgPicker.addEventListener('input', (e) => {");
        sb.AppendLine("      updateTheme(currentDefaultBg, e.target.value);");
        sb.AppendLine("      btnDark.classList.remove('active');");
        sb.AppendLine("      btnLight.classList.remove('active');");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    btnDark.addEventListener('click', () => {");
        sb.AppendLine("      updateTheme('#1e1e1e', '#d4d4d4');");
        sb.AppendLine("      btnDark.classList.add('active');");
        sb.AppendLine("      btnLight.classList.remove('active');");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    btnLight.addEventListener('click', () => {");
        sb.AppendLine("      updateTheme('#ffffff', '#1e1e1e');");
        sb.AppendLine("      btnLight.classList.add('active');");
        sb.AppendLine("      btnDark.classList.remove('active');");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    // Grid toggle and color controls");
        sb.AppendLine("    cellGridToggle.addEventListener('change', (e) => {");
        sb.AppendLine("      container.classList.toggle('show-cell-grid', e.target.checked);");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    pixelGridToggle.addEventListener('change', (e) => {");
        sb.AppendLine("      container.classList.toggle('show-pixel-grid', e.target.checked);");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    cellGridColorPicker.addEventListener('input', (e) => {");
        sb.AppendLine("      cellGridCustomColor = true;");
        sb.AppendLine("      svg.querySelectorAll('.cell-grid line').forEach(el => el.setAttribute('stroke', e.target.value));");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    pixelGridColorPicker.addEventListener('input', (e) => {");
        sb.AppendLine("      pixelGridCustomColor = true;");
        sb.AppendLine("      svg.querySelectorAll('.pixel-grid line').forEach(el => el.setAttribute('stroke', e.target.value));");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    // Initialize grid colors based on default background");
        sb.AppendLine("    updateGridColors(currentDefaultBg);");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string BuildCellDataJson(IHex1bTerminalRegion region)
    {
        var rows = new List<string>();

        for (int y = 0; y < region.Height; y++)
        {
            var cells = new List<string>();
            for (int x = 0; x < region.Width; x++)
            {
                var cell = region.GetCell(x, y);
                var ch = cell.Character ?? "";
                
                // Escape special JSON characters in the grapheme string
                var escapedChar = EscapeJsonString(ch);

                var fg = cell.Foreground.HasValue
                    ? $"{{\"r\":{cell.Foreground.Value.R},\"g\":{cell.Foreground.Value.G},\"b\":{cell.Foreground.Value.B}}}"
                    : "null";

                var bg = cell.Background.HasValue
                    ? $"{{\"r\":{cell.Background.Value.R},\"g\":{cell.Background.Value.G},\"b\":{cell.Background.Value.B}}}"
                    : "null";

                var attrs = (int)cell.Attributes;
                var seq = cell.Sequence;
                var writtenAt = cell.WrittenAt != default 
                    ? $"\"{cell.WrittenAt:O}\"" 
                    : "null";

                // Include sixel data if present
                var sixel = cell.SixelData != null
                    ? $"{{\"origin\":{(cell.IsSixel ? "true" : "false")},\"w\":{cell.SixelData.WidthInCells},\"h\":{cell.SixelData.HeightInCells}}}"
                    : "null";

                cells.Add($"{{\"c\":\"{escapedChar}\",\"fg\":{fg},\"bg\":{bg},\"a\":{attrs},\"seq\":{seq},\"t\":{writtenAt},\"sixel\":{sixel}}}");
            }
            rows.Add($"[{string.Join(",", cells)}]");
        }

        return $"[\n      {string.Join(",\n      ", rows)}\n    ]";
    }

    /// <summary>
    /// Escapes a string for JSON encoding.
    /// </summary>
    private static string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        var sb = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\u0000",
                < ' ' => $"\\u{(int)ch:x4}",
                _ => ch.ToString()
            });
        }
        return sb.ToString();
    }
}
