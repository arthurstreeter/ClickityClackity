using System.IO;
using System.Text.Json;
using ClickityClackity.Models;

namespace ClickityClackity.Services;

public sealed class SoundManager : IDisposable
{
    private static readonly string[] AudioExts =
        [".wav", ".mp3", ".flac", ".aiff", ".m4a", ".aac"];

    private readonly SoundEngine _engine = new();
    private readonly Dictionary<InputEvent, List<SoundEntry>> _map = [];
    private readonly Random _rng = new();
    private readonly string _profilePath;

    // ── Exposed settings ──────────────────────────────────────────────────────
    public Dictionary<InputEvent, float> EventVolumes { get; } = [];
    public List<KeyOverrideEntry>        KeyOverrides  { get; } = [];

    public float DragPitchSemitones   { get; set; } = 7f;
    public float RandomPitchSemitones { get; set; } = 0f;

    public string SoundsFolder { get; private set; }

    public void SetSoundsFolder(string path)
    {
        SoundsFolder = path;
        Directory.CreateDirectory(path);
        _engine.InvalidateCache();
    }

    public float Volume
    {
        get => _engine.Volume;
        set => _engine.Volume = value;
    }

    public SoundManager(string soundsFolder, string profilePath)
    {
        SoundsFolder = soundsFolder;
        _profilePath = profilePath;
        Directory.CreateDirectory(soundsFolder);

        foreach (InputEvent e in Enum.GetValues<InputEvent>())
        {
            _map[e]         = [];
            EventVolumes[e] = 1.0f;
        }
    }

    // ── Sound map ─────────────────────────────────────────────────────────────

    public IReadOnlyList<SoundEntry> GetSounds(InputEvent e) =>
        _map.TryGetValue(e, out var list) ? list : [];

    public void SetSounds(InputEvent e, List<SoundEntry> sounds) => _map[e] = sounds;

    // ── Play entry point ──────────────────────────────────────────────────────

    public void Play(InputEventData data)
    {
        if (data.Event == InputEvent.KeyDown && data.VkCode.HasValue)
            PlayKeyDown(data.VkCode.Value, data.CtrlDown, data.AltDown, data.ShiftDown);
        else if (data.Event == InputEvent.KeyHold && data.VkCode.HasValue)
            PlayKeyHold(data.VkCode.Value, data.CtrlDown, data.AltDown, data.ShiftDown);
        else if (data.Event.IsDrag())
            PlayEvent(data.Event, CalcDragPitch(data.Dx, data.Dy));
        else
            PlayEvent(data.Event);
    }

    private KeyOverrideEntry? FindOverride(uint vkCode, bool ctrl, bool alt, bool shift) =>
        KeyOverrides.Find(k => k.VkCode == vkCode
            && k.RequireCtrl  == ctrl
            && k.RequireAlt   == alt
            && k.RequireShift == shift);

    private void PlayKeyDown(uint vkCode, bool ctrl, bool alt, bool shift)
    {
        var ko = FindOverride(vkCode, ctrl, alt, shift);
        if (ko != null && ko.Sounds.Count > 0)
        {
            var s = PickRandom(ko.Sounds);
            var path = Path.Combine(SoundsFolder, s.File);
            if (File.Exists(path))
            {
                float pitch = s.RandomPitch ? ApplyRandom(1.0f) : 1.0f;
                _engine.Play(path, pitch, ko.Volume);
                return;
            }
        }
        PlayEvent(InputEvent.KeyDown);
    }

    private void PlayKeyHold(uint vkCode, bool ctrl, bool alt, bool shift)
    {
        var ko = FindOverride(vkCode, ctrl, alt, shift);
        if (ko != null && ko.HoldSounds.Count > 0)
        {
            var s = PickRandom(ko.HoldSounds);
            var path = Path.Combine(SoundsFolder, s.File);
            if (File.Exists(path))
            {
                float pitch = s.RandomPitch ? ApplyRandom(1.0f) : 1.0f;
                _engine.Play(path, pitch, ko.Volume);
                return;
            }
        }
        PlayEvent(InputEvent.KeyHold);
    }

    private void PlayEvent(InputEvent evt, float basePitch = 1.0f)
    {
        var sounds = _map[evt];
        if (sounds.Count == 0) return;

        var s = PickRandom(sounds);
        if (string.IsNullOrEmpty(s.File)) return;
        var full = Path.Combine(SoundsFolder, s.File);
        if (!File.Exists(full)) return;

        float evtVol = EventVolumes.TryGetValue(evt, out var v) ? v : 1.0f;
        float pitch  = s.RandomPitch ? ApplyRandom(basePitch) : basePitch;
        _engine.Play(full, pitch, evtVol);
    }

    private T PickRandom<T>(IReadOnlyList<T> list) =>
        list.Count == 1 ? list[0] : list[_rng.Next(list.Count)];

    // ── Pitch helpers ─────────────────────────────────────────────────────────

    private float CalcDragPitch(int dx, int dy)
    {
        if (DragPitchSemitones <= 0f) return 1.0f;
        float combined   = dx - dy;
        float normalized = Math.Clamp(combined / 50f, -1f, 1f);
        return SemitonesToFactor(normalized * DragPitchSemitones);
    }

