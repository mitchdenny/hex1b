namespace Hex1b;

using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Rendering;
using Hex1b.Theming;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

/// <summary>
/// Node that renders a 3D scene from a camera's perspective.
/// </summary>
public class SceneNode : Hex1bNode
{
    private SceneRenderer _renderer = new();
    private SceneClass? _scene;
    private SceneCamera? _camera;

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
}
