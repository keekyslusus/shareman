using System.IO;
using System.Windows;
using GDriveTelegramSender.Services;
using GDriveTelegramSender.Ui;
using GDriveTelegramSender.Views;

namespace GDriveTelegramSender;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeManager.Initialize();

        // 1. Settings & Core Infrastructure
        var settingsService = new SettingsService();

        // 2. Localization
        var localizationService = LocalizationService.Instance;
        localizationService.Initialize(settingsService.Settings.Language);

        // 3. Concrete Services (Constructor Injection)
        var driveService = new GoogleDriveService(settingsService);
        var telegramService = new TelegramClientService(settingsService);
        var backupService = new BackupService(settingsService);
        var shellService = new ShellIntegrationService();

        // 4. Workflow Coordinators
        var sendWorkflow = new SendWorkflowCoordinator(driveService, telegramService, settingsService);

        // 5. Views & UI Components
        var sendViewControl = new SendViewControl(sendWorkflow, telegramService, settingsService, localizationService);
        var settingsViewControl = new SettingsViewControl(
            settingsService,
            driveService,
            telegramService,
            backupService,
            shellService,
            localizationService);

        // 6. Main Window Assembly
        string? filePath = null;
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            filePath = e.Args[0];
        }

        var mainWindow = new MainWindow(
            filePath,
            sendViewControl,
            settingsViewControl,
            localizationService,
            telegramService,
            driveService);

        mainWindow.Show();
    }
}
