using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClickLight.Windows;

/// <summary>
/// Installs a low-level mouse hook (WH_MOUSE_LL) to capture all mouse events system-wide.
/// This is the Windows equivalent of macOS CGEvent tap.
/// </summary>
public sealed class MouseHookController : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MOUSEMOVE = 0x0200;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    private const int IDC_ARROW = 32512;
    private const int IDC_IBEAM = 32513;
    private const int IDC_HAND = 32649;
    private const int IDC_SIZEALL = 32646;
    private const int IDC_SIZENS = 32645;
    private const int IDC_SIZEWE = 32644;
    private const int IDC_SIZENESW = 32643;
    private const int IDC_SIZENWSE = 32642;
    private const int CURSOR_SHOWING = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private readonly SettingsStore _settingsStore;
    private readonly Action<ClickEvent> _onClickEvent;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelMouseProc? _proc;
    private bool _leftButtonDown;
    private bool _isFileDrag;
    private int _dragMoveCount;
    private POINT _dragStartPoint;
    private POINT _lastDragEmitPoint;
    private DateTime _lastDragTime = DateTime.MinValue;
    private static readonly TimeSpan DragThrottle = TimeSpan.FromMilliseconds(16);
    private static readonly HashSet<IntPtr> _standardCursors = new();
    private static bool _standardCursorsLoaded;

    public bool IsRunning => _hookId != IntPtr.Zero;
    public string StatusLabel => IsRunning ? "Active" : "Stopped";

    public MouseHookController(SettingsStore settingsStore, Action<ClickEvent> onClickEvent)
    {
        _settingsStore = settingsStore;
        _onClickEvent = onClickEvent;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        // For WH_MOUSE_LL in .NET 8, we must pass a valid loaded module handle.
        // GetModuleHandle("user32") is the reliable workaround — WH_MOUSE_LL doesn't
        // actually inject into other processes, so the module handle is just a formality.
        var moduleHandle = GetModuleHandle("user32");
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ClickLight] SetWindowsHookEx failed: error {Marshal.GetLastWin32Error()}");
        }
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _proc = null;
        _leftButtonDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var msg = (int)wParam;
            var now = GetTimestamp();
            var x = hookStruct.pt.x;
            var y = hookStruct.pt.y;

            switch (msg)
            {
                case WM_LBUTTONDOWN:
                    _leftButtonDown = true;
                    _isFileDrag = false;
                    _dragMoveCount = 0;
                    _dragStartPoint = hookStruct.pt;
                    _lastDragEmitPoint = hookStruct.pt;
                    FireEvent(ClickKind.LeftDown, x, y, now);
                    break;

                case WM_LBUTTONUP:
                    _leftButtonDown = false;
                    FireEvent(ClickKind.LeftUp, x, y, now);
                    break;

                case WM_RBUTTONDOWN:
                    FireEvent(ClickKind.RightDown, x, y, now);
                    break;

                case WM_RBUTTONUP:
                    FireEvent(ClickKind.RightUp, x, y, now);
                    break;

                case WM_MOUSEMOVE:
                    if (_leftButtonDown)
                    {
                        var elapsed = DateTime.UtcNow - _lastDragTime;
                        var distSq = Math.Pow(x - _lastDragEmitPoint.x, 2) + Math.Pow(y - _lastDragEmitPoint.y, 2);
                        if (elapsed > DragThrottle && distSq > 25)
                        {
                            _lastDragEmitPoint = hookStruct.pt;
                            _lastDragTime = DateTime.UtcNow;
                            _dragMoveCount++;

                            // After a few drag moves, check if cursor changed (file/object drag)
                            if (!_isFileDrag && _dragMoveCount >= 3)
                            {
                                _isFileDrag = IsNonStandardCursor();
                            }

                            if (_isFileDrag)
                                FireEvent(ClickKind.FileDrag, x, y, now);
                            else
                                FireDragEvent(x, y, now);
                        }
                    }
                    else if (_settingsStore.Settings.ShowLaserPointer)
                    {
                        var elapsed = DateTime.UtcNow - _lastDragTime;
                        if (elapsed > DragThrottle)
                        {
                            _lastDragTime = DateTime.UtcNow;
                            FireEvent(ClickKind.Move, x, y, now);
                        }
                    }
                    break;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void FireEvent(ClickKind kind, int x, int y, double timestamp)
    {
        if (!_settingsStore.Settings.IsEnabled) return;
        _onClickEvent(new ClickEvent(kind, x, y, timestamp));
    }

    private void FireDragEvent(int x, int y, double timestamp)
    {
        if (!_settingsStore.Settings.IsEnabled) return;
        _onClickEvent(new ClickEvent(ClickKind.Drag, x, y, timestamp, _dragStartPoint.x, _dragStartPoint.y));
    }

    private static double GetTimestamp()
    {
        return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }

    /// <summary>
    /// Checks if the current cursor is NOT a standard system cursor.
    /// During file/object drag-drop, Windows changes the cursor to a custom drag icon.
    /// </summary>
    private static bool IsNonStandardCursor()
    {
        EnsureStandardCursorsLoaded();

        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci)) return false;
        if ((ci.flags & CURSOR_SHOWING) == 0) return false;
        if (ci.hCursor == IntPtr.Zero) return false;

        return !_standardCursors.Contains(ci.hCursor);
    }

    private static void EnsureStandardCursorsLoaded()
    {
        if (_standardCursorsLoaded) return;
        _standardCursorsLoaded = true;

        int[] standardIds = { IDC_ARROW, IDC_IBEAM, IDC_HAND, IDC_SIZEALL, IDC_SIZENS, IDC_SIZEWE, IDC_SIZENESW, IDC_SIZENWSE };
        foreach (var id in standardIds)
        {
            var cursor = LoadCursor(IntPtr.Zero, id);
            if (cursor != IntPtr.Zero)
                _standardCursors.Add(cursor);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
