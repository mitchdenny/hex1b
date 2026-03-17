using System.Diagnostics;
using NAudio.Wave;

namespace AudioDemo;

/// <summary>
/// Audio output that pipes raw PCM float samples to <c>pw-cat</c> (PipeWire)
/// for playback on Linux. Replaces NAudio's Windows-only WaveOutEvent.
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
                FileName = "pw-cat",
                ArgumentList =
                {
                    "--playback",
                    "--format", "f32",
                    "--rate", _sampleRate.ToString(),
                    "--channels", _channels.ToString(),
                    "-"
                },
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        _process.Start();
        // Drain stderr to prevent blocking
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
        // 2048 frames × 2 channels = 4096 float samples per buffer (~46ms at 44.1kHz)
        const int framesPerBuffer = 2048;
        var sampleCount = framesPerBuffer * _channels;
        var floatBuffer = new float[sampleCount];
        var byteBuffer = new byte[sampleCount * sizeof(float)];

        try
        {
            var stdin = _process!.StandardInput.BaseStream;

            while (_running && _process is { HasExited: false })
            {
                var read = _source!.Read(floatBuffer, 0, sampleCount);
                if (read == 0)
                {
                    // Silence if no data
                    Array.Clear(byteBuffer, 0, byteBuffer.Length);
                    stdin.Write(byteBuffer, 0, byteBuffer.Length);
                    continue;
                }

                // Convert float[] to byte[] (IEEE float LE — native format)
                Buffer.BlockCopy(floatBuffer, 0, byteBuffer, 0, read * sizeof(float));
                stdin.Write(byteBuffer, 0, read * sizeof(float));
                stdin.Flush();
            }
        }
        catch (IOException)
        {
            // pw-cat exited — expected on dispose
        }
        catch (ObjectDisposedException)
        {
            // Stream closed
        }
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
    }
}
