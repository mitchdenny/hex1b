namespace Hex1b;

using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Objects;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

/// <summary>
/// Extension methods for Scene widget integration.
/// </summary>
public static class SceneExtensions
{
    /// <summary>
    /// Create a scene widget that renders a 3D scene from a camera's perspective.
    /// Usage: context.Scene(scene, camera)
    /// </summary>
    public static SceneWidget Scene<TParentWidget>(this WidgetContext<TParentWidget> context, SceneClass scene, SceneCamera camera)
        where TParentWidget : Hex1bWidget
    {
        return new SceneWidget(scene, camera);
    }
}


