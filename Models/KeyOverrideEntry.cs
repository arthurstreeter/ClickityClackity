namespace ClickityClackity.Models;

public sealed class KeyOverrideEntry
{
    public uint             VkCode       { get; set; }
    public List<SoundEntry> Sounds       { get; set; } = [];
    public List<SoundEntry> HoldSounds   { get; set; } = [];
    public float            Volume       { get; set; } = 1.0f;
    public bool             RequireCtrl  { get; set; } = false;
    public bool             RequireAlt   { get; set; } = false;
    public bool             RequireShift { get; set; } = false;

    public string KeyName => VkCode switch
    {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        0x1B => "Escape",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2E => "Delete",
        0x5B => "Win",
        _    => ((System.Windows.Forms.Keys)VkCode).ToString(),
    };
}
