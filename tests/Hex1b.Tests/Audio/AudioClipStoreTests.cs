using Xunit;

namespace Hex1b.Tests.Audio;

public class AudioClipStoreTests
{
    [Fact]
    public void StoreClip_StoresAndRetrievesByClipId()
    {
        var store = new AudioClipStore();
        var clip = new AudioClipData(42, new byte[] { 1, 2, 3 }, AudioFormat.Wav, 44100);

        store.StoreClip(clip);

        var retrieved = store.GetClipById(42);
        Assert.NotNull(retrieved);
        Assert.Equal(42u, retrieved.ClipId);
        Assert.Equal(new byte[] { 1, 2, 3 }, retrieved.Data);
    }

    [Fact]
    public void StoreClip_ReplacesExistingClip()
    {
        var store = new AudioClipStore();
        store.StoreClip(new AudioClipData(1, new byte[] { 1 }, AudioFormat.Wav, 44100));
        store.StoreClip(new AudioClipData(1, new byte[] { 2, 3 }, AudioFormat.Wav, 44100));

        Assert.Equal(1, store.ClipCount);
        var clip = store.GetClipById(1);
        Assert.NotNull(clip);
        Assert.Equal(new byte[] { 2, 3 }, clip.Data);
    }

    [Fact]
    public void RemoveClip_RemovesClip()
    {
        var store = new AudioClipStore();
        store.StoreClip(new AudioClipData(1, new byte[] { 1 }, AudioFormat.Wav, 44100));

        Assert.True(store.RemoveClip(1));
        Assert.Null(store.GetClipById(1));
        Assert.Equal(0, store.ClipCount);
    }

    [Fact]
    public void RemoveClip_ReturnsFalseForMissing()
    {
        var store = new AudioClipStore();
        Assert.False(store.RemoveClip(999));
    }

    [Fact]
    public void Clear_RemovesAllClips()
    {
        var store = new AudioClipStore();
        store.StoreClip(new AudioClipData(1, new byte[] { 1 }, AudioFormat.Wav, 44100));
        store.StoreClip(new AudioClipData(2, new byte[] { 2 }, AudioFormat.Wav, 44100));

        store.Clear();

        Assert.Equal(0, store.ClipCount);
        Assert.Null(store.GetClipById(1));
        Assert.Null(store.GetClipById(2));
    }

    [Fact]
    public void ProcessChunk_SingleChunk_ReturnsClip()
    {
        var store = new AudioClipStore();
        var cmd = new AudioCommand { ClipId = 42, Format = AudioFormat.Wav, SampleRate = 44100, MoreData = 0 };
        var data = new byte[] { 1, 2, 3, 4 };

        var result = store.ProcessChunk(cmd, data);

        Assert.NotNull(result);
        Assert.Equal(42u, result.ClipId);
        Assert.Equal(data, result.Data);
    }

    [Fact]
    public void ProcessChunk_MultiChunk_AssemblesComplete()
    {
        var store = new AudioClipStore();

        var cmd1 = new AudioCommand { ClipId = 10, Format = AudioFormat.Wav, SampleRate = 22050, MoreData = 1 };
        var cmd2 = new AudioCommand { MoreData = 1 };
        var cmd3 = new AudioCommand { MoreData = 0 };

        Assert.Null(store.ProcessChunk(cmd1, new byte[] { 1, 2 }));
        Assert.True(store.IsChunkedTransferInProgress);

        Assert.Null(store.ProcessChunk(cmd2, new byte[] { 3, 4 }));

        var result = store.ProcessChunk(cmd3, new byte[] { 5 });
        Assert.NotNull(result);
        Assert.Equal(10u, result.ClipId);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result.Data);
        Assert.Equal(AudioFormat.Wav, result.Format);
        Assert.Equal(22050u, result.SampleRate);

        Assert.False(store.IsChunkedTransferInProgress);
    }

    [Fact]
    public void AllocateId_ReturnsUniqueIds()
    {
        var store = new AudioClipStore();
        var id1 = store.AllocateId();
        var id2 = store.AllocateId();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Quota_EvictsOldestWhenExceeded()
    {
        var store = new AudioClipStore(quotaBytes: 10);
        store.StoreClip(new AudioClipData(1, new byte[] { 1, 2, 3, 4, 5 }, AudioFormat.Wav, 44100));
        store.StoreClip(new AudioClipData(2, new byte[] { 6, 7, 8, 9, 10 }, AudioFormat.Wav, 44100));

        // Storing a third should evict the first
        store.StoreClip(new AudioClipData(3, new byte[] { 11, 12, 13, 14, 15 }, AudioFormat.Wav, 44100));

        Assert.Null(store.GetClipById(1)); // Evicted
        Assert.NotNull(store.GetClipById(3)); // Newest preserved
    }
}
