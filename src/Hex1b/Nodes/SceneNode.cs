namespace Hex1b;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Rendering;
using Hex1b.Scene.Textures;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

/// <summary>
/// Node that renders a 3D scene from a camera's perspective.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneNode : Hex1bNode
{
    private SceneRenderer _renderer = new();
    private SceneClass? _scene;
    private SceneCamera? _camera;
    private readonly Dictionary<SceneTextureMaterial, WidgetTextureSlot> _widgetSlots = new();

    public SceneClass? Scene 
    { 
        get => _scene; 
        set 
        { 
            if (_scene != value) 
            { 
                _scene = value; 
                MarkDirty(); 
            } 
        } 
    }

    public SceneCamera? Camera 
    { 
        get => _camera; 
        set 
        { 
            if (_camera != value) 
            { 
                _camera = value; 
                MarkDirty(); 
            } 
        } 
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        return constraints.Constrain(new(constraints.MaxWidth, constraints.MaxHeight));
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Scene == null || Camera == null)
            return;

        // Refresh any widget-backed textures before rasterizing the scene. This requires a
        // live surface context because the source widgets must render through the host app's
        // pipeline; without one we simply skip the update and reuse the previous texture.
        if (context is SurfaceRenderContext surfaceCtx)
            UpdateWidgetTextures(surfaceCtx);

        var solidMode = ContainsSolidMesh(Scene);
        var rasterWidth = solidMode ? Bounds.Width : Bounds.Width * 2;
        var rasterHeight = solidMode ? Bounds.Height * 2 : Bounds.Height * 4;
        var rasterizerContext = new SceneRasterizerContext(rasterWidth, rasterHeight);

        _renderer.Render(Scene, Camera, rasterizerContext);

        if (solidMode)
        {
            RenderHalfBlockToTerminal(context, rasterizerContext);
            return;
        }

        RenderBrailleToTerminal(context, rasterizerContext);
    }

    /// <summary>
    /// Reconciles every widget bound to a <see cref="SceneTextureMaterial.WidgetSource"/> in
    /// the current scene into a cached offscreen node. These nodes are intentionally hidden
    /// from <see cref="Hex1bNode.GetChildren"/> so they never participate in focus, input, or
    /// the host layout pass — they exist only to be rendered into textures.
    /// </summary>
    internal async Task ReconcileWidgetSourcesAsync(ReconcileContext context)
    {
        if (Scene is null)
        {
            _widgetSlots.Clear();
            return;
        }

        var materials = new List<SceneTextureMaterial>();
        CollectWidgetSources(Scene, materials);

        if (materials.Count == 0)
        {
            _widgetSlots.Clear();
            return;
        }

        foreach (var stale in _widgetSlots.Keys.Where(m => !materials.Contains(m)).ToList())
            _widgetSlots.Remove(stale);

        foreach (var material in materials)
        {
            if (material.WidgetSource is null)
                continue;

            if (!_widgetSlots.TryGetValue(material, out var slot))
            {
                slot = new WidgetTextureSlot();
                _widgetSlots[material] = slot;
            }

            slot.Node = await context.ReconcileChildAsync(slot.Node, material.WidgetSource, this);
        }
    }

    private static void CollectWidgetSources(SceneObject obj, List<SceneTextureMaterial> result)
    {
        if (obj is SceneMesh mesh && mesh.Material is SceneTextureMaterial texture && texture.WidgetSource is not null)
            result.Add(texture);

        foreach (var child in obj.Children)
            CollectWidgetSources(child, result);
    }

    private void UpdateWidgetTextures(SurfaceRenderContext surfaceCtx)
    {
        if (_widgetSlots.Count == 0)
            return;

        const int cellPixelWidth = 2;
        const int cellPixelHeight = 2;

        foreach (var (material, slot) in _widgetSlots)
        {
            if (slot.Node is null)
                continue;

            var columns = System.Math.Max(1, material.WidgetSourceColumns);
            var rows = System.Math.Max(1, material.WidgetSourceRows);
            var node = slot.Node;

            node.Measure(Constraints.Tight(columns, rows));
            node.Arrange(new Rect(0, 0, columns, rows));

            var pool = surfaceCtx.SurfacePool;
            var surface = pool != null
                ? pool.Rent(columns, rows, surfaceCtx.CellMetrics)
                : new Surface(columns, rows, surfaceCtx.CellMetrics);

            try
            {
                var tempContext = new SurfaceRenderContext(
                    surface, 0, 0, surfaceCtx.Theme, surfaceCtx.TrackedObjectStore)
                {
                    CachingEnabled = surfaceCtx.CachingEnabled,
                    MouseX = surfaceCtx.MouseX,
                    MouseY = surfaceCtx.MouseY,
                    CellMetrics = surfaceCtx.CellMetrics,
                    SurfacePool = pool
                };

                tempContext.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
                tempContext.RenderChild(node);

                if (slot.Texture is null || slot.Columns != columns || slot.Rows != rows)
                {
                    slot.Texture = new SceneTexture2D(columns * cellPixelWidth, rows * cellPixelHeight);
                    slot.Columns = columns;
                    slot.Rows = rows;
                }

                SurfaceCellTextureSampler.SampleInto(slot.Texture, surface, cellPixelWidth, cellPixelHeight);
                material.Texture = slot.Texture;
            }
            finally
            {
                if (pool != null)
                    pool.Return(surface);
            }
        }
    }

    private bool ContainsSolidMesh(SceneObject obj)
    {
        if (obj is SceneMesh mesh && mesh.Material is SceneMeshMaterial)
            return true;

        foreach (var child in obj.Children)
        {
            if (ContainsSolidMesh(child))
                return true;
        }

        return false;
    }

    private void RenderBrailleToTerminal(Hex1bRenderContext context, SceneRasterizerContext rasterizerContext)
    {
        var reset = context.Theme.GetResetToGlobalCodes();

        for (var cellY = 0; cellY < Bounds.Height; cellY++)
        {
            for (var cellX = 0; cellX < Bounds.Width; cellX++)
            {
                var brailleBits = 0;
                var colorSum = Vector3.Zero;
                var colorCount = 0;

                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 0, (cellY * 4) + 0, 1 << 0, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 0, (cellY * 4) + 1, 1 << 1, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 0, (cellY * 4) + 2, 1 << 2, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 1, (cellY * 4) + 0, 1 << 3, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 1, (cellY * 4) + 1, 1 << 4, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 1, (cellY * 4) + 2, 1 << 5, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 0, (cellY * 4) + 3, 1 << 6, ref brailleBits, ref colorSum, ref colorCount);
                AccumulateBrailleDot(rasterizerContext, (cellX * 2) + 1, (cellY * 4) + 3, 1 << 7, ref brailleBits, ref colorSum, ref colorCount);

                var output = " ";
                if (brailleBits != 0)
                {
                    var averageColor = colorCount > 0 ? colorSum / colorCount : Vector3.One;
                    var fg = ToHexColor(averageColor);
                    output = $"{fg.ToForegroundAnsi()}{char.ConvertFromUtf32(0x2800 + brailleBits)}{reset}";
                }

                context.WriteClipped(Bounds.X + cellX, Bounds.Y + cellY, output);
            }
        }
    }

    private void RenderHalfBlockToTerminal(Hex1bRenderContext context, SceneRasterizerContext rasterizerContext)
    {
        var reset = context.Theme.GetResetToGlobalCodes();

        for (var cellY = 0; cellY < Bounds.Height; cellY++)
        {
            for (var cellX = 0; cellX < Bounds.Width; cellX++)
            {
                var topPixel = GetPixel(rasterizerContext, cellX, (cellY * 2) + 0);
                var bottomPixel = GetPixel(rasterizerContext, cellX, (cellY * 2) + 1);
                var topOn = topPixel.W > 0;
                var bottomOn = bottomPixel.W > 0;

                string ch;
                if (!topOn && !bottomOn)
                {
                    ch = " ";
                }
                else if (topOn && bottomOn)
                {
                    var fg = ToHexColor(new Vector3(topPixel.X, topPixel.Y, topPixel.Z));
                    var bg = ToHexColor(new Vector3(bottomPixel.X, bottomPixel.Y, bottomPixel.Z));
                    ch = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}▀{reset}";
                }
                else if (topOn)
                {
                    var fg = ToHexColor(new Vector3(topPixel.X, topPixel.Y, topPixel.Z));
                    ch = $"{fg.ToForegroundAnsi()}▀{reset}";
                }
                else
                {
                    var fg = ToHexColor(new Vector3(bottomPixel.X, bottomPixel.Y, bottomPixel.Z));
                    ch = $"{fg.ToForegroundAnsi()}▄{reset}";
                }

                context.WriteClipped(Bounds.X + cellX, Bounds.Y + cellY, ch);
            }
        }
    }

    private static Vector4 GetPixel(SceneRasterizerContext rasterizerContext, int x, int y)
    {
        if (!rasterizerContext.IsInViewport(x, y))
            return Vector4.Zero;
        return rasterizerContext.GetPixel(x, y);
    }

    private static void AccumulateBrailleDot(
        SceneRasterizerContext rasterizerContext,
        int x,
        int y,
        int bit,
        ref int brailleBits,
        ref Vector3 colorSum,
        ref int colorCount)
    {
        if (!rasterizerContext.IsInViewport(x, y))
            return;

        var pixel = rasterizerContext.GetPixel(x, y);
        if (pixel.W <= 0)
            return;

        brailleBits |= bit;
        colorSum += new Vector3(pixel.X, pixel.Y, pixel.Z);
        colorCount++;
    }

    private static Hex1bColor ToHexColor(Vector3 color)
    {
        static byte ToByte(float value)
        {
            var clamped = value < 0 ? 0 : value > 1 ? 1 : value;
            return (byte)(clamped * 255.0f);
        }

        return Hex1bColor.FromRgb(ToByte(color.X), ToByte(color.Y), ToByte(color.Z));
    }

    private sealed class WidgetTextureSlot
    {
        public Hex1bNode? Node { get; set; }
        public SceneTexture2D? Texture { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
    }
}
