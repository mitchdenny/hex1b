using AnimatedHumanoidDemo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatedHumanoidDemo.Tests;

[TestClass]
public class AnimatedHumanoidTests
{
    [TestMethod]
    public void Load_BundledRobot_PreservesTopologyAndNormalizesHeight()
    {
        var model = new AnimatedHumanoid(AnimatedHumanoid.AssetPath);
        Assert.AreEqual(7214, model.VertexCount);
        Assert.AreEqual(3234, model.TriangleCount);
        Assert.HasCount(14, model.Animations);
        var positions = Positions(model);
        var heights = positions.Where((_, index) => index % 3 == 1).ToArray();
        Assert.AreEqual(0, heights.Min(), 0.0001);
        Assert.AreEqual(3, heights.Max(), 0.0001);
    }

    [TestMethod]
    [DataRow("Idle")]
    [DataRow("Walking")]
    [DataRow("Running")]
    [DataRow("Dance")]
    [DataRow("Wave")]
    [DataRow("Jump")]
    public void SetAnimationFrame_DifferentTimes_DeformsExistingVertexBuffers(string animation)
    {
        var model = new AnimatedHumanoid(AnimatedHumanoid.AssetPath);
        var buffers = model.Meshes.Select(m => m.Geometry.GetAttribute("position")!.Data).ToArray();
        model.SetAnimationFrame(animation, 0, false);
        var initial = Positions(model);
        model.SetAnimationFrame(animation, 0.4f, false);
        var animated = Positions(model);

        Assert.IsTrue(animated.All(float.IsFinite));
        Assert.IsTrue(initial.Zip(animated).Any(pair => Math.Abs(pair.First - pair.Second) > 0.01f),
            $"{animation} must change the model, not just the status label.");
        for (var i = 0; i < buffers.Length; i++)
            Assert.AreSame(buffers[i], model.Meshes[i].Geometry.GetAttribute("position")!.Data);
    }

    [TestMethod]
    public void SetAnimationFrame_LoopingClip_RepeatsPose()
    {
        var model = new AnimatedHumanoid(AnimatedHumanoid.AssetPath);
        var duration = model.Animations.Single(a => a.Name == "Walking").Duration;
        model.SetAnimationFrame("Walking", 0.25f, true);
        var first = Positions(model);
        model.SetAnimationFrame("Walking", duration + 0.25f, true);
        AssertPositionsEqual(first, Positions(model));
    }

    [TestMethod]
    public void SetAnimationFrame_OneShotClip_HoldsFinalPose()
    {
        var model = new AnimatedHumanoid(AnimatedHumanoid.AssetPath);
        var duration = model.Animations.Single(a => a.Name == "Wave").Duration;
        model.SetAnimationFrame("Wave", duration, false);
        var final = Positions(model);
        model.SetAnimationFrame("Wave", duration + 1, false);
        AssertPositionsEqual(final, Positions(model));
    }

    [TestMethod]
    public void SetAnimationFrame_SwitchingClips_DoesNotInheritPreviousPose()
    {
        var model = new AnimatedHumanoid(AnimatedHumanoid.AssetPath);
        model.SetAnimationFrame("Idle", 0, true);
        var idle = Positions(model);
        model.SetAnimationFrame("Jump", 0.4f, false);
        model.SetAnimationFrame("Idle", 0, true);
        AssertPositionsEqual(idle, Positions(model));
    }

    [TestMethod]
    public void SetAnimationFrame_InvalidInputs_Throws()
    {
        var model = new AnimatedHumanoid(AnimatedHumanoid.AssetPath);
        Assert.ThrowsExactly<ArgumentException>(() => model.SetAnimationFrame("Missing", 0, true));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => model.SetAnimationFrame("Idle", -1, true));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => model.SetAnimationFrame("Idle", float.NaN, true));
    }

    private static float[] Positions(AnimatedHumanoid model)
        => model.Meshes.SelectMany(m => m.Geometry.GetAttribute("position")!.Data).ToArray();

    private static void AssertPositionsEqual(float[] expected, float[] actual)
    {
        Assert.HasCount(expected.Length, actual);
        for (var i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], actual[i], 0.0001f, $"Component {i}");
    }
}
