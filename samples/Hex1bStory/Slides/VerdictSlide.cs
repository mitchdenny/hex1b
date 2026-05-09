using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// Verdict + takeaway. Shows live stats from the repo (sample count, src
/// file count, lines of code) so the presenter can riff on what's there
/// "right now", followed by three takeaway lines. Stats are computed lazily
/// once and cached — never recomputed and never blocking.
/// </summary>
internal sealed class VerdictSlide : ISlide
{
    public string Title => "Verdict";

    private static readonly Lazy<RepoStats> Stats = new(LoadStats, isThreadSafe: true);

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;
        var s = Stats.Value;

        return ctx.VStack(v =>
        [
            v.Text("So... is it garbage?"),
            v.Text("════════════════════"),
            v.Text(""),
            v.Text(""),
            v.Center(c => c.VStack(stack =>
            [
                stack.Border(stack.Padding(2, 1, stack.VStack(p =>
                [
                    p.Text(s.Available
                        ? $"  {s.SampleCount,4}   samples in samples/"
                        : "   --     samples in samples/"),
                    p.Text(s.Available
                        ? $"  {s.SourceFiles,4}   .cs files in src/Hex1b/"
                        : "   --     .cs files in src/Hex1b/"),
                    p.Text(s.Available
                        ? $"{s.SourceLines,6}   lines of source"
                        : "   --     lines of source"),
                    p.Text("  ~7.6k tests · all passing · self-hosted"),
                ])))
                    .Title(" what's in the repo right now "),
            ])),
            v.Text(""),
            v.Text(""),
            v.Center(c => c.VStack(stack =>
            [
                stack.Text("It's possible.  Months, not years."),
                stack.Text(""),
                stack.Text("Iterate with guardrails — Roslyn rules, early."),
                stack.Text(""),
                stack.Text("Now shipping in Aspire."),
            ])),
        ]);
    }

    private static RepoStats LoadStats()
    {
        try
        {
            var root = RepoRoot.Locate();
            if (root is null) return RepoStats.Unavailable;

            var samplesDir = Path.Combine(root, "samples");
            var sampleCount = Directory.Exists(samplesDir)
                ? Directory.GetDirectories(samplesDir).Length
                : 0;

            var srcDir = Path.Combine(root, "src", "Hex1b");
            var srcFiles = 0;
            var srcLines = 0;
            if (Directory.Exists(srcDir))
            {
                foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
                {
                    srcFiles++;
                    try
                    {
                        srcLines += File.ReadAllLines(file).Length;
                    }
                    catch
                    {
                        // skip files we can't read; keep going
                    }
                }
            }

            return new RepoStats(true, sampleCount, srcFiles, srcLines);
        }
        catch
        {
            return RepoStats.Unavailable;
        }
    }

    private sealed record RepoStats(bool Available, int SampleCount, int SourceFiles, int SourceLines)
    {
        public static RepoStats Unavailable { get; } = new(false, 0, 0, 0);
    }
}
