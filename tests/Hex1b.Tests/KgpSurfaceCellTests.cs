using System.Security.Cryptography;
using System.Text;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP (Kitty Graphics Protocol) support in surface cells,
/// tracked object store, surface tracking, and surface comparer.
/// </summary>
public class KgpSurfaceCellTests
{
    private static KgpCellData CreateKgpData(string imageContent = "test-image", int zIndex = -1)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(imageContent));
        return new KgpCellData(
            transmitPayload: $"\x1b_Ga=t,f=32,s=2,v=2,i=1;{Convert.ToBase64String(Encoding.UTF8.GetBytes(imageContent))}\x1b\\",
            imageId: 1,
            widthInCells: 4,
            heightInCells: 2,
            sourcePixelWidth: 20,
            sourcePixelHeight: 10,
            contentHash: hash,
            zIndex: zIndex);
    }

    #region SurfaceCell KGP

    [Fact]
    public void SurfaceCell_WithKgp_HasKgpReturnsTrue()
    {
        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        var cell = new SurfaceCell("", null, null, Kgp: tracked);

        Assert.True(cell.HasKgp);
    }

    [Fact]
    public void SurfaceCell_WithoutKgp_HasKgpReturnsFalse()
    {
        var cell = new SurfaceCell("A", null, null);

        Assert.False(cell.HasKgp);
    }

    [Fact]
    public void SurfaceCell_DefaultValues_KgpIsNull()
    {
        var cell = new SurfaceCell("A", null, null);

        Assert.Null(cell.Kgp);
        Assert.False(cell.HasKgp);
    }

    #endregion

    #region Surface KGP Count Tracking

    [Fact]
    public void Surface_TracksKgpCount_IncrementOnSet()
    {
        var surface = new Surface(10, 5);
        Assert.False(surface.HasKgp);

        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        surface[0, 0] = new SurfaceCell("", null, null, Kgp: tracked);

        Assert.True(surface.HasKgp);
    }

    [Fact]
    public void Surface_TracksKgpCount_DecrementOnOverwrite()
    {
        var surface = new Surface(10, 5);
        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        surface[0, 0] = new SurfaceCell("", null, null, Kgp: tracked);
        Assert.True(surface.HasKgp);

        // Overwrite with non-KGP cell
        surface[0, 0] = new SurfaceCell("A", null, null);
        Assert.False(surface.HasKgp);
    }

    [Fact]
    public void Surface_Clear_ResetsKgpCount()
    {
        var surface = new Surface(10, 5);
        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        surface[0, 0] = new SurfaceCell("", null, null, Kgp: tracked);
        Assert.True(surface.HasKgp);

        surface.Clear();
        Assert.False(surface.HasKgp);
    }

    [Fact]
    public void Surface_TrySetCell_TracksKgpCount()
    {
        var surface = new Surface(10, 5);
        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        
        surface.TrySetCell(3, 2, new SurfaceCell("", null, null, Kgp: tracked));
        Assert.True(surface.HasKgp);
    }

    [Fact]
    public void Surface_Clone_PreservesKgpCount()
    {
        var surface = new Surface(10, 5);
        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        surface[0, 0] = new SurfaceCell("", null, null, Kgp: tracked);
        
        var clone = surface.Clone();
        Assert.True(clone.HasKgp);
    }

    #endregion

    #region TrackedObjectStore KGP

    [Fact]
    public void TrackedObjectStore_GetOrCreateKgp_CreatesNewTrackedObject()
    {
        var store = new TrackedObjectStore();
        var kgpData = CreateKgpData();

        var tracked = store.GetOrCreateKgp(kgpData);

        Assert.NotNull(tracked);
        Assert.Same(kgpData, tracked.Data);
        Assert.Equal(1, store.KgpCount);
    }

    [Fact]
    public void TrackedObjectStore_GetOrCreateKgp_DeduplicatesByHash()
    {
        var store = new TrackedObjectStore();
        var kgpData1 = CreateKgpData("same-image");
        var kgpData2 = CreateKgpData("same-image");

        var tracked1 = store.GetOrCreateKgp(kgpData1);
        var tracked2 = store.GetOrCreateKgp(kgpData2);

        // Same tracked object returned for same hash
        Assert.Same(tracked1, tracked2);
        Assert.Equal(1, store.KgpCount);
    }

    [Fact]
    public void TrackedObjectStore_GetOrCreateKgp_DifferentHashCreatesNew()
    {
        var store = new TrackedObjectStore();
        var kgpData1 = CreateKgpData("image-A");
        var kgpData2 = CreateKgpData("image-B");

        var tracked1 = store.GetOrCreateKgp(kgpData1);
        var tracked2 = store.GetOrCreateKgp(kgpData2);

        Assert.NotSame(tracked1, tracked2);
        Assert.Equal(2, store.KgpCount);
    }

    [Fact]
    public void TrackedObjectStore_GetOrCreateKgp_RefCounting()
    {
        var store = new TrackedObjectStore();
        var kgpData = CreateKgpData();

        var tracked = store.GetOrCreateKgp(kgpData);
        Assert.Equal(1, store.KgpCount);

        // Second reference
        var tracked2 = store.GetOrCreateKgp(kgpData);
        Assert.Equal(1, store.KgpCount); // Still one unique object

        // Release both
        tracked.Release();
        Assert.Equal(1, store.KgpCount); // Still tracked (refcount > 0)
        tracked2.Release();
        Assert.Equal(0, store.KgpCount); // Removed at zero refs
    }

    [Fact]
    public void TrackedObjectStore_Clear_RemovesKgp()
    {
        var store = new TrackedObjectStore();
        store.GetOrCreateKgp(CreateKgpData("img1"));
        store.GetOrCreateKgp(CreateKgpData("img2"));
        Assert.Equal(2, store.KgpCount);

        store.Clear();
        Assert.Equal(0, store.KgpCount);
    }

    #endregion

    #region SurfaceComparer KGP

    [Fact]
    public void SurfaceComparer_DetectsKgpChange()
    {
        var prev = new Surface(10, 5);
        var curr = new Surface(10, 5);
        
        var kgpData = CreateKgpData();
        var tracked = new TrackedObject<KgpCellData>(kgpData, _ => { });
        curr[0, 0] = new SurfaceCell("", null, null, Kgp: tracked);

        var diff = SurfaceComparer.Compare(prev, curr);

        Assert.True(diff.ChangedCells.Count > 0);
        Assert.Contains(diff.ChangedCells, c => c.X == 0 && c.Y == 0);
    }

    [Fact]
    public void SurfaceComparer_IgnoresIdenticalKgp()
    {
        var kgpData = CreateKgpData("same-image");
        var tracked1 = new TrackedObject<KgpCellData>(kgpData, _ => { });
        var tracked2 = new TrackedObject<KgpCellData>(
            CreateKgpData("same-image"), _ => { });

        var prev = new Surface(10, 5);
        var curr = new Surface(10, 5);
        prev[0, 0] = new SurfaceCell("", null, null, Kgp: tracked1);
        curr[0, 0] = new SurfaceCell("", null, null, Kgp: tracked2);

        var diff = SurfaceComparer.Compare(prev, curr);

        // Same content hash → no change
        Assert.DoesNotContain(diff.ChangedCells, c => c.X == 0 && c.Y == 0);
    }

    #endregion

    #region KgpCellData ZIndex

    [Fact]
    public void KgpCellData_BuildPlacement_UsesZIndex()
    {
        var kgpData = CreateKgpData(zIndex: 5);
        var payload = kgpData.BuildPlacementPayload();

        Assert.Contains("z=5", payload);
        Assert.DoesNotContain("z=-1", payload);
    }

    [Fact]
    public void KgpCellData_BuildPlacement_DefaultZIndexIsNegativeOne()
    {
        var kgpData = CreateKgpData();
        var payload = kgpData.BuildPlacementPayload();

        Assert.Contains("z=-1", payload);
    }

    [Fact]
    public void KgpCellData_BuildPlacement_PositiveZIndex()
    {
        var kgpData = CreateKgpData(zIndex: 3);
        var payload = kgpData.BuildPlacementPayload();

        Assert.Contains("z=3", payload);
    }

    [Fact]
    public void KgpCellData_WithClip_PreservesZIndex()
    {
        var kgpData = CreateKgpData(zIndex: 7);
        var clipped = kgpData.WithClip(10, 20, 30, 40, 2, 1);

        Assert.Equal(7, clipped.ZIndex);
        Assert.Contains("z=7", clipped.BuildPlacementPayload());
    }

    #endregion
}
