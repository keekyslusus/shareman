using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace GDriveTelegramSender.Ui;

public static class ThemeManager
{
    private static bool? _currentIsLight;
    private static readonly List<Window> RegisteredWindows = new();

    private static Color DarkMusicPrimary { get; } = Color.FromRgb(0xD0, 0xBC, 0xFF);
    private static Color LightMusicPrimary { get; } = Color.FromRgb(0x67, 0x50, 0xA4);
    private static Color DarkDockSurface { get; } = Color.FromArgb(0xE6, 0x20, 0x21, 0x24);
    private static Color LightDockSurface { get; } = Color.FromArgb(0xF0, 0xFC, 0xFC, 0xFD);

    private static readonly PaletteState DarkState = new(
        Surface: Color.FromRgb(0x21, 0x1F, 0x26),
        Text: Color.FromRgb(0xE6, 0xE1, 0xE5),
        MutedText: Color.FromRgb(0xCA, 0xC4, 0xD0),
        Border: Color.FromRgb(0x44, 0x47, 0x46),
        PrimaryContainer: Color.FromRgb(0x4F, 0x37, 0x8B));

    private static readonly PaletteState LightState = new(
        Surface: Color.FromRgb(0xF3, 0xF3, 0xFA),
        Text: Color.FromRgb(0x1C, 0x1B, 0x1F),
        MutedText: Color.FromRgb(0x49, 0x45, 0x4F),
        Border: Color.FromRgb(0xC4, 0xC7, 0xC5),
        PrimaryContainer: Color.FromRgb(0xEA, 0xDD, 0xFF));

    static ThemeManager()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not 0;
        }
        catch
        {
            return false;
        }
    }

    public static void RegisterWindow(Window window)
    {
        if (!RegisteredWindows.Contains(window))
        {
            RegisteredWindows.Add(window);
            window.Closed += (s, e) => RegisteredWindows.Remove(window);
            window.SourceInitialized += (s, e) =>
            {
                var handle = new WindowInteropHelper(window).Handle;
                NativeMethods.SetWindowTheme(handle, _currentIsLight ?? IsLight());
            };
        }
    }

    public static void Initialize(Window? mainWindow = null)
    {
        if (mainWindow != null)
        {
            RegisterWindow(mainWindow);
        }
        ApplyTheme(IsLight());
    }

    public static void ApplyTheme(bool isLight)
    {
        _currentIsLight = isLight;
        var app = Application.Current;
        if (app == null) return;

        var dict = app.Resources;
        var state = isLight ? LightState : DarkState;
        var paper = isLight ? Color.FromRgb(0xF7, 0xF9, 0xFC) : Color.FromRgb(0x20, 0x21, 0x24);
        var surface = isLight ? Color.FromRgb(0xFC, 0xFC, 0xFD) : state.Surface;
        var wash = Composite(paper, WithAlpha(state.PrimaryContainer, 0.25));
        var card = isLight ? surface : Composite(surface, WithAlpha(state.Text, 0.03));
        var accent = isLight ? LightMusicPrimary : DarkMusicPrimary;
        var accentForeground = isLight ? Colors.White : Color.FromRgb(0x20, 0x21, 0x24);
        var heroStart = Composite(paper, WithAlpha(state.PrimaryContainer, 0.45));
        var heroEnd = Composite(paper, WithAlpha(state.PrimaryContainer, 0.12));

        (string key, Color color)[] colors =
        [
            ("SettingsPaper", paper),
            ("SettingsSurface", surface),
            ("SettingsCard", card),
            ("SettingsWash", wash),
            ("SettingsLine", WithAlpha(state.Text, 0.10)),
            ("SettingsHover", WithAlpha(state.Text, 0.05)),
            ("SettingsSelected", WithAlpha(state.Text, 0.08)),
            ("SettingsText", state.Text),
            ("SettingsMuted", state.MutedText),
            ("SettingsSectionTitle", isLight ? Colors.Black : Colors.White),
            ("SettingsAccent", accent),
            ("SettingsAccentForeground", accentForeground),
            ("SettingsAccentLine", WithAlpha(accent, 0.18)),
            ("SettingsHeroStart", heroStart),
            ("SettingsHeroEnd", heroEnd),
            ("SettingsSuccess", isLight ? Color.FromRgb(0x18, 0x80, 0x38) : Color.FromRgb(0x81, 0xC9, 0x95)),
            ("SettingsWarning", isLight ? Color.FromRgb(0xBA, 0x6E, 0x00) : Color.FromRgb(0xFD, 0xD6, 0x63)),
            ("SettingsError", isLight ? Color.FromRgb(0xD9, 0x30, 0x25) : Color.FromRgb(0xF2, 0x8B, 0x82)),
            ("SettingsScrollbarThumb", isLight ? Color.FromArgb(0x8F, 0x30, 0x34, 0x3A) : Color.FromArgb(0xA6, 0xE8, 0xEA, 0xED)),
        ];

        foreach (var (key, color) in colors)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            dict[key] = brush;
            dict[key + "Color"] = color;
        }

        dict["SettingsTransparent"] = Brushes.Transparent;
        dict["SettingsScrollTrackWidth"] = (double)OverlayScrollbarPolicy.TrackWidthPixels;
        dict["SettingsScrollThumbWidth"] = (double)OverlayScrollbarPolicy.ThumbWidthPixels;
        dict["SettingsScrollMinThumbHeight"] = (double)OverlayScrollbarPolicy.MinimumThumbHeightPixels;
        dict["SettingsScrollTrackMargin"] = new Thickness(0, OverlayScrollbarPolicy.EdgeInsetPixels, 0, OverlayScrollbarPolicy.EdgeInsetPixels);
        dict["SettingsScrollThumbMargin"] = new Thickness(0, 0, OverlayScrollbarPolicy.EdgeInsetPixels, 0);

        foreach (var window in RegisteredWindows)
        {
            if (window.IsLoaded)
            {
                var handle = new WindowInteropHelper(window).Handle;
                NativeMethods.SetWindowTheme(handle, isLight);
            }
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            var isLight = IsLight();
            if (isLight != _currentIsLight)
            {
                Application.Current?.Dispatcher.InvokeAsync(() => ApplyTheme(isLight));
            }
        }
    }

    private static Color Composite(Color background, Color foreground)
    {
        var foregroundAlpha = foreground.A / 255d;
        var backgroundAlpha = background.A / 255d;
        var alpha = foregroundAlpha + backgroundAlpha * (1 - foregroundAlpha);
        if (alpha == 0) return Colors.Transparent;
        byte Blend(byte bg, byte fg) => (byte)Math.Round(
            (fg * foregroundAlpha + bg * backgroundAlpha * (1 - foregroundAlpha)) / alpha);
        return Color.FromArgb(
            (byte)Math.Round(alpha * 255),
            Blend(background.R, foreground.R),
            Blend(background.G, foreground.G),
            Blend(background.B, foreground.B));
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Clamp((int)Math.Round(255 * alpha), 0, 255), color.R, color.G, color.B);

    private sealed record PaletteState(
        Color Surface,
        Color Text,
        Color MutedText,
        Color Border,
        Color PrimaryContainer);
}