    private float ApplyRandom(float basePitch)
    {
        if (RandomPitchSemitones <= 0f) return basePitch;
        float semi = (float)((_rng.NextDouble() * 2.0 - 1.0) * RandomPitchSemitones);
        return basePitch * SemitonesToFactor(semi);
    }

    private static float SemitonesToFactor(float semitones) =>
        MathF.Pow(2f, semitones / 12f);

    // ── File discovery ────────────────────────────────────────────────────────

    public IReadOnlyList<string> GetSoundFiles() =>
        Directory
            .EnumerateFiles(SoundsFolder)
            .Where(f => AudioExts.Contains(
                Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void InvalidateAudioCache() => _engine.InvalidateCache();

    // ── Profile persistence ───────────────────────────────────────────────────

    public void SaveProfile()
    {
        var doc = new ProfileData
        {
            Volume               = Volume,
            DragPitchSemitones   = DragPitchSemitones,
            RandomPitchSemitones = RandomPitchSemitones,
            SoundsFolder         = SoundsFolder,
            Sounds       = _map.ToDictionary(kv => kv.Key.ToString(),
                kv => (List<SoundEntryData>?)kv.Value
                    .Select(s => new SoundEntryData { File = s.File, RandomPitch = s.RandomPitch })
                    .ToList()),
            EventVolumes = EventVolumes.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            KeyOverrides = KeyOverrides.Select(k => new KeyOverrideData
            {
                VkCode       = k.VkCode,
                Sounds       = ToData(k.Sounds),
                HoldSounds   = ToData(k.HoldSounds),
                Volume       = k.Volume,
                RequireCtrl  = k.RequireCtrl,
                RequireAlt   = k.RequireAlt,
                RequireShift = k.RequireShift,
            }).ToList(),
        };
        File.WriteAllText(_profilePath,
            JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void LoadProfile()
    {
        if (!File.Exists(_profilePath)) return;
        try
        {
            var doc = JsonSerializer.Deserialize<ProfileData>(File.ReadAllText(_profilePath));
            if (doc == null) return;

            Volume               = Math.Clamp(doc.Volume, 0f, 1f);
            DragPitchSemitones   = Math.Max(0f, doc.DragPitchSemitones);
            RandomPitchSemitones = Math.Max(0f, doc.RandomPitchSemitones);

            if (!string.IsNullOrWhiteSpace(doc.SoundsFolder))
                SetSoundsFolder(doc.SoundsFolder);

            foreach (var (k, v) in doc.Sounds)
                if (Enum.TryParse<InputEvent>(k, out var e) && v != null)
                    _map[e] = FromData(v);

            foreach (var (k, v) in doc.EventVolumes)
                if (Enum.TryParse<InputEvent>(k, out var e))
                    EventVolumes[e] = v;

            KeyOverrides.Clear();
            foreach (var kd in doc.KeyOverrides)
                KeyOverrides.Add(new KeyOverrideEntry
                {
                    VkCode       = kd.VkCode,
                    Sounds       = FromData(kd.Sounds),
                    HoldSounds   = FromData(kd.HoldSounds),
                    Volume       = kd.Volume,
                    RequireCtrl  = kd.RequireCtrl,
                    RequireAlt   = kd.RequireAlt,
                    RequireShift = kd.RequireShift,
                });
        }
        catch { /* ignore corrupt profile */ }
    }

    private static List<SoundEntryData> ToData(List<SoundEntry> list) =>
        list.Select(s => new SoundEntryData { File = s.File, RandomPitch = s.RandomPitch }).ToList();

    private static List<SoundEntry> FromData(List<SoundEntryData> list) =>
        list.Select(s => new SoundEntry { File = s.File, RandomPitch = s.RandomPitch }).ToList();

    public void Dispose() => _engine.Dispose();

    // ── JSON DTOs ─────────────────────────────────────────────────────────────

    private sealed class ProfileData
    {
        public float                                     Volume               { get; set; } = 0.8f;
        public float                                     DragPitchSemitones   { get; set; } = 7f;
        public float                                     RandomPitchSemitones { get; set; } = 0f;
        public string                                    SoundsFolder         { get; set; } = "";
        public Dictionary<string, List<SoundEntryData>?> Sounds              { get; set; } = [];
        public Dictionary<string, float>                 EventVolumes         { get; set; } = [];
        public List<KeyOverrideData>                     KeyOverrides         { get; set; } = [];
    }

    private sealed class SoundEntryData
    {
        public string File        { get; set; } = "";
        public bool   RandomPitch { get; set; } = true;
    }

    private sealed class KeyOverrideData
    {
        public uint                 VkCode       { get; set; }
        public List<SoundEntryData> Sounds       { get; set; } = [];
        public List<SoundEntryData> HoldSounds   { get; set; } = [];
        public float                Volume       { get; set; } = 1.0f;
        public bool                 RequireCtrl  { get; set; } = false;
        public bool                 RequireAlt   { get; set; } = false;
        public bool                 RequireShift { get; set; } = false;
    }
}
