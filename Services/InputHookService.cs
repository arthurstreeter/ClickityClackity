using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClickityClackity.Models;

namespace ClickityClackity.Services;

public sealed class InputHookService : IDisposable
{
    public event Action<InputEventData>? InputDetected;

    // When true, the next key-down is consumed by KeyCaptured instead of InputDetected
    public bool IsCapturingKey { get; set; }
    public event Action<uint>? KeyCaptured;

    private IntPtr _kbHook = IntPtr.Zero;
    private IntPtr _mHook  = IntPtr.Zero;
    private NativeMethods.HookProc? _kbProc;
    private NativeMethods.HookProc? _mProc;

    // Key-hold tracking
    private readonly HashSet<uint>                    _downKeys   = [];
    private readonly Dictionary<uint, DispatcherTimer> _holdTimers = [];

    // Modifier key state
    private bool _ctrlDown, _altDown, _shiftDown;

    // Mouse state
    private bool _leftDown, _rightDown, _middleDown;
    private bool _leftDragArmed = true, _rightDragArmed = true, _middleDragArmed = true;

    // Mouse-delta accumulator for pitch calculation
    private int   _pendingDx, _pendingDy;
    private int   _lastX,     _lastY;
    private bool  _mouseInitialized;

    private static readonly TimeSpan HoldDelay    = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan HoldRepeat   = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan DragInterval = TimeSpan.FromMilliseconds(80);

    public void Install()
    {
        _kbProc = KeyboardProc;
        _mProc  = MouseProc;

        using var proc   = Process.GetCurrentProcess();
        using var module = proc.MainModule!;
        var hMod = NativeMethods.GetModuleHandle(module.ModuleName);

        _kbHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbProc, hMod, 0);
        _mHook  = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL,    _mProc,  hMod, 0);
    }

    public void Uninstall()
    {
        if (_kbHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
        if (_mHook  != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_mHook);  _mHook  = IntPtr.Zero; }
        foreach (var t in _holdTimers.Values) t.Stop();
        _holdTimers.Clear();
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb  = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                UpdateModifiers(kb.vkCode, true);

                if (IsCapturingKey)
                {
                    IsCapturingKey = false;
                    KeyCaptured?.Invoke(kb.vkCode);
                    return NativeMethods.CallNextHookEx(_kbHook, nCode, wParam, lParam);
                }

                if (_downKeys.Add(kb.vkCode))
                {
                    Fire(new InputEventData { Event = InputEvent.KeyDown, VkCode = kb.vkCode,
                        CtrlDown = _ctrlDown, AltDown = _altDown, ShiftDown = _shiftDown });
                    StartHoldTimer(kb.vkCode);
                }
            }
            else if (msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
            {
                _downKeys.Remove(kb.vkCode);
                StopHoldTimer(kb.vkCode);
                Fire(new InputEventData { Event = InputEvent.KeyUp, VkCode = kb.vkCode,
                    CtrlDown = _ctrlDown, AltDown = _altDown, ShiftDown = _shiftDown });
                UpdateModifiers(kb.vkCode, false);
            }
        }
        return NativeMethods.CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private void UpdateModifiers(uint vk, bool down)
    {
        if (vk is 0x10 or 0xA0 or 0xA1) _shiftDown = down;
        if (vk is 0x11 or 0xA2 or 0xA3) _ctrlDown  = down;
        if (vk is 0x12 or 0xA4 or 0xA5) _altDown   = down;
    }

    private void StartHoldTimer(uint vk)
    {
        var t = new DispatcherTimer { Interval = HoldDelay };
        bool firstTick = true;
        t.Tick += (_, _) =>
        {
            if (firstTick) { firstTick = false; t.Interval = HoldRepeat; }
            if (_downKeys.Contains(vk))
                Fire(new InputEventData { Event = InputEvent.KeyHold, VkCode = vk,
                    CtrlDown = _ctrlDown, AltDown = _altDown, ShiftDown = _shiftDown });
            else
                t.Stop();
        };
        _holdTimers[vk] = t;
        t.Start();
    }

    private void StopHoldTimer(uint vk)
    {
        if (_holdTimers.Remove(vk, out var t)) t.Stop();
    }

    // ── Mouse ─────────────────────────────────────────────────────────────────

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var ms  = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            switch (msg)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    _leftDown = true; _leftDragArmed = true;
                    Fire(InputEvent.MouseLeftDown);   break;
                case NativeMethods.WM_LBUTTONUP:
                    _leftDown = false;
                    Fire(InputEvent.MouseLeftUp);     break;
                case NativeMethods.WM_RBUTTONDOWN:
                    _rightDown = true; _rightDragArmed = true;
                    Fire(InputEvent.MouseRightDown);  break;
                case NativeMethods.WM_RBUTTONUP:
                    _rightDown = false;
                    Fire(InputEvent.MouseRightUp);    break;
                case NativeMethods.WM_MBUTTONDOWN:
                    _middleDown = true; _middleDragArmed = true;
                    Fire(InputEvent.MouseMiddleDown); break;
                case NativeMethods.WM_MBUTTONUP:
                    _middleDown = false;
                    Fire(InputEvent.MouseMiddleUp);   break;
                case NativeMethods.WM_MOUSEWHEEL:
                    short delta = (short)(ms.mouseData >> 16);
                    Fire(delta > 0 ? InputEvent.MouseScrollUp : InputEvent.MouseScrollDown);
                    break;
                case NativeMethods.WM_MOUSEMOVE:
                    AccumulateDelta(ms.pt.x, ms.pt.y);
                    if (_leftDown   && _leftDragArmed)   FireDrag(InputEvent.MouseLeftDrag);
                    if (_rightDown  && _rightDragArmed)  FireDrag(InputEvent.MouseRightDrag);
                    if (_middleDown && _middleDragArmed) FireDrag(InputEvent.MouseMiddleDrag);
                    break;
            }
        }
        return NativeMethods.CallNextHookEx(_mHook, nCode, wParam, lParam);
    }

    private void AccumulateDelta(int x, int y)
    {
        if (_mouseInitialized)
        {
            _pendingDx += x - _lastX;
            _pendingDy += y - _lastY;
        }
        _lastX = x;
        _lastY = y;
        _mouseInitialized = true;
    }

    private void FireDrag(InputEvent dragEvent)
    {
        int dx = _pendingDx;
        int dy = _pendingDy;
        _pendingDx = _pendingDy = 0;

        SetDragArmed(dragEvent, false);
        Fire(new InputEventData { Event = dragEvent, Dx = dx, Dy = dy });

        var t = new DispatcherTimer { Interval = DragInterval };
        t.Tick += (_, _) => { t.Stop(); SetDragArmed(dragEvent, true); };
        t.Start();
    }

    private void SetDragArmed(InputEvent e, bool armed)
    {
        switch (e)
        {
            case InputEvent.MouseLeftDrag:   _leftDragArmed   = armed; break;
            case InputEvent.MouseRightDrag:  _rightDragArmed  = armed; break;
            case InputEvent.MouseMiddleDrag: _middleDragArmed = armed; break;
        }
    }

    private void Fire(InputEvent e)   => InputDetected?.Invoke(new InputEventData { Event = e });
    private void Fire(InputEventData d) => InputDetected?.Invoke(d);

    public void Dispose() => Uninstall();
}
