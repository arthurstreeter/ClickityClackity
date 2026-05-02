namespace ClickityClackity.Models;

public sealed class InputEventData
{
    public InputEvent Event     { get; init; }
    public uint?      VkCode    { get; init; }
    public int        Dx        { get; init; }
    public int        Dy        { get; init; }
    public bool       CtrlDown  { get; init; }
    public bool       AltDown   { get; init; }
    public bool       ShiftDown { get; init; }
}
