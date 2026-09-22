namespace Hex1b;

/// <summary>
/// The deletion target specifier for KGP delete commands.
/// Specified by the 'd' key in the control data.
/// </summary>
public enum KgpDeleteTarget
{
    /// <summary>Delete all visible placements (d=a).</summary>
    All,

    /// <summary>Delete all visible placements, free data (d=A).</summary>
    AllFreeData,

    /// <summary>Delete by image ID (d=i).</summary>
    ById,

    /// <summary>Delete by image ID, free data (d=I).</summary>
    ByIdFreeData,

    /// <summary>Delete newest by image number (d=n).</summary>
    ByNumber,

    /// <summary>Delete newest by image number, free data (d=N).</summary>
    ByNumberFreeData,

    /// <summary>Delete at cursor position (d=c).</summary>
    AtCursor,

    /// <summary>Delete at cursor position, free data (d=C).</summary>
    AtCursorFreeData,

    /// <summary>Delete at specific cell (d=p).</summary>
    AtCell,

    /// <summary>Delete at specific cell, free data (d=P).</summary>
    AtCellFreeData,

    /// <summary>Delete at cell with z-index (d=q).</summary>
    AtCellWithZIndex,

    /// <summary>Delete at cell with z-index, free data (d=Q).</summary>
    AtCellWithZIndexFreeData,

    /// <summary>Delete by column (d=x).</summary>
    ByColumn,

    /// <summary>Delete by column, free data (d=X).</summary>
    ByColumnFreeData,

    /// <summary>Delete by row (d=y).</summary>
    ByRow,

    /// <summary>Delete by row, free data (d=Y).</summary>
    ByRowFreeData,

    /// <summary>Delete by z-index (d=z).</summary>
    ByZIndex,

    /// <summary>Delete by z-index, free data (d=Z).</summary>
    ByZIndexFreeData,

    /// <summary>Delete by ID range (d=r).</summary>
    ByRange,

    /// <summary>Delete by ID range, free data (d=R).</summary>
    ByRangeFreeData,

    /// <summary>Delete animation frames (d=f).</summary>
    AnimationFrames,

    /// <summary>Delete animation frames, free data (d=F).</summary>
    AnimationFramesFreeData,
}
