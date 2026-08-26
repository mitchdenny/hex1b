namespace Hex1b.Tests;

[TestClass]
public class KgpTerminalGraphicsStateTests
{
    private static KgpImageData CreateImage(uint id, byte fill)
        => new(
            id,
            imageNumber: 0,
            KgpTestHelper.CreatePixelData(1, 1, fillByte: fill),
            width: 1,
            height: 1,
            KgpFormat.Rgba32);

    private static KgpPlacement CreatePlacement(uint imageId)
        => new(imageId, placementId: 1, row: 0, column: 0, displayColumns: 1, displayRows: 1);

    private static void StartUpload(KgpImageStore store, uint imageId)
    {
        var controlData = $"a=t,f=32,s=1,v=1,i={imageId},m=1,q=2";
        var success = KgpCommandParser.TryParse(
            controlData,
            out var command,
            out var failure);
        Assert.IsTrue(
            success,
            success ? null : failure.FormatReason(controlData.AsSpan()));
        var result = store.ProcessChunk(
            command!,
            new byte[] { 1, 2, 3 },
            maximumBytes: 4);
        Assert.AreEqual(KgpImageStore.ChunkStatus.Incomplete, result.Status);
    }

    [TestMethod]
    public void ScreenStates_SameImageId_AreIndependent()
    {
        var state = new KgpTerminalGraphicsState();
        state.ActiveImageStore.StoreImage(CreateImage(1, 0xAA));
        state.ActivePlacements.Add(CreatePlacement(1));

        state.EnterAlternateScreen();
        Assert.AreEqual(0, state.ActiveImageStore.ImageCount);
        Assert.IsEmpty(state.ActivePlacements);

        state.ActiveImageStore.StoreImage(CreateImage(1, 0xBB));
        state.ActivePlacements.Add(CreatePlacement(1));

        state.ExitAlternateScreen();

        Assert.AreEqual(0xAA, state.ActiveImageStore.GetImageById(1)!.Data[0]);
        TestSeq.Single(state.ActivePlacements);
    }

    [TestMethod]
    public void EnterAlternateScreen_WhenRepeated_ReinitializesOnlyAlternate()
    {
        var state = new KgpTerminalGraphicsState();
        state.ActiveImageStore.StoreImage(CreateImage(1, 0xAA));
        state.ActivePlacements.Add(CreatePlacement(1));

        state.EnterAlternateScreen();
        state.ActiveImageStore.StoreImage(CreateImage(2, 0xBB));
        state.ActivePlacements.Add(CreatePlacement(2));
        StartUpload(state.ActiveImageStore, 3);

        state.EnterAlternateScreen();

        Assert.AreEqual(0, state.ActiveImageStore.ImageCount);
        Assert.IsEmpty(state.ActivePlacements);
        Assert.IsFalse(state.ActiveImageStore.IsChunkedTransferInProgress);

        state.ExitAlternateScreen();
        Assert.AreEqual(0xAA, state.ActiveImageStore.GetImageById(1)!.Data[0]);
        TestSeq.Single(state.ActivePlacements);
    }

    [TestMethod]
    public void ExitAlternateScreen_WhenAlreadyMain_IsNoOp()
    {
        var state = new KgpTerminalGraphicsState();
        state.ActiveImageStore.StoreImage(CreateImage(1, 0xAA));
        state.ActivePlacements.Add(CreatePlacement(1));

        state.ExitAlternateScreen();

        Assert.IsNotNull(state.ActiveImageStore.GetImageById(1));
        TestSeq.Single(state.ActivePlacements);
    }

    [TestMethod]
    public void ClearActiveScreen_WithHistoryReference_PreservesDataUntilHistoryClears()
    {
        var state = new KgpTerminalGraphicsState();
        state.ActiveImageStore.StoreImage(CreateImage(1, 0xAA));
        state.ActiveImageStore.StoreImage(CreateImage(2, 0xBB));
        state.ActivePlacements.Add(CreatePlacement(1));
        state.ActivePlacements.Add(CreatePlacement(2));
        state.RetainActiveHistoryImage(1);

        state.ClearActiveScreen(clearHistory: false);

        Assert.IsEmpty(state.ActivePlacements);
        Assert.IsNotNull(state.ActiveImageStore.GetImageById(1));
        Assert.IsNull(state.ActiveImageStore.GetImageById(2));

        state.ReleaseActiveHistoryImage(1);
        Assert.AreEqual(0, state.ActiveImageStore.ImageCount);

        state.ActiveImageStore.StoreImage(CreateImage(3, 0xCC));
        state.RetainActiveHistoryImage(3);
        state.ClearActiveScreen(clearHistory: true);

        Assert.AreEqual(0, state.ActiveImageStore.ImageCount);
    }

    [TestMethod]
    public void ReleaseActiveHistoryImage_WithoutReference_Throws()
    {
        var state = new KgpTerminalGraphicsState();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => state.ReleaseActiveHistoryImage(1));
    }

    [TestMethod]
    public void Reset_ClearsMainAndAlternateState()
    {
        var state = new KgpTerminalGraphicsState();
        state.ActiveImageStore.StoreImage(CreateImage(1, 0xAA));
        state.ActivePlacements.Add(CreatePlacement(1));
        state.RetainActiveHistoryImage(1);
        StartUpload(state.ActiveImageStore, 10);
        var mainStore = state.ActiveImageStore;
        state.EnterAlternateScreen();
        state.ActiveImageStore.StoreImage(CreateImage(2, 0xBB));
        state.ActivePlacements.Add(CreatePlacement(2));
        state.RetainActiveHistoryImage(2);
        StartUpload(state.ActiveImageStore, 20);
        var alternateStore = state.ActiveImageStore;

        state.Reset();

        Assert.AreEqual(0, mainStore.ImageCount);
        Assert.AreEqual(0, mainStore.TotalSize);
        Assert.IsFalse(mainStore.IsChunkedTransferInProgress);
        Assert.AreEqual(0, alternateStore.ImageCount);
        Assert.AreEqual(0, alternateStore.TotalSize);
        Assert.IsFalse(alternateStore.IsChunkedTransferInProgress);
        Assert.AreEqual(0, state.ActiveImageStore.ImageCount);
        Assert.IsEmpty(state.ActivePlacements);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => state.ReleaseActiveHistoryImage(1));

        state.EnterAlternateScreen();
        Assert.AreEqual(0, state.ActiveImageStore.ImageCount);
        Assert.IsEmpty(state.ActivePlacements);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => state.ReleaseActiveHistoryImage(2));
    }
}
