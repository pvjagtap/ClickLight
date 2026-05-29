using System.Runtime.InteropServices;

namespace ClickLight.Windows;

/// <summary>
/// Hides/restores the system mouse cursor globally using Win32 API.
/// Uses SetSystemCursor to replace all cursor types with a blank cursor,
/// and SystemParametersInfo to restore defaults.
/// </summary>
public static class CursorHider
{
    private const uint OCR_NORMAL = 32512;
    private const uint OCR_IBEAM = 32513;
    private const uint OCR_WAIT = 32514;
    private const uint OCR_CROSS = 32515;
    private const uint OCR_UP = 32516;
    private const uint OCR_SIZENWSE = 32642;
    private const uint OCR_SIZENESW = 32643;
    private const uint OCR_SIZEWE = 32644;
    private const uint OCR_SIZENS = 32645;
    private const uint OCR_SIZEALL = 32646;
    private const uint OCR_NO = 32648;
    private const uint OCR_HAND = 32649;
    private const uint OCR_APPSTARTING = 32650;

    private const uint SPI_SETCURSORS = 0x0057;
    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;

    [DllImport("user32.dll")]
    private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyImage(IntPtr h, uint uType, int cxDesired, int cyDesired, uint fuFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static readonly uint[] AllCursorIds =
    {
        OCR_NORMAL, OCR_IBEAM, OCR_WAIT, OCR_CROSS, OCR_UP,
        OCR_SIZENWSE, OCR_SIZENESW, OCR_SIZEWE, OCR_SIZENS,
        OCR_SIZEALL, OCR_NO, OCR_HAND, OCR_APPSTARTING
    };

    private static bool _isHidden;

    public static bool IsHidden => _isHidden;

    /// <summary>
    /// Hides all system cursors by replacing them with a blank (transparent) cursor.
    /// Uses system cursor dimensions for maximum compatibility.
    /// </summary>
    public static void Hide()
    {
        if (_isHidden) return;

        // Get system cursor size (typically 32x32)
        var cx = GetSystemMetrics(SM_CXCURSOR);
        var cy = GetSystemMetrics(SM_CYCURSOR);
        if (cx <= 0) cx = 32;
        if (cy <= 0) cy = 32;

        // AND mask: all 0xFF = fully transparent background
        // Each row is padded to WORD (2-byte) boundary: ceil(cx/8) rounded up to even
        var andStride = ((cx + 15) / 16) * 2;
        var andMask = new byte[andStride * cy];
        Array.Fill(andMask, (byte)0xFF);

        // XOR mask: all 0x00 = no visible pixels
        var xorMask = new byte[andStride * cy];
        Array.Fill(xorMask, (byte)0x00);

        var blankCursor = CreateCursor(IntPtr.Zero, 0, 0, cx, cy, andMask, xorMask);
        if (blankCursor == IntPtr.Zero) return;

        foreach (var cursorId in AllCursorIds)
        {
            // CopyImage with IMAGE_CURSOR (2) to create a copy for each SetSystemCursor call
            // (SetSystemCursor takes ownership and destroys the cursor handle)
            var copy = CopyImage(blankCursor, 2, 0, 0, 0);
            if (copy != IntPtr.Zero)
            {
                SetSystemCursor(copy, cursorId);
            }
        }

        DestroyCursor(blankCursor);
        _isHidden = true;
    }

    /// <summary>
    /// Restores all system cursors to their defaults.
    /// </summary>
    public static void Restore()
    {
        if (!_isHidden) return;

        // SPI_SETCURSORS reloads all system cursors from the registry defaults
        SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        _isHidden = false;
    }
}
