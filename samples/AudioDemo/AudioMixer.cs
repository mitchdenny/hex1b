using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioDemo;

/// <summary>
/// Spatial audio mixer that tracks producer positions and a listener position,
/// computing distance-based attenuation and mixing multiple sources.
/// </summary>
public sealed class AudioMixer : IDisposable
{
    private readonly Dictionary<uint, byte[]> _clips = new();
    private readonly Dictionary<ProducerKey, ProducerState> _producers = new();
    private readonly object _lock = new();
    private readonly WaveFormat _outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    private PipeWireOutput? _output;
    private MixingSampleProvider? _mixer;
    private int _listenerCol;
    private int _listenerRow;

    /// <summary>
    /// Current listener column (typically from mouse position).
    /// </summary>
    public int ListenerCol
    {
        get { lock (_lock) return _listenerCol; }
        set { lock (_lock) _listenerCol = value; }
    }

    /// <summary>
    /// Current listener row (typically from mouse position).
    /// </summary>
    public int ListenerRow
    {
        get { lock (_lock) return _listenerRow; }
        set { lock (_lock) _listenerRow = value; }
    }

    /// <summary>
    /// Initialize the audio output device and start playback.
    /// </summary>
    public void Start()
    {
        _mixer = new MixingSampleProvider(_outputFormat)
        {
            ReadFully = true
        };

        try
        {
            _output = new PipeWireOutput(_outputFormat.SampleRate, _outputFormat.Channels);
            _output.Init(_mixer);
            _output.Play();
        }
        catch (Exception)
        {
            // Audio device not available — run silently
            _output = null;
        }
    }

    /// <summary>
    /// Store a decoded audio clip for later playback.
    /// </summary>
    public void StoreClip(uint clipId, byte[] wavData)
    {
        lock (_lock)
        {
            _clips[clipId] = wavData;
        }
    }

    /// <summary>
    /// Place or update an audio producer at a cell position.
    /// </summary>
    public void PlaceProducer(uint clipId, uint placementId, int col, int row, int volume, bool loop)
    {
        lock (_lock)
        {
            var key = new ProducerKey(clipId, placementId);
            if (_producers.TryGetValue(key, out var existing))
            {
                existing.Column = col;
                existing.Row = row;
                existing.BaseVolume = volume / 100f;
                existing.Loop = loop;
                UpdateProducerVolume(existing);
                return;
            }

            if (!_clips.TryGetValue(clipId, out var wavData))
                return;

            var state = new ProducerState
            {
                ClipId = clipId,
                PlacementId = placementId,
                Column = col,
                Row = row,
                BaseVolume = volume / 100f,
                Loop = loop,
            };

            if (TryCreateSampleProvider(wavData, loop, out var provider))
            {
                state.Provider = provider;
                UpdateProducerVolume(state);
                _mixer?.AddMixerInput(provider);
            }

            _producers[key] = state;
        }
    }

    /// <summary>
    /// Stop and remove a specific producer.
    /// </summary>
    public void StopProducer(uint clipId, uint placementId)
    {
        lock (_lock)
        {
            var key = new ProducerKey(clipId, placementId);
            if (_producers.TryGetValue(key, out var state))
            {
                if (state.Provider is not null)
                    _mixer?.RemoveMixerInput(state.Provider);
                _producers.Remove(key);
            }
        }
    }

    /// <summary>
    /// Delete all producers for a specific clip, or all producers.
    /// </summary>
    public void DeleteClip(uint clipId)
    {
        lock (_lock)
        {
            var toRemove = _producers.Where(p => p.Key.ClipId == clipId).ToList();
            foreach (var (key, state) in toRemove)
            {
                if (state.Provider is not null)
                    _mixer?.RemoveMixerInput(state.Provider);
                _producers.Remove(key);
            }
            _clips.Remove(clipId);
        }
    }

