using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HNice.Util;

/// <summary>
/// Makes the native Windows title bar match the dark theme (Windows 10 20H1+ / 11).
/// Keeps the real OS caption (snap, drag, buttons) instead of drawing a custom one.
/// </summary>
public static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        if (new WindowInteropHelper(window).Handle is var handle && handle != IntPtr.Zero)
        {
            Set(handle);
        }
        else
        {
            window.SourceInitialized += (_, _) => Set(new WindowInteropHelper(window).Handle);
        }
    }

    private static void Set(IntPtr handle)
    {
        try
        {
            var enabled = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));

            // Caption colour = Surface1 (#191D28) as COLORREF 0x00BBGGRR. Ignored before Windows 11.
            var caption = 0x00281D19;
            DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Older Windows: keep the default caption.
        }
    }
}
