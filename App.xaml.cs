using System.IO;
using System.Windows;
using GDriveTelegramSender.Services;

namespace GDriveTelegramSender;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LocalizationService.Instance.Initialize(SettingsService.Instance.Settings.Language);

        string? filePath = null;
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            filePath = e.Args[0];
        }

        var mainWindow = new MainWindow(filePath);
        mainWindow.Show();
    }
}
