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

    private const int GwlExStyle = -20;
    private const int WsExDlgModalFrame = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const uint WmSetIcon = 0x0080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    internal static void RemoveWindowIcon(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        int extendedStyle = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, extendedStyle | WsExDlgModalFrame);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
        SendMessage(hwnd, WmSetIcon, (IntPtr)0, IntPtr.Zero);
        SendMessage(hwnd, WmSetIcon, (IntPtr)1, IntPtr.Zero);
    }

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
