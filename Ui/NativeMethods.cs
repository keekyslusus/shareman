using System;
using System.Runtime.InteropServices;

namespace GDriveTelegramSender.Ui;

internal static class NativeMethods
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    internal static void EnableImmersiveDarkMode(IntPtr hwnd) => SetWindowTheme(hwnd, isLight: false);

    internal static void SetWindowTheme(IntPtr hwnd, bool isLight)
    {
        if (Environment.OSVersion.Version.Major >= 10 && hwnd != IntPtr.Zero)
        {
            int darkMode = isLight ? 0 : 1;
            int hr = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
            if (hr != 0)
            {
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref darkMode, sizeof(int));
            }
        }
    }
}
