namespace Hex1b.Widgets;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Nodes;
using Hex1b.Scene.Core;
using Hex1b.Scene.Objects;
using SceneClass = Hex1b.Scene.Core.Scene;

/// <summary>
/// A widget that renders a 3D scene from a camera's perspective into the terminal.
/// Usage: context.Scene(scene, camera)
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public record SceneWidget : Hex1bWidget
{
    public SceneClass Scene { get; init; }
    public SceneCamera Camera { get; init; }

    public SceneWidget(SceneClass scene, SceneCamera camera)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SceneNode ?? new SceneNode();

        // Mark dirty if properties changed
        if (node.Scene != Scene || node.Camera != Camera)
        {
            node.MarkDirty();
        }

        node.Scene = Scene;
        node.Camera = Camera;

        // Reconcile any widgets bound to texture materials so they can be rendered offscreen.
        await node.ReconcileWidgetSourcesAsync(context);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SceneNode);
}

