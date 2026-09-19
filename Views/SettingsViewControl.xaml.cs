using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GDriveTelegramSender.Services;
using GDriveTelegramSender.Ui;
using Microsoft.Win32;

namespace GDriveTelegramSender.Views;

public partial class SettingsViewControl : UserControl, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly GoogleDriveService _driveService;
    private readonly TelegramClientService _telegramService;
    private readonly BackupService _backupService;
    private readonly ShellIntegrationService _shellService;
    private readonly LocalizationService _localizationService;

    private SettingsScrollController? _settingsScrollController;
    private SettingsScrollMotionController? _settingsScrollMotion;

    public event Action? RequestReturnToSend;
    public event Action? AuthStateChanged;

    public SettingsViewControl(
        SettingsService settingsService,
        GoogleDriveService driveService,
        TelegramClientService telegramService,
        BackupService backupService,
        ShellIntegrationService shellService,
        LocalizationService localizationService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _shellService = shellService ?? throw new ArgumentNullException(nameof(shellService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        LanguageComboBox.ItemsSource = LocalizationService.SupportedLanguages;
        LanguageComboBox.SelectedValue = _localizationService.ConfiguredLanguage;
        UpdateLanguageVisibility();

        _localizationService.LanguageChanged += OnLanguageChanged;

        Loaded += SettingsViewControl_Loaded;
    }

    private async void SettingsViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settingsScrollController == null)
        {
            _settingsScrollController = new SettingsScrollController(SettingsScrollViewer);
            _settingsScrollMotion = new SettingsScrollMotionController(
                SettingsScrollViewer,
                (TranslateTransform)SettingsContentPanel.RenderTransform);
        }

        LoadSettingsToUi();
        UpdateSendToStatus();
        await UpdateAuthBadgesAsync();
    }

    public void OnLanguageChanged()
    {
        UpdateSendToStatus();
        _ = UpdateAuthBadgesAsync();
        UpdateLanguageVisibility();
        LanguageComboBox.Items.Refresh();
    }

    private void UpdateLanguageVisibility()
    {
        LanguageRow.Visibility = _localizationService.HasMultipleLanguages
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedValue is string lang && lang != _localizationService.ConfiguredLanguage)
        {
            _localizationService.SetLanguage(lang);
            _settingsService.Settings.Language = lang;
            _settingsService.SaveSettings();
        }
    }

    public void LoadSettingsToUi()
    {
        var s = _settingsService.Settings;
        TgApiIdBox.Text = s.TelegramApiId > 0 ? s.TelegramApiId.ToString() : "";
        TgApiHashBox.Text = s.TelegramApiHash ?? "";
        TgPhoneBox.Text = s.TelegramPhoneNumber ?? "";

        GoogleClientIdBox.Text = s.GoogleClientId ?? "";
        GoogleClientSecretBox.Text = s.GoogleClientSecret ?? "";

        UpdateLanguageVisibility();
        LanguageComboBox.SelectedValue = !string.IsNullOrEmpty(s.Language) ? s.Language : LocalizationService.SystemLanguageCode;
        AutoCloseCheckBox.IsChecked = s.AutoCloseOnSuccess;
    }

    public void SaveUiToSettings()
    {
        var s = _settingsService.Settings;
        if (int.TryParse(TgApiIdBox.Text.Trim(), out int apiId))
        {
            s.TelegramApiId = apiId;
        }
        s.TelegramApiHash = TgApiHashBox.Text.Trim();
        s.TelegramPhoneNumber = TgPhoneBox.Text.Trim();

        s.GoogleClientId = GoogleClientIdBox.Text.Trim();
        s.GoogleClientSecret = GoogleClientSecretBox.Text.Trim();

        if (LanguageComboBox.SelectedValue is string lang)
        {
            s.Language = lang;
        }

        s.AutoCloseOnSuccess = AutoCloseCheckBox.IsChecked ?? true;

        _settingsService.SaveSettings();
    }

    public async Task UpdateAuthBadgesAsync()
    {
        // Telegram Badge
        try
        {
            var user = await _telegramService.GetCurrentUserAsync();
            if (user != null)
            {
                string name = $"{user.first_name} {user.last_name}".Trim();
                if (!string.IsNullOrWhiteSpace(user.MainUsername))
                {
                    name += $" (@{user.MainUsername})";
                }
                TgStatusBadge.Text = name;
                TgStatusBadge.Foreground = (Brush)FindResource("SettingsAccent");
                TgStatusDot.Fill = (Brush)FindResource("SettingsAccent");
                TgLoginBtn.Visibility = Visibility.Collapsed;
                TgLogoutBtn.Visibility = Visibility.Visible;
            }
            else
            {
                TgStatusBadge.Text = _localizationService.Get("Status_NotConnected");
                TgStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
                TgStatusDot.Fill = (Brush)FindResource("SettingsMuted");
                TgLoginBtn.Visibility = Visibility.Visible;
                TgLogoutBtn.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            TgStatusBadge.Text = _localizationService.Get("Status_NotConnected");
            TgStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
            TgStatusDot.Fill = (Brush)FindResource("SettingsMuted");
            TgLoginBtn.Visibility = Visibility.Visible;
            TgLogoutBtn.Visibility = Visibility.Collapsed;
        }

        // Google Badge
        try
        {
            bool gdriveAuth = await _driveService.IsAuthorizedAsync();
            if (gdriveAuth)
            {
                string? email = await _driveService.GetUserEmailAsync();
                GoogleStatusBadge.Text = !string.IsNullOrWhiteSpace(email) ? email : _localizationService.Get("Status_Authorized");
                GoogleStatusBadge.Foreground = (Brush)FindResource("SettingsAccent");
                GoogleStatusDot.Fill = (Brush)FindResource("SettingsAccent");
                GoogleAuthBtn.Visibility = Visibility.Collapsed;
                GoogleResetBtn.Visibility = Visibility.Visible;
            }
            else
            {
                GoogleStatusBadge.Text = _driveService.IsConfigured ? _localizationService.Get("Status_LoginRequired") : _localizationService.Get("Status_NotConfigured");
                GoogleStatusBadge.Foreground = _driveService.IsConfigured
                    ? (Brush)FindResource("SettingsWarning")
                    : (Brush)FindResource("SettingsMuted");
                GoogleStatusDot.Fill = _driveService.IsConfigured
                    ? (Brush)FindResource("SettingsWarning")
                    : (Brush)FindResource("SettingsMuted");
                GoogleAuthBtn.Visibility = Visibility.Visible;
                GoogleResetBtn.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            GoogleStatusBadge.Text = _localizationService.Get("Status_NotConfigured");
            GoogleStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
            GoogleStatusDot.Fill = (Brush)FindResource("SettingsMuted");
            GoogleAuthBtn.Visibility = Visibility.Visible;
            GoogleResetBtn.Visibility = Visibility.Collapsed;
        }
    }

    public void UpdateSendToStatus()
    {
        bool installed = _shellService.IsShortcutInstalled();
        if (installed)
        {
            SendToStatusBadge.Text = _localizationService.Get("Status_Added");
            SendToStatusBadge.Foreground = (Brush)FindResource("SettingsAccent");
            SendToStatusDot.Fill = (Brush)FindResource("SettingsAccent");
            AddSendToBtn.Visibility = Visibility.Collapsed;
            RemoveSendToBtn.Visibility = Visibility.Visible;
        }
        else
        {
            SendToStatusBadge.Text = _localizationService.Get("Status_NotAdded");
            SendToStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
            SendToStatusDot.Fill = (Brush)FindResource("SettingsMuted");
            AddSendToBtn.Visibility = Visibility.Visible;
            RemoveSendToBtn.Visibility = Visibility.Collapsed;
        }
    }

    private async void TgLoginBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();

        if (_settingsService.Settings.TelegramApiId <= 0 || string.IsNullOrWhiteSpace(_settingsService.Settings.TelegramApiHash))
        {
            MessageBox.Show(_localizationService.Get("Msg_FillTgApiCredentials_Body"), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            TgLoginBtn.IsEnabled = false;
            TgStatusBadge.Text = _localizationService.Get("Status_LoggingIn");
            TgStatusBadge.Foreground = (Brush)FindResource("SettingsWarning");
            TgStatusDot.Fill = (Brush)FindResource("SettingsWarning");

            var ownerWindow = Window.GetWindow(this);

            var user = await _telegramService.LoginAsync(promptType =>
            {
                return Dispatcher.Invoke(() =>
                {
                    if (promptType == "verification_code")
                    {
                        var dlg = new PromptDialog(_localizationService.Get("Prompt_VerificationCode_Title"), _localizationService.Get("Prompt_VerificationCode_Body"))
                        {
                            Owner = ownerWindow
                        };
                        return dlg.ShowDialog() == true ? dlg.ResponseText : null;
                    }
                    if (promptType == "password")
                    {
                        var dlg = new PromptDialog(_localizationService.Get("Prompt_2FA_Title"), _localizationService.Get("Prompt_2FA_Body"), isPassword: true)
                        {
                            Owner = ownerWindow
                        };
                        return dlg.ShowDialog() == true ? dlg.ResponseText : null;
                    }
                    if (promptType == "phone_number")
                    {
                        var dlg = new PromptDialog(_localizationService.Get("Prompt_Phone_Title"), _localizationService.Get("Prompt_Phone_Body"))
                        {
                            Owner = ownerWindow
                        };
                        return dlg.ShowDialog() == true ? dlg.ResponseText : null;
                    }
                    return null;
                });
            });

            if (user != null)
            {
                await UpdateAuthBadgesAsync();
                MessageBox.Show(_localizationService.Format("Msg_TgAuthSuccess_Body", user.first_name, user.last_name), _localizationService.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                AuthStateChanged?.Invoke();
            }
            else
            {
                await UpdateAuthBadgesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(_localizationService.Format("Msg_TgAuthError_Body", ex.Message), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            await UpdateAuthBadgesAsync();
        }
        finally
        {
            TgLoginBtn.IsEnabled = true;
        }
    }

    private void TgLogoutBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(_localizationService.Get("Msg_ConfirmTgLogout_Body"), _localizationService.Get("Msg_ConfirmTgLogout_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _telegramService.Logout();
            _ = UpdateAuthBadgesAsync();
            AuthStateChanged?.Invoke();
            MessageBox.Show(_localizationService.Get("Msg_TgSessionDeleted_Body"), _localizationService.Get("Msg_TgSessionDeleted_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void LoadSecretJsonBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = _localizationService.Get("Dialog_SelectJson_Title"),
            Filter = _localizationService.Get("Dialog_SelectJson_Filter")
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            try
            {
                string json = File.ReadAllText(dialog.FileName);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                JsonElement appElem = default;
                if (root.TryGetProperty("installed", out var inst))
                {
                    appElem = inst;
                }
                else if (root.TryGetProperty("web", out var web))
                {
                    appElem = web;
                }

                if (appElem.ValueKind != JsonValueKind.Undefined)
                {
                    if (appElem.TryGetProperty("client_id", out var cid))
                    {
                        GoogleClientIdBox.Text = cid.GetString();
                    }
                    if (appElem.TryGetProperty("client_secret", out var cs))
                    {
                        GoogleClientSecretBox.Text = cs.GetString() ?? string.Empty;
                    }
                    MessageBox.Show(_localizationService.Get("Msg_GoogleKeysLoaded_Body"), _localizationService.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(_localizationService.Get("Msg_GoogleKeysFormatWarning_Body"), _localizationService.Get("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(_localizationService.Format("Msg_ReadJsonError_Body", ex.Message), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void GoogleAuthBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();

        if (string.IsNullOrWhiteSpace(_settingsService.Settings.GoogleClientId) ||
            string.IsNullOrWhiteSpace(_settingsService.Settings.GoogleClientSecret))
        {
            MessageBox.Show(_localizationService.Get("Msg_FillGoogleCredentials_Body"), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            GoogleAuthBtn.IsEnabled = false;
            GoogleStatusBadge.Text = _localizationService.Get("Status_BrowserLogin");
            GoogleStatusBadge.Foreground = (Brush)FindResource("SettingsWarning");
            GoogleStatusDot.Fill = (Brush)FindResource("SettingsWarning");

            bool success = await _driveService.AuthorizeAsync();
            if (success)
            {
                await UpdateAuthBadgesAsync();
                MessageBox.Show(_localizationService.Get("Msg_GoogleAuthSuccess_Body"), _localizationService.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                AuthStateChanged?.Invoke();
            }
            else
            {
                await UpdateAuthBadgesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(_localizationService.Format("Msg_GoogleAuthError_Body", ex.Message), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            await UpdateAuthBadgesAsync();
        }
        finally
        {
            GoogleAuthBtn.IsEnabled = true;
        }
    }

    private void GoogleResetBtn_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(_localizationService.Get("Msg_ConfirmGoogleReset_Body"), _localizationService.Get("Msg_ConfirmGoogleReset_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _driveService.ResetAuthorization();
            _ = UpdateAuthBadgesAsync();
            AuthStateChanged?.Invoke();
            MessageBox.Show(_localizationService.Get("Msg_GoogleResetDone_Body"), _localizationService.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AddSendToBtn_Click(object sender, RoutedEventArgs e)
    {
        bool ok = _shellService.InstallShortcut();
        if (ok)
        {
            UpdateSendToStatus();
            MessageBox.Show(_localizationService.Get("Msg_SendToAdded_Body"), _localizationService.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(_localizationService.Get("Msg_SendToCreateError_Body"), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveSendToBtn_Click(object sender, RoutedEventArgs e)
    {
        bool ok = _shellService.UninstallShortcut();
        if (ok)
        {
            UpdateSendToStatus();
            MessageBox.Show(_localizationService.Get("Msg_SendToRemoved_Body"), _localizationService.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void CreateBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUiToSettings();

            var dialog = new SaveFileDialog
            {
                Title = _localizationService.Get("Settings_Backup_CreateBtn"),
                Filter = BackupService.DialogFilter,
                DefaultExt = BackupService.BackupFileExtension,
                FileName = $"shareman_Backup_{DateTime.Now:yyyy-MM-dd_HHmm}.gdtbak"
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                _telegramService.ResetClient();
                _backupService.CreateBackup(dialog.FileName);

                await UpdateAuthBadgesAsync();

                MessageBox.Show(
                    _localizationService.Format("Msg_BackupCreated_Body", dialog.FileName),
                    _localizationService.Get("Msg_BackupCreated_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Msg_BackupCreateError_Body", ex.Message),
                _localizationService.Get("Common_Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RestoreBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = _localizationService.Get("Settings_Backup_RestoreBtn"),
                Filter = BackupService.DialogFilter,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            var confirm = MessageBox.Show(
                _localizationService.Get("Msg_RestoreConfirm_Body"),
                _localizationService.Get("Msg_RestoreConfirm_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _telegramService.ResetClient();
            _driveService.ResetAuthorization();

            _backupService.RestoreBackup(dialog.FileName);

            _settingsService.LoadSettings();
            LoadSettingsToUi();
            await UpdateAuthBadgesAsync();

            AuthStateChanged?.Invoke();

            MessageBox.Show(
                _localizationService.Get("Msg_RestoreSuccess_Body"),
                _localizationService.Get("Msg_RestoreSuccess_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Msg_RestoreError_Body", ex.Message),
                _localizationService.Get("Common_Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();
        _telegramService.ResetClient();
        await UpdateAuthBadgesAsync();
        AuthStateChanged?.Invoke();
        RequestReturnToSend?.Invoke();
    }

    public void Dispose()
    {
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _settingsScrollController?.Dispose();
        _settingsScrollController = null;
        _settingsScrollMotion?.Dispose();
        _settingsScrollMotion = null;
    }
}
