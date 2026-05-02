namespace ClickityClackity.Models;

public enum InputEvent
{
    KeyDown,
    KeyUp,
    KeyHold,
    MouseLeftDown,
    MouseLeftUp,
    MouseRightDown,
    MouseRightUp,
    MouseMiddleDown,
    MouseMiddleUp,
    MouseScrollUp,
    MouseScrollDown,
    MouseLeftDrag,
    MouseRightDrag,
    MouseMiddleDrag,
}

public static class InputEventExtensions
{
    public static string DisplayName(this InputEvent e) => e switch
    {
        InputEvent.KeyDown         => "Key Press",
        InputEvent.KeyUp           => "Key Release",
        InputEvent.KeyHold         => "Key Hold",
        InputEvent.MouseLeftDown   => "Left Click Down",
        InputEvent.MouseLeftUp     => "Left Click Up",
        InputEvent.MouseRightDown  => "Right Click Down",
        InputEvent.MouseRightUp    => "Right Click Up",
        InputEvent.MouseMiddleDown => "Middle Click Down",
        InputEvent.MouseMiddleUp   => "Middle Click Up",
        InputEvent.MouseScrollUp   => "Scroll Up",
        InputEvent.MouseScrollDown => "Scroll Down",
        InputEvent.MouseLeftDrag   => "Left Drag",
        InputEvent.MouseRightDrag  => "Right Drag",
        InputEvent.MouseMiddleDrag => "Middle Drag",
        _                          => e.ToString(),
    };

    public static bool IsDrag(this InputEvent e) =>
        e is InputEvent.MouseLeftDrag or InputEvent.MouseRightDrag or InputEvent.MouseMiddleDrag;
}
