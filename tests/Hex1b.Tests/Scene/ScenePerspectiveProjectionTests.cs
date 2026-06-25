#pragma warning disable HEX1B_SCENE // Tests exercise the experimental Scene API
namespace Hex1b.Tests.Scene;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hex1b.Scene.Math;

[TestClass]
public class ScenePerspectiveProjectionTests
{
    // Regression: Matrix4.Perspective built on top of an identity matrix once left element
    // [3][3] = 1, so the homogeneous w came out as (-viewZ + 1) instead of -viewZ. That extra
    // +1 is negligible for distant objects but dominates close-ups (e.g. a camera diving onto a
    // sphere for deep zoom), collapsing geometry to a speck. A correct perspective matrix must
    // have [3][3] == 0 so the perspective divide uses the true camera-space depth.
    [TestMethod]
    [DataRow(0.05f)]
    [DataRow(0.5f)]
    [DataRow(5.0f)]
    public void Perspective_ClipW_EqualsCameraSpaceDepth(float depth)
    {
        var proj = Matrix4.Perspective(MathF.PI / 4f, 1.0f, 0.01f, 50f);

        // A point straight ahead at camera-space depth `depth` (camera looks down -Z).
        var clip = proj * new Vector4(0f, 0f, -depth, 1f);

        Assert.AreEqual(depth, clip.W, 1e-4f,
            "Perspective clip-space w must equal the camera-space depth (element [3][3] must be 0).");
    }

    [TestMethod]
    public void Perspective_BottomRow_IsPureProjective()
    {
        var proj = Matrix4.Perspective(MathF.PI / 4f, 1.0f, 0.01f, 50f);

        Assert.AreEqual(0f, proj.Get(3, 0), 1e-6f);
        Assert.AreEqual(0f, proj.Get(3, 1), 1e-6f);
        Assert.AreEqual(-1f, proj.Get(3, 2), 1e-6f);
        Assert.AreEqual(0f, proj.Get(3, 3), 1e-6f);
    }

    // A close-up, off-axis point should project well away from the optical axis. With the old
    // +1 bug its NDC x was divided by ~(depth + 1) instead of depth, pulling it almost to the
    // centre and shrinking every close object to a dot.
    [TestMethod]
    public void Perspective_CloseOffAxisPoint_ProjectsLarge()
    {
        var proj = Matrix4.Perspective(MathF.PI / 4f, 1.0f, 0.01f, 50f);

        // 0.02 to the right, 0.046 in front of the camera: ~24 deg off a 45 deg-FOV axis,
        // i.e. essentially at the edge of the viewport.
        var clip = proj * new Vector4(0.02f, 0f, -0.046f, 1f);
        var ndcX = clip.X / clip.W;

        Assert.IsTrue(ndcX > 0.9f,
            $"Expected a close off-axis point near the viewport edge (ndcX > 0.9) but got {ndcX:F3}.");
    }
}
