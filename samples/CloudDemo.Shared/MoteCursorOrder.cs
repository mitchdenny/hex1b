/// <summary>Orders motes top-to-bottom then left-to-right, in pixel space.</summary>
/// <remarks>
/// Both renderers emit one graphics command per mote, and hundreds of commands that
/// jump the cursor around arbitrarily is the access pattern terminals handle worst.
/// Sorting into cursor order costs almost nothing and also lets a renderer skip the
/// cursor move whenever consecutive motes land in the same cell.
/// </remarks>
internal sealed class MoteCursorOrder : IComparer<DustMote>
{
    public static readonly MoteCursorOrder Instance = new();

    public int Compare(DustMote? x, DustMote? y)
    {
        if (x is null || y is null)
        {
            return 0;
        }

        var byRow = x.Y.CompareTo(y.Y);
        return byRow != 0 ? byRow : x.X.CompareTo(y.X);
    }
}
