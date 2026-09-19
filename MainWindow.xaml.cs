using System;
using System.IO;
using System.Windows;
using GDriveTelegramSender.Services;
using GDriveTelegramSender.Ui;
using GDriveTelegramSender.Views;

namespace GDriveTelegramSender;

public partial class MainWindow : Window
{
    private readonly SendViewControl _sendView;
    private readonly SettingsViewControl _settingsView;
    private readonly LocalizationService _localizationService;
    private readonly TelegramClientService _telegramService;
    private readonly GoogleDriveService _driveService;

    private readonly string? _initialFilePath;
    private ViewTransition? _viewTransition;

    public MainWindow(
        string? filePath,
        SendViewControl sendView,
        SettingsViewControl settingsView,
        LocalizationService localizationService,
        TelegramClientService telegramService,
        GoogleDriveService driveService)
    {
        InitializeComponent();

        _initialFilePath = filePath;
        _sendView = sendView ?? throw new ArgumentNullException(nameof(sendView));
        _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));

        ThemeManager.Initialize(this);

        _sendView.Visibility = Visibility.Visible;
        _settingsView.Visibility = Visibility.Collapsed;
        ViewsTransitionSurface.Children.Add(_sendView);
        ViewsTransitionSurface.Children.Add(_settingsView);

        _localizationService.LanguageChanged += OnLanguageChanged;

        _sendView.RequestOpenSettings += ShowSettingsView;
        _sendView.RequestClose += Close;

        _settingsView.RequestReturnToSend += async () =>
        {
            ShowSendView();
            await _sendView.LoadContactsAsync();
        };

        _settingsView.AuthStateChanged += async () =>
        {
            await _sendView.LoadContactsAsync();
        };

        Loaded += MainWindow_Loaded;
        Closed += (_, _) =>
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _viewTransition?.Dispose();
            _sendView.Dispose();
            _settingsView.Dispose();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewTransition = new ViewTransition(ViewsTransitionSurface, [_sendView, _settingsView]);

        if (!string.IsNullOrEmpty(_initialFilePath) && File.Exists(_initialFilePath))
        {
            _sendView.SetSelectedFile(_initialFilePath);
            ShowSendView();
            await _sendView.LoadContactsAsync();
        }
        else
        {
            bool tgConfigured = _telegramService.IsConfigured && _telegramService.HasSessionFile;
            bool gdriveConfigured = _driveService.IsConfigured;

            if (!tgConfigured || !gdriveConfigured)
            {
                ShowSettingsView();
            }
            else
            {
                ShowSendView();
                await _sendView.LoadContactsAsync();
            }
        }
    }

    private void OnLanguageChanged()
    {
        if (_sendView.Visibility == Visibility.Visible)
        {
            ToggleSettingsText.Text = _localizationService.Get("Common_Settings");
        }
        else
        {
            ToggleSettingsText.Text = _localizationService.Get("Common_BackToSend");
        }
    }

    private void ShowSendView()
    {
        _viewTransition?.Show(_sendView);
        ToggleSettingsIcon.Data = AppIcons.SettingsOutlined;
        ToggleSettingsText.Text = _localizationService.Get("Common_Settings");
    }

    private void ShowSettingsView()
    {
        _viewTransition?.Show(_settingsView);
        ToggleSettingsIcon.Data = AppIcons.ArrowBackOutlined;
        ToggleSettingsText.Text = _localizationService.Get("Common_BackToSend");
    }

    private void ToggleSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_sendView.IsUploading)
        {
            MessageBox.Show(
                _localizationService.Get("Msg_UploadInProgress_Body"),
                _localizationService.Get("Msg_UploadInProgress_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_sendView.Visibility == Visibility.Visible)
        {
            ShowSettingsView();
        }
        else
        {
            ShowSendView();
            if (_telegramService.IsConfigured && _telegramService.HasSessionFile)
            {
                _ = _sendView.LoadContactsAsync();
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && File.Exists(files[0]))
            {
                _sendView.SetSelectedFile(files[0]);
                ShowSendView();
            }
        }
    }
}