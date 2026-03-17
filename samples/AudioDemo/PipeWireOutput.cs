using System.Diagnostics;
using NAudio.Wave;

namespace AudioDemo;

/// <summary>
/// Audio output that streams a WAV-formatted float32 stream to <c>pw-play</c>
/// (PipeWire) for playback on Linux. Writes a WAV header first so pw-play
/// auto-detects the format, then continuously pumps PCM samples.
/// </summary>
public sealed class PipeWireOutput : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private Process? _process;
    private Thread? _pumpThread;
    private ISampleProvider? _source;
    private volatile bool _running;

    public PipeWireOutput(int sampleRate = 44100, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;
    }

    public void Init(ISampleProvider source)
    {
        _source = source;
    }

    public void Play()
    {
        if (_source is null)
            throw new InvalidOperationException("Call Init() before Play().");

        _running = true;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pw-play",
                ArgumentList = { "-" },
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        _process.Start();
        _process.BeginErrorReadLine();

        _pumpThread = new Thread(PumpAudio)
        {
            IsBackground = true,
            Name = "AudioPump"
        };
        _pumpThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _pumpThread?.Join(timeout: TimeSpan.FromSeconds(2));

        if (_process is { HasExited: false })
        {
            try
            {
                _process.StandardInput.Close();
                _process.WaitForExit(2000);
                if (!_process.HasExited)
                    _process.Kill();
            }
            catch { /* best effort */ }
        }
    }

    private void PumpAudio()
    {
        const int framesPerBuffer = 2048;
        var sampleCount = framesPerBuffer * _channels;
        var floatBuffer = new float[sampleCount];
        var byteBuffer = new byte[sampleCount * sizeof(float)];

        try
        {
            var stdin = _process!.StandardInput.BaseStream;

            // Write a WAV header with a large data size so pw-play streams until EOF
            WriteWavHeader(stdin, _sampleRate, _channels);

            while (_running && _process is { HasExited: false })
            {
                var read = _source!.Read(floatBuffer, 0, sampleCount);
                if (read == 0)
                {
                    Array.Clear(byteBuffer, 0, byteBuffer.Length);
                    stdin.Write(byteBuffer, 0, byteBuffer.Length);
                    stdin.Flush();
                    continue;
                }

                Buffer.BlockCopy(floatBuffer, 0, byteBuffer, 0, read * sizeof(float));
                stdin.Write(byteBuffer, 0, read * sizeof(float));
                stdin.Flush();
            }
        }
        catch (IOException)
        {
            // pw-play exited — expected on dispose
        }
        catch (ObjectDisposedException)
        {
            // Stream closed
        }
    }

    /// <summary>
    /// Write a WAV header for IEEE float32 with a large data chunk size,
    /// allowing pw-play to stream until stdin closes.
    /// </summary>
    private static void WriteWavHeader(Stream stream, int sampleRate, int channels)
    {
        const int bitsPerSample = 32;
        var blockAlign = channels * (bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;
        const int dataSize = int.MaxValue - 44;

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)'R'); writer.Write((byte)'I'); writer.Write((byte)'F'); writer.Write((byte)'F');
        writer.Write(36 + dataSize);
        writer.Write((byte)'W'); writer.Write((byte)'A'); writer.Write((byte)'V'); writer.Write((byte)'E');
        writer.Write((byte)'f'); writer.Write((byte)'m'); writer.Write((byte)'t'); writer.Write((byte)' ');
        writer.Write(16);               // sub-chunk size
        writer.Write((short)3);          // IEEE float format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write((byte)'d'); writer.Write((byte)'a'); writer.Write((byte)'t'); writer.Write((byte)'a');
        writer.Write(dataSize);
        writer.Flush();
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
    }
}
