using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ClickityClackity.Services;

public sealed class SoundEngine : IDisposable
{
    private static readonly WaveFormat TargetFmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    private readonly WaveOutEvent            _output;
    private readonly MixingSampleProvider    _mixer;
    private readonly Dictionary<string, CachedSound> _cache
        = new(StringComparer.OrdinalIgnoreCase);

    public SoundEngine()
    {
        _output = new WaveOutEvent { DesiredLatency = 60 };
        _mixer  = new MixingSampleProvider(TargetFmt) { ReadFully = true };
        _output.Init(_mixer);
        _output.Play();
    }

    public float Volume
    {
        get => _output.Volume;
        set => _output.Volume = Math.Clamp(value, 0f, 1f);
    }

    /// <param name="pitch">1.0 = normal. >1 = higher/faster, <1 = lower/slower.</param>
    /// <param name="volume">Per-clip volume multiplier applied before the master fader.</param>
    public void Play(string filePath, float pitch = 1.0f, float volume = 1.0f)
    {
        if (!_cache.TryGetValue(filePath, out var sound))
        {
            try   { sound = new CachedSound(filePath); }
            catch { return; }
            _cache[filePath] = sound;
        }

        ISampleProvider src = Math.Abs(pitch - 1.0f) < 0.005f
            ? new CachedSoundProvider(sound)
            : new PitchedCachedSoundProvider(sound, pitch);

        if (Math.Abs(volume - 1.0f) >= 0.005f)
            src = new VolumeSampleProvider(src) { Volume = volume };

        _mixer.AddMixerInput(src);
    }

    public void InvalidateCache() => _cache.Clear();

    public void Dispose() { _output.Stop(); _output.Dispose(); }
}

// ── Pre-loaded audio ──────────────────────────────────────────────────────────

public sealed class CachedSound
{
    public float[] Data { get; }

    public CachedSound(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        ISampleProvider src = reader;

        if (reader.WaveFormat.Channels == 1)
            src = new MonoToStereoSampleProvider(src);

        if (reader.WaveFormat.SampleRate != 44100)
            src = new WdlResamplingSampleProvider(src, 44100);

        var list = new List<float>();
        var buf  = new float[4096];
        int n;
        while ((n = src.Read(buf, 0, buf.Length)) > 0)
            list.AddRange(buf.AsSpan(0, n));

        Data = [.. list];
    }
}

// ── Normal-speed provider ─────────────────────────────────────────────────────

public sealed class CachedSoundProvider : ISampleProvider
{
    private static readonly WaveFormat Fmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private readonly CachedSound _sound;
    private int _pos;

    public CachedSoundProvider(CachedSound sound) => _sound = sound;
    public WaveFormat WaveFormat => Fmt;

    public int Read(float[] buffer, int offset, int count)
    {
        int avail = _sound.Data.Length - _pos;
        int n     = Math.Min(avail, count);
        Array.Copy(_sound.Data, _pos, buffer, offset, n);
        _pos += n;
        return n;
    }
}

// ── Pitch-shifted provider (linear interpolation, speed-coupled) ──────────────

public sealed class PitchedCachedSoundProvider : ISampleProvider
{
    private static readonly WaveFormat Fmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private readonly float[] _data;
    private readonly int     _totalFrames;
    private double           _pos;   // frame index (fractional)
    private readonly double  _step;  // frames advanced per output frame = pitch factor

    public PitchedCachedSoundProvider(CachedSound sound, float pitchFactor)
    {
        _data        = sound.Data;
        _totalFrames = sound.Data.Length / 2;
        _step        = pitchFactor;
    }

    public WaveFormat WaveFormat => Fmt;

    public int Read(float[] buffer, int offset, int count)
    {
        int written     = 0;
        int framesToRead = count / 2; // stereo: 2 floats per frame

        for (int i = 0; i < framesToRead; i++)
        {
            int    f0   = (int)_pos;
            double frac = _pos - f0;
            int    f1   = f0 + 1;

            if (f0 >= _totalFrames) break;

            float l0 = _data[f0 * 2];
            float r0 = _data[f0 * 2 + 1];
            float l1 = f1 < _totalFrames ? _data[f1 * 2]     : 0f;
            float r1 = f1 < _totalFrames ? _data[f1 * 2 + 1] : 0f;

            buffer[offset + written]     = (float)(l0 + frac * (l1 - l0));
            buffer[offset + written + 1] = (float)(r0 + frac * (r1 - r0));
            written += 2;
            _pos    += _step;
        }
        return written;
    }
}
