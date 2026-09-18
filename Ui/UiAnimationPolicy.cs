using System;
using System.Windows;

namespace GDriveTelegramSender.Ui;

public static class UiAnimationPolicy
{
    public static bool Enabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    public static Duration ToggleTransitionDuration => new(Enabled ? TimeSpan.FromMilliseconds(180) : TimeSpan.Zero);
}