    /// <summary>
    /// Delete all producers and clips.
    /// </summary>
    public void DeleteAll()
    {
        lock (_lock)
        {
            foreach (var state in _producers.Values)
            {
                if (state.Provider is not null)
                    _mixer?.RemoveMixerInput(state.Provider);
            }
            _producers.Clear();
            _clips.Clear();
        }
    }

    /// <summary>
    /// Update all producer volumes based on the current listener position.
    /// Call this whenever the listener moves.
    /// </summary>
    public void UpdateAllVolumes()
    {
        lock (_lock)
        {
            foreach (var state in _producers.Values)
            {
                UpdateProducerVolume(state);
            }
        }
    }

    private void UpdateProducerVolume(ProducerState state)
    {
        if (state.Provider is null) return;

        var dx = state.Column - _listenerCol;
        var dy = state.Row - _listenerRow;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        // Smooth rolloff: falls to ~50% at 5 cells, ~20% at 15 cells, near-zero at 30+
        var attenuation = 1.0f / (1.0f + distance * distance * 0.02f);
        var effectiveVolume = state.BaseVolume * attenuation;

        state.Provider.Volume = Math.Clamp(effectiveVolume, 0f, 1f);
    }

    private bool TryCreateSampleProvider(byte[] wavData, bool loop, out VolumeSampleProvider? provider)
    {
        provider = null;
        try
        {
            var stream = new MemoryStream(wavData);
            var reader = new WaveFileReader(stream);
            ISampleProvider sample = reader.ToSampleProvider();

            // Convert to output format if needed
            if (sample.WaveFormat.SampleRate != _outputFormat.SampleRate ||
                sample.WaveFormat.Channels != _outputFormat.Channels)
            {
                var resampled = new WdlResamplingSampleProvider(sample, _outputFormat.SampleRate);
                if (resampled.WaveFormat.Channels == 1)
                    sample = new MonoToStereoSampleProvider(resampled);
                else
                    sample = resampled;
            }
            else if (sample.WaveFormat.Channels == 1)
            {
                sample = new MonoToStereoSampleProvider(sample);
            }

            if (loop)
            {
                sample = new LoopingSampleProvider(sample, wavData);
            }

            provider = new VolumeSampleProvider(sample) { Volume = 1.0f };
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
    }

    private record struct ProducerKey(uint ClipId, uint PlacementId);

    private class ProducerState
    {
        public uint ClipId;
        public uint PlacementId;
        public int Column;
        public int Row;
        public float BaseVolume;
        public bool Loop;
        public VolumeSampleProvider? Provider;
    }
}

/// <summary>
/// Sample provider that loops audio by recreating the reader when it ends.
/// </summary>
internal sealed class LoopingSampleProvider : ISampleProvider
{
    private readonly byte[] _wavData;
    private ISampleProvider _current;

    public WaveFormat WaveFormat => _current.WaveFormat;

    public LoopingSampleProvider(ISampleProvider initial, byte[] wavData)
    {
        _current = initial;
        _wavData = wavData;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = _current.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                // Restart
                try
                {
                    var stream = new MemoryStream(_wavData);
                    var reader = new WaveFileReader(stream);
                    ISampleProvider sample = reader.ToSampleProvider();

                    if (sample.WaveFormat.SampleRate != WaveFormat.SampleRate ||
                        sample.WaveFormat.Channels != WaveFormat.Channels)
                    {
                        var resampled = new WdlResamplingSampleProvider(sample, WaveFormat.SampleRate);
                        if (resampled.WaveFormat.Channels == 1 && WaveFormat.Channels == 2)
                            sample = new MonoToStereoSampleProvider(resampled);
                        else
                            sample = resampled;
                    }
                    else if (sample.WaveFormat.Channels == 1 && WaveFormat.Channels == 2)
                    {
                        sample = new MonoToStereoSampleProvider(sample);
                    }

                    _current = sample;
                }
                catch
                {
                    break;
                }
            }
            else
            {
                totalRead += read;
            }
        }
        return totalRead;
    }
}
