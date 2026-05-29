using System.Runtime.InteropServices;

class Program
{
    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc fn, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetMonitorInfoW(IntPtr hMon, ref MONITORINFOEXW mi);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettingsW(string device, int mode, ref DEVMODE dm);

    [DllImport("user32.dll")]
    static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT pt);

    delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data);

    static void Main()
    {
        Console.WriteLine($"System DPI: {GetDpiForSystem()}\n");
        Console.WriteLine("=== Monitors ===");

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var mi = new MONITORINFOEXW { cbSize = Marshal.SizeOf<MONITORINFOEXW>() };
            GetMonitorInfoW(hMon, ref mi);
            GetDpiForMonitor(hMon, 0, out uint effDpi, out _);
            GetDpiForMonitor(hMon, 1, out uint angDpi, out _);
            GetDpiForMonitor(hMon, 2, out uint rawDpi, out _);

            var r = mi.rcMonitor;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;

            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
            EnumDisplaySettingsW(mi.szDevice, -1, ref dm);

            Console.WriteLine($"  {mi.szDevice}  Primary={mi.dwFlags == 1}");
            Console.WriteLine($"    rcMonitor (API sees): L={r.Left} T={r.Top} R={r.Right} B={r.Bottom}  => {w}x{h}");
            Console.WriteLine($"    Native resolution:    {dm.dmPelsWidth}x{dm.dmPelsHeight}");
            Console.WriteLine($"    DEVMODE position:     ({dm.dmPositionX},{dm.dmPositionY})");
            Console.WriteLine($"    EffDPI={effDpi}  AngularDPI={angDpi}  RawDPI={rawDpi}");
            Console.WriteLine($"    Scale: rcMonitor/native = {(double)w / dm.dmPelsWidth:F4}");
            Console.WriteLine();
            return true;
        }, IntPtr.Zero);

        Console.WriteLine("=== Cursor position test ===");
        Console.WriteLine("Move cursor to TOP-LEFT corner of LAPTOP screen and press Enter:");
        Console.ReadLine();
        GetCursorPos(out var pos);
        Console.WriteLine($"  GetCursorPos: ({pos.X}, {pos.Y})\n");

        Console.WriteLine("Move cursor to CENTER of LAPTOP screen and press Enter:");
        Console.ReadLine();
        GetCursorPos(out pos);
        Console.WriteLine($"  GetCursorPos: ({pos.X}, {pos.Y})\n");

        Console.WriteLine("Move cursor to TOP-LEFT corner of 4K screen and press Enter:");
        Console.ReadLine();
        GetCursorPos(out pos);
        Console.WriteLine($"  GetCursorPos: ({pos.X}, {pos.Y})\n");

        Console.WriteLine("Move cursor to CENTER of 4K screen and press Enter:");
        Console.ReadLine();
        GetCursorPos(out pos);
        Console.WriteLine($"  GetCursorPos: ({pos.X}, {pos.Y})\n");

        Console.WriteLine("Done. Press Enter to exit.");
        Console.ReadLine();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEXW
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion;
        public short dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight;
        public int dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
}
