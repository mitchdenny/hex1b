namespace AudioDemo;

/// <summary>
/// Generates simple WAV audio samples for the spatial audio demo.
/// </summary>
public static class AudioSamples
{
    /// <summary>
    /// Generate a WAV file containing a sine wave tone.
    /// </summary>
    public static byte[] GenerateSineTone(float frequencyHz, float durationSeconds,
        int sampleRate = 44100, float amplitude = 0.5f)
    {
        var numSamples = (int)(sampleRate * durationSeconds);
        var samples = new short[numSamples];

        for (var i = 0; i < numSamples; i++)
        {
            var t = (float)i / sampleRate;
            samples[i] = (short)(amplitude * short.MaxValue * MathF.Sin(2 * MathF.PI * frequencyHz * t));
        }

        return EncodeWav(samples, sampleRate, channels: 1);
    }

    /// <summary>
    /// Generate a crackling fire sound (filtered noise bursts).
    /// </summary>
    public static byte[] GenerateFireCrackle(float durationSeconds, int sampleRate = 44100)
    {
        var numSamples = (int)(sampleRate * durationSeconds);
        var samples = new short[numSamples];
        var rng = new Random(42);
        var envelope = 0f;

        for (var i = 0; i < numSamples; i++)
        {
            // Random crackle bursts
            if (rng.NextDouble() < 0.005)
                envelope = 0.9f;

            envelope *= 0.998f;

            var noise = (float)(rng.NextDouble() * 2 - 1);
            // Simple low-pass filter
            var filtered = noise * envelope;
            samples[i] = (short)(filtered * short.MaxValue * 0.7f);
        }

        return EncodeWav(samples, sampleRate, channels: 1);
    }

    /// <summary>
    /// Generate a water drip/stream sound (modulated sine with noise).
    /// </summary>
    public static byte[] GenerateWaterDrip(float durationSeconds, int sampleRate = 44100)
    {
        var numSamples = (int)(sampleRate * durationSeconds);
        var samples = new short[numSamples];
        var rng = new Random(123);

        for (var i = 0; i < numSamples; i++)
        {
            var t = (float)i / sampleRate;

            // Periodic drip: sine burst every ~0.8s
            var dripPhase = t % 0.8f;
            var dripEnvelope = dripPhase < 0.05f ? MathF.Exp(-dripPhase * 40) : 0f;

            // Frequency glide (higher to lower during drip)
            var freq = 800f + 400f * MathF.Exp(-dripPhase * 30);
            var drip = dripEnvelope * MathF.Sin(2 * MathF.PI * freq * t);

            // Ambient stream noise
            var noise = (float)(rng.NextDouble() * 2 - 1) * 0.05f;

            samples[i] = (short)((drip + noise) * short.MaxValue * 0.6f);
        }

        return EncodeWav(samples, sampleRate, channels: 1);
    }

    /// <summary>
    /// Generate a metallic clang sound (rapidly decaying overtone series).
    /// </summary>
    public static byte[] GenerateMetalClang(float durationSeconds, int sampleRate = 44100)
    {
        var numSamples = (int)(sampleRate * durationSeconds);
        var samples = new short[numSamples];

        for (var i = 0; i < numSamples; i++)
        {
            var t = (float)i / sampleRate;
            var envelope = MathF.Exp(-t * 4f);

            // Metallic timbre: inharmonic overtones
            var sound = 0.4f * MathF.Sin(2 * MathF.PI * 440 * t) +
                        0.3f * MathF.Sin(2 * MathF.PI * 587 * t) +
                        0.2f * MathF.Sin(2 * MathF.PI * 733 * t) +
                        0.1f * MathF.Sin(2 * MathF.PI * 1047 * t);

            samples[i] = (short)(sound * envelope * short.MaxValue * 0.6f);
        }

        return EncodeWav(samples, sampleRate, channels: 1);
    }

    /// <summary>
    /// Generate a low ambient hum (e.g., for a dungeon generator).
    /// </summary>
    public static byte[] GenerateAmbientHum(float durationSeconds, int sampleRate = 44100)
    {
        var numSamples = (int)(sampleRate * durationSeconds);
        var samples = new short[numSamples];
        var rng = new Random(77);

        for (var i = 0; i < numSamples; i++)
        {
            var t = (float)i / sampleRate;

            // Low fundamental with subtle modulation
            var hum = 0.5f * MathF.Sin(2 * MathF.PI * 60 * t) +
                      0.3f * MathF.Sin(2 * MathF.PI * 120 * t) +
                      0.1f * MathF.Sin(2 * MathF.PI * 180 * t);

            // Slow amplitude modulation
            var mod = 0.7f + 0.3f * MathF.Sin(2 * MathF.PI * 0.2f * t);
            hum *= mod;

            // Subtle noise
            var noise = (float)(rng.NextDouble() * 2 - 1) * 0.02f;

            samples[i] = (short)((hum + noise) * short.MaxValue * 0.4f);
        }

        return EncodeWav(samples, sampleRate, channels: 1);
    }

    private static byte[] EncodeWav(short[] samples, int sampleRate, int channels)
    {
        var bytesPerSample = 2; // 16-bit
        var dataSize = samples.Length * bytesPerSample;
        var headerSize = 44;
        var totalSize = headerSize + dataSize;

        using var ms = new MemoryStream(totalSize);
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(totalSize - 8);
        writer.Write("WAVE"u8);

        // fmt sub-chunk
        writer.Write("fmt "u8);
        writer.Write(16); // Sub-chunk size
        writer.Write((short)1); // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample); // Byte rate
        writer.Write((short)(channels * bytesPerSample)); // Block align
        writer.Write((short)(bytesPerSample * 8)); // Bits per sample

        // data sub-chunk
        writer.Write("data"u8);
        writer.Write(dataSize);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        return ms.ToArray();
    }
}
