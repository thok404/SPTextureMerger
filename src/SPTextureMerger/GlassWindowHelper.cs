using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SPTextureMerger;

internal static class GlassWindowHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetAttribute(handle, DwmwaUseImmersiveDarkMode, 1);
        SetAttribute(handle, DwmwaWindowCornerPreference, 2);
        SetAttribute(handle, DwmwaSystemBackdropType, 2);
    }

    private static void SetAttribute(IntPtr handle, int attribute, int value)
    {
        _ = DwmSetWindowAttribute(handle, attribute, ref value, Marshal.SizeOf<int>());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}

