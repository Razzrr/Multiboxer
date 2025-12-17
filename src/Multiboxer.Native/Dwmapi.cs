using System.Runtime.InteropServices;

namespace Multiboxer.Native;

/// <summary>
/// P/Invoke declarations for dwmapi.dll - Desktop Window Manager APIs
/// </summary>
public static class Dwmapi
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    public static extern int DwmIsCompositionEnabled(out bool pfEnabled);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

    [DllImport("dwmapi.dll")]
    public static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    // DWM Thumbnail APIs - for Video FX style rendering
    [DllImport("dwmapi.dll")]
    public static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

    [DllImport("dwmapi.dll")]
    public static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnailId, out SIZE pSize);
}

#region Enums and Structs

public enum DwmWindowAttribute : uint
{
    DWMWA_NCRENDERING_ENABLED = 1,
    DWMWA_NCRENDERING_POLICY = 2,
    DWMWA_TRANSITIONS_FORCEDISABLED = 3,
    DWMWA_ALLOW_NCPAINT = 4,
    DWMWA_CAPTION_BUTTON_BOUNDS = 5,
    DWMWA_NONCLIENT_RTL_LAYOUT = 6,
    DWMWA_FORCE_ICONIC_REPRESENTATION = 7,
    DWMWA_FLIP3D_POLICY = 8,
    DWMWA_EXTENDED_FRAME_BOUNDS = 9,
    DWMWA_HAS_ICONIC_BITMAP = 10,
    DWMWA_DISALLOW_PEEK = 11,
    DWMWA_EXCLUDED_FROM_PEEK = 12,
    DWMWA_CLOAK = 13,
    DWMWA_CLOAKED = 14,
    DWMWA_FREEZE_REPRESENTATION = 15,
    DWMWA_PASSIVE_UPDATE_MODE = 16,
    DWMWA_USE_HOSTBACKDROPBRUSH = 17,
    DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
    DWMWA_WINDOW_CORNER_PREFERENCE = 33,
    DWMWA_BORDER_COLOR = 34,
    DWMWA_CAPTION_COLOR = 35,
    DWMWA_TEXT_COLOR = 36,
    DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 37,
    DWMWA_SYSTEMBACKDROP_TYPE = 38
}

public enum DwmWindowCornerPreference : uint
{
    DWMWCP_DEFAULT = 0,
    DWMWCP_DONOTROUND = 1,
    DWMWCP_ROUND = 2,
    DWMWCP_ROUNDSMALL = 3
}

public enum DwmSystemBackdropType : uint
{
    DWMSBT_AUTO = 0,
    DWMSBT_NONE = 1,
    DWMSBT_MAINWINDOW = 2,  // Mica
    DWMSBT_TRANSIENTWINDOW = 3,  // Acrylic
    DWMSBT_TABBEDWINDOW = 4  // Tabbed Mica
}

[StructLayout(LayoutKind.Sequential)]
public struct MARGINS
{
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;

    public MARGINS(int left, int right, int top, int bottom)
    {
        cxLeftWidth = left;
        cxRightWidth = right;
        cyTopHeight = top;
        cyBottomHeight = bottom;
    }

    /// <summary>
    /// Creates margins that extend into the entire client area
    /// </summary>
    public static MARGINS ExtendAll => new(-1, -1, -1, -1);
}

[StructLayout(LayoutKind.Sequential)]
public struct DWM_BLURBEHIND
{
    public DwmBlurBehindFlags dwFlags;
    public bool fEnable;
    public IntPtr hRgnBlur;
    public bool fTransitionOnMaximized;
}

[Flags]
public enum DwmBlurBehindFlags : uint
{
    DWM_BB_ENABLE = 0x00000001,
    DWM_BB_BLURREGION = 0x00000002,
    DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004
}

// DWM Thumbnail structures
[StructLayout(LayoutKind.Sequential)]
public struct DWM_THUMBNAIL_PROPERTIES
{
    public DwmThumbnailFlags dwFlags;
    public RECT rcDestination;
    public RECT rcSource;
    public byte opacity;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fVisible;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fSourceClientAreaOnly;
}

[Flags]
public enum DwmThumbnailFlags : uint
{
    DWM_TNP_RECTDESTINATION = 0x00000001,
    DWM_TNP_RECTSOURCE = 0x00000002,
    DWM_TNP_OPACITY = 0x00000004,
    DWM_TNP_VISIBLE = 0x00000008,
    DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010
}

[StructLayout(LayoutKind.Sequential)]
public struct SIZE
{
    public int cx;
    public int cy;
}

#endregion
