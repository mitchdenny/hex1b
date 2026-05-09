using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// Tiny shared helper for slides whose body is just a heading + bullet list.
/// Real slides can absolutely build their own widget tree directly — this is
/// here only so the placeholder slides stay short until you flesh them out.
/// </summary>
internal static class PlaceholderSlide
{
    public static Hex1bWidget Build(
        SlideContext context,
        string heading,
        params string[] bullets)
    {
        var ctx = context.Root;
        return ctx.VStack(v =>
        {
            var children = new List<Hex1bWidget>
            {
                v.Text(heading),
                v.Text(new string('═', heading.Length)),
                v.Text(""),
            };

            foreach (var bullet in bullets)
            {
                children.Add(v.Text("  • " + bullet));
            }

            return [.. children];
        });
    }
}
