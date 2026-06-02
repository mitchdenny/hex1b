using System.Globalization;
using System.Text;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the GraphemeHelper scan window (64 UTF-16 chars).
/// 
/// These tests verify correct behavior with:
/// - Real-world complex sequences (emoji ZWJ families, flags, combining marks)
/// - Sequences that fit within the 64-char scan window
/// - Adversarial sequences that exceed the 64-char window, confirming the
///   documented graceful degradation (cursor lands mid-cluster)
/// </summary>
[TestClass]
public class GraphemeBoundaryWindowTests
{
    // ── Real-world sequences that fit within the 64-char window ──────

    [TestMethod]
    public void PreviousCluster_Ascii_MovesOneChar()
    {
        var text = "Hello";
        Assert.AreEqual(3, GraphemeHelper.GetPreviousClusterBoundary(text, 4));
    }

    [TestMethod]
    public void NextCluster_Ascii_MovesOneChar()
    {
        var text = "Hello";
        Assert.AreEqual(2, GraphemeHelper.GetNextClusterBoundary(text, 1));
    }

    [TestMethod]
    public void PreviousCluster_SurrogatePairEmoji_SkipsEntirePair()
    {
        // 😀 = U+1F600 = 2 UTF-16 chars (surrogate pair)
        var text = "A😀B";
        // Cursor after 😀 (index 3), should skip back to index 1
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 3));
    }

    [TestMethod]
    public void NextCluster_SurrogatePairEmoji_SkipsEntirePair()
    {
        var text = "A😀B";
        // Cursor at 😀 start (index 1), should skip to index 3
        Assert.AreEqual(3, GraphemeHelper.GetNextClusterBoundary(text, 1));
    }

    [TestMethod]
    public void PreviousCluster_EmojiWithSkinTone_SkipsEntireSequence()
    {
        // 👍🏽 = U+1F44D U+1F3FD = 4 UTF-16 chars
        var emoji = "👍🏽";
        Assert.AreEqual(4, emoji.Length); // Verify our assumption
        var text = "X" + emoji + "Y";
        // Cursor after emoji (index 5), should skip back to index 1
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 5));
    }

    [TestMethod]
    public void NextCluster_EmojiWithSkinTone_SkipsEntireSequence()
    {
        var emoji = "👍🏽";
        var text = "X" + emoji + "Y";
        Assert.AreEqual(5, GraphemeHelper.GetNextClusterBoundary(text, 1));
    }

    [TestMethod]
    public void PreviousCluster_FlagEmoji_SkipsEntirePair()
    {
        // 🇺🇸 = U+1F1FA U+1F1F8 = 4 UTF-16 chars (2 regional indicators)
        var flag = "🇺🇸";
        Assert.AreEqual(4, flag.Length);
        var text = "A" + flag + "B";
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 5));
    }

    [TestMethod]
    public void NextCluster_FlagEmoji_SkipsEntirePair()
    {
        var flag = "🇺🇸";
        var text = "A" + flag + "B";
        Assert.AreEqual(5, GraphemeHelper.GetNextClusterBoundary(text, 1));
    }

    [TestMethod]
    public void PreviousCluster_CombiningDiacritical_SkipsBaseAndMark()
    {
        // e + combining acute accent = 2 chars, 1 grapheme cluster
        var text = "A" + "e\u0301" + "B";
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 3));
    }

    [TestMethod]
    public void NextCluster_CombiningDiacritical_SkipsBaseAndMark()
    {
        var text = "A" + "e\u0301" + "B";
        Assert.AreEqual(3, GraphemeHelper.GetNextClusterBoundary(text, 1));
    }

    [TestMethod]
    public void PreviousCluster_ZwjFamily_SkipsEntireSequence()
    {
        // 👨‍👩‍👧 = man ZWJ woman ZWJ girl = 8 UTF-16 chars
        var family = "👨\u200D👩\u200D👧";
        var text = "X" + family + "Y";
        var familyEnd = 1 + family.Length;
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, familyEnd));
    }

    [TestMethod]
    public void NextCluster_ZwjFamily_SkipsEntireSequence()
    {
        var family = "👨\u200D👩\u200D👧";
        var text = "X" + family + "Y";
        Assert.AreEqual(1 + family.Length, GraphemeHelper.GetNextClusterBoundary(text, 1));
    }

    [TestMethod]
    public void PreviousCluster_LargestStandardZwjFamily_FitsInWindow()
    {
        // 👨‍👩‍👧‍👦 = man ZWJ woman ZWJ girl ZWJ boy = 11 UTF-16 chars
        var family = "👨\u200D👩\u200D👧\u200D👦";
        Assert.AreEqual(11, family.Length);
        var text = "X" + family + "Y";
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 12));
    }

    [TestMethod]
    public void PreviousCluster_SkinTonedZwjFamily_FitsInWindow()
    {
        // Family with skin tones: 👨🏻‍👩🏽‍👧🏾‍👦🏿
        // Each person = 4 chars (surrogate pair + skin tone), ZWJ = 1 char each
        // Total = 4+1+4+1+4+1+4 = 19 UTF-16 chars
        var family = "👨🏻\u200D👩🏽\u200D👧🏾\u200D👦🏿";
        var text = "X" + family + "Y";
        var familyEnd = 1 + family.Length;
        Assert.IsTrue(family.Length <= 64, $"Skin-toned family should fit in window ({family.Length} chars)");
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, familyEnd));
    }

    [TestMethod]
    public void PreviousCluster_MultipleCombiningMarks_FitsInWindow()
    {
        // Base char + 10 combining diacritical marks = 11 chars, 1 cluster
        var sb = new StringBuilder();
        sb.Append('a');
        for (int i = 0; i < 10; i++)
            sb.Append('\u0308'); // combining diaeresis
        var zalgo = sb.ToString();
        Assert.AreEqual(11, zalgo.Length);
        var text = "X" + zalgo + "Y";
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 1 + zalgo.Length));
    }

    [TestMethod]
    public void PreviousCluster_HeavyZalgo30Marks_FitsInWindow()
    {
        // Base char + 30 combining marks = 31 chars, well within 64
        var sb = new StringBuilder();
        sb.Append('a');
        for (int i = 0; i < 30; i++)
            sb.Append('\u0308');
        var zalgo = sb.ToString();
        Assert.AreEqual(31, zalgo.Length);
        var text = "X" + zalgo + "Y";
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 1 + zalgo.Length));
    }

    [TestMethod]
    public void PreviousCluster_63CombiningMarks_FitsExactlyInWindow()
    {
        // Base char + 63 combining marks = 64 chars — exactly fits the window
        var sb = new StringBuilder();
        sb.Append('a');
        for (int i = 0; i < 63; i++)
            sb.Append('\u0308');
        var zalgo = sb.ToString();
        Assert.AreEqual(64, zalgo.Length);
        var text = "X" + zalgo + "Y";
        // Window scans back exactly 64 chars from cursor, capturing the full cluster
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 1 + zalgo.Length));
    }

    // ── Sequences that EXCEED the 64-char window ────────────────
    // These confirm the documented graceful degradation behavior:
    // the cursor lands mid-cluster instead of at the true start.

    [TestMethod]
    public void PreviousCluster_65CombiningMarks_ExceedsWindow_LandsMidCluster()
    {
        // Base char + 65 combining marks = 66 chars — exceeds 64-char window
        var sb = new StringBuilder();
        sb.Append('a');
        for (int i = 0; i < 65; i++)
            sb.Append('\u0308');
        var cluster = sb.ToString();
        Assert.AreEqual(66, cluster.Length);
        var text = "X" + cluster + "Y";
        var cursorPos = 1 + cluster.Length; // After the cluster

        var result = GraphemeHelper.GetPreviousClusterBoundary(text, cursorPos);

        // The window starts at cursorPos - 64, missing the base char 'a' at index 1.
        // The scan sees only combining marks, so it returns a position inside the
        // cluster rather than index 1. This is the expected degradation.
        Assert.IsTrue(result > 1, $"Expected cursor to land mid-cluster (>1) but got {result}. " +
            "Window should not reach back to the cluster start at index 1.");
        // Should not crash or return negative
        Assert.IsTrue(result >= 0);
        Assert.IsTrue(result < cursorPos, "Should move backward at least somewhat");
    }

    [TestMethod]
    public void PreviousCluster_100CombiningMarks_ExceedsWindow_LandsMidCluster()
    {
        // Base char + 100 combining marks = 101 chars — far exceeds window
        var sb = new StringBuilder();
        sb.Append('a');
        for (int i = 0; i < 100; i++)
            sb.Append('\u0308');
        var cluster = sb.ToString();
        Assert.AreEqual(101, cluster.Length);
        var text = "X" + cluster + "Y";
        var cursorPos = 1 + cluster.Length;

        var result = GraphemeHelper.GetPreviousClusterBoundary(text, cursorPos);

        Assert.IsTrue(result > 1, $"Expected mid-cluster landing (>1) but got {result}");
        Assert.IsTrue(result >= 0);
        Assert.IsTrue(result < cursorPos);
    }

    [TestMethod]
    public void NextCluster_ExceedsWindow_StillMovesForward()
    {
        // Forward scan: if cluster exceeds 64 chars, GetNextClusterBoundary still
        // reads the full cluster from StringInfo because the enumerator starts at
        // the cursor position and reads forward. The 64-char window truncates
        // the view, so StringInfo may report a shorter cluster.
        var sb = new StringBuilder();
        sb.Append('a');
        for (int i = 0; i < 100; i++)
            sb.Append('\u0308');
        var cluster = sb.ToString();
        var text = "X" + cluster + "Y";

        var result = GraphemeHelper.GetNextClusterBoundary(text, 1);

        // Should move forward past at least 64 chars (the window), but may not
        // reach the true cluster end if it exceeds the window
        Assert.IsTrue(result > 1, "Should advance from cursor position");
        // The window is 64 chars, so the enumerator sees at most 64 chars of the
        // cluster and may report that as one element
        Assert.IsTrue(result <= 1 + cluster.Length, "Should not overshoot past the cluster");
    }

    // ── Navigation through sequences of complex clusters ────────

    [TestMethod]
    public void NavigateForward_ThroughMixedClusters_EachStepCorrect()
    {
        // "A" + flag + skin-tone emoji + "B"
        var flag = "🇺🇸";        // 4 chars
        var thumb = "👍🏽";       // 4 chars
        var text = "A" + flag + thumb + "B";
        // Boundaries: 0, 1, 5, 9, 10

        var pos = 0;
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos); // skip 'A'
        Assert.AreEqual(1, pos);
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos); // skip flag
        Assert.AreEqual(5, pos);
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos); // skip thumb
        Assert.AreEqual(9, pos);
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos); // skip 'B'
        Assert.AreEqual(10, pos);
    }

    [TestMethod]
    public void NavigateBackward_ThroughMixedClusters_EachStepCorrect()
    {
        var flag = "🇺🇸";
        var thumb = "👍🏽";
        var text = "A" + flag + thumb + "B";

        var pos = text.Length; // 10
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos); // before 'B'
        Assert.AreEqual(9, pos);
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos); // before thumb
        Assert.AreEqual(5, pos);
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos); // before flag
        Assert.AreEqual(1, pos);
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos); // before 'A'
        Assert.AreEqual(0, pos);
    }

    [TestMethod]
    public void NavigateForward_MultipleFlagEmojis()
    {
        // 🇺🇸🇬🇧🇫🇷 — three flags, each 4 chars
        var text = "🇺🇸🇬🇧🇫🇷";
        Assert.AreEqual(12, text.Length);

        var pos = 0;
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos);
        Assert.AreEqual(4, pos);
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos);
        Assert.AreEqual(8, pos);
        pos = GraphemeHelper.GetNextClusterBoundary(text, pos);
        Assert.AreEqual(12, pos);
    }

    [TestMethod]
    public void NavigateBackward_MultipleFlagEmojis()
    {
        var text = "🇺🇸🇬🇧🇫🇷";

        var pos = text.Length;
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos);
        Assert.AreEqual(8, pos);
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos);
        Assert.AreEqual(4, pos);
        pos = GraphemeHelper.GetPreviousClusterBoundary(text, pos);
        Assert.AreEqual(0, pos);
    }

    // ── Edge cases ──────────────────────────────────────────────

    [TestMethod]
    public void PreviousCluster_EmptyString_ReturnsZero()
    {
        Assert.AreEqual(0, GraphemeHelper.GetPreviousClusterBoundary("", 0));
        Assert.AreEqual(0, GraphemeHelper.GetPreviousClusterBoundary("", 5));
    }

    [TestMethod]
    public void NextCluster_EmptyString_ReturnsZero()
    {
        Assert.AreEqual(0, GraphemeHelper.GetNextClusterBoundary("", 0));
    }

    [TestMethod]
    public void PreviousCluster_NullString_ReturnsZero()
    {
        Assert.AreEqual(0, GraphemeHelper.GetPreviousClusterBoundary(null!, 0));
    }

    [TestMethod]
    public void NextCluster_NullString_ReturnsZero()
    {
        Assert.AreEqual(0, GraphemeHelper.GetNextClusterBoundary(null!, 0));
    }

    [TestMethod]
    public void PreviousCluster_IndexBeyondEnd_ClampsToLength()
    {
        var text = "AB";
        Assert.AreEqual(1, GraphemeHelper.GetPreviousClusterBoundary(text, 100));
    }

    [TestMethod]
    public void NextCluster_IndexBeyondEnd_ReturnsLength()
    {
        var text = "AB";
        Assert.AreEqual(2, GraphemeHelper.GetNextClusterBoundary(text, 100));
    }

    [TestMethod]
    public void NextCluster_NegativeIndex_ClampsToZero()
    {
        var text = "AB";
        Assert.AreEqual(1, GraphemeHelper.GetNextClusterBoundary(text, -5));
    }

    [TestMethod]
    public void PreviousCluster_IndexAtZero_ReturnsZero()
    {
        var text = "Hello";
        Assert.AreEqual(0, GraphemeHelper.GetPreviousClusterBoundary(text, 0));
    }

    [TestMethod]
    public void NextCluster_IndexAtEnd_ReturnsLength()
    {
        var text = "Hello";
        Assert.AreEqual(5, GraphemeHelper.GetNextClusterBoundary(text, 5));
    }

    // ── Verify window boundary is exactly 64 ────────────────────

    [TestMethod]
    [DataRow(62, true)]  // 63 chars total (base + 62 marks) — within window
    [DataRow(63, true)]  // 64 chars total — exactly fits window
    [DataRow(64, false)] // 65 chars total — exceeds window by 1
    [DataRow(70, false)] // 71 chars total — clearly exceeds
    public void PreviousCluster_WindowBoundary_ExactThreshold(int combiningMarks, bool shouldReachClusterStart)
    {
        var sb = new StringBuilder();
        sb.Append('a'); // base char
        for (int i = 0; i < combiningMarks; i++)
            sb.Append('\u0308'); // combining diaeresis
        var cluster = sb.ToString();
        var text = "X" + cluster + "Y";
        var cursorPos = 1 + cluster.Length;

        var result = GraphemeHelper.GetPreviousClusterBoundary(text, cursorPos);

        if (shouldReachClusterStart)
        {
            Assert.AreEqual(1, result);
        }
        else
        {
            Assert.IsTrue(result > 1, $"With {combiningMarks} combining marks ({cluster.Length} total chars), " +
                $"expected mid-cluster landing (>1) but got {result}");
        }
    }
}
