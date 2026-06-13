namespace Hex1b;

using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Rendering;
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

        // Create a rasterizer context for this viewport
        var rasterizerContext = new SceneRasterizerContext(Bounds.Width, Bounds.Height);

        // Render the scene
        _renderer.Render(Scene, Camera, rasterizerContext);

        // Convert raster output to terminal characters
        RenderRasterToTerminal(context, rasterizerContext);
    }

    private void RenderRasterToTerminal(Hex1bRenderContext context, SceneRasterizerContext rasterizerContext)
    {
        // Simple wireframe rendering using line-drawing characters
        // This is a placeholder - a real implementation would use more sophisticated ASCII art

        for (int y = 0; y < rasterizerContext.ViewportHeight && y < Bounds.Height; y++)
        {
            for (int x = 0; x < rasterizerContext.ViewportWidth && x < Bounds.Width; x++)
            {
                var pixel = rasterizerContext.GetPixel(x, y);
                if (pixel.W > 0) // Has content
                {
                    // Use different characters based on intensity
                    var intensity = (pixel.X + pixel.Y + pixel.Z) / 3.0f;
                    var ch = intensity < 0.25f ? ' ' :
                             intensity < 0.5f ? '·' :
                             intensity < 0.75f ? '▪' :
                             '█';

                    var cellX = Bounds.X + x;
                    var cellY = Bounds.Y + y;

                    context.WriteClipped(cellX, cellY, ch.ToString());
                }
            }
        }
    }
}



