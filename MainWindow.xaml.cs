using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GDriveTelegramSender.Models;
using GDriveTelegramSender.Services;
using GDriveTelegramSender.Ui;
using GDriveTelegramSender.Views;
using Microsoft.Win32;

namespace GDriveTelegramSender;

public partial class MainWindow : Window
{
    private string? _selectedFilePath;
    private readonly SettingsService _settingsService = SettingsService.Instance;
    private readonly GoogleDriveService _driveService = new();
    private readonly TelegramClientService _telegramService = new();

    private readonly List<TelegramChatItem> _allChats = new();
    private readonly ObservableCollection<TelegramChatItem> _filteredChats = new();

    private CancellationTokenSource? _uploadCts;
    private bool _isUploading;
    private ViewTransition? _viewTransition;
    private SettingsScrollController? _settingsScrollController;
    private SettingsScrollMotionController? _settingsScrollMotion;
    private SettingsScrollController? _contactsScrollController;
    private SettingsScrollMotionController? _contactsScrollMotion;

    public MainWindow(string? filePath = null)
    {
        InitializeComponent();

        ContactsListBox.ItemsSource = _filteredChats;
        _selectedFilePath = filePath;

        ThemeManager.Initialize(this);

        LanguageComboBox.ItemsSource = LocalizationService.SupportedLanguages;
        LanguageComboBox.SelectedValue = LocalizationService.Instance.ConfiguredLanguage;
        UpdateLanguageVisibility();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        Loaded += MainWindow_Loaded;
        Closed += (_, _) =>
        {
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            _viewTransition?.Dispose();
            _settingsScrollController?.Dispose();
            _settingsScrollMotion?.Dispose();
            _contactsScrollController?.Dispose();
            _contactsScrollMotion?.Dispose();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewTransition = new ViewTransition(ViewsTransitionSurface, [SendView, SettingsView]);

        _settingsScrollController = new SettingsScrollController(SettingsView);
        _settingsScrollMotion = new SettingsScrollMotionController(
            SettingsView,
            (TranslateTransform)SettingsContentPanel.RenderTransform);

        HookContactsScroll();
        ContactsListBox.Loaded += (_, _) => HookContactsScroll();

        LoadSettingsToUi();
        UpdateSendToStatus();
        await UpdateAuthBadgesAsync();

        if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            SetSelectedFile(_selectedFilePath);
            ShowSendView();
            await LoadContactsAsync();
        }
        else
        {
            // If accounts aren't configured yet, take user straight to settings
            bool tgConfigured = _telegramService.IsConfigured && _telegramService.HasSessionFile;
            bool gdriveConfigured = _driveService.IsConfigured;

            if (!tgConfigured || !gdriveConfigured)
            {
                ShowSettingsView();
            }
            else
            {
                ShowSendView();
                await LoadContactsAsync();
            }
        }
    }

    private void OnLanguageChanged()
    {
        if (SendView.Visibility == Visibility.Visible)
        {
            ToggleSettingsText.Text = Loc.Get("Common_Settings");
        }
        else
        {
            ToggleSettingsText.Text = Loc.Get("Common_BackToSend");
        }

        if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            var fi = new FileInfo(_selectedFilePath);
            FileSizeText.Text = Loc.Format("Send_FileSize_Format", FormatBytes(fi.Length));
            ChangeFileBtnText.Text = Loc.Get("Common_Change");
        }
        else
        {
            ChangeFileBtnText.Text = Loc.Get("Common_Choose");
        }

        UpdateSendToStatus();
        _ = UpdateAuthBadgesAsync();

        UpdateLanguageVisibility();
        LanguageComboBox.Items.Refresh();
    }

    private void UpdateLanguageVisibility()
    {
        LanguageRow.Visibility = LocalizationService.Instance.HasMultipleLanguages
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedValue is string lang && lang != LocalizationService.Instance.ConfiguredLanguage)
        {
            LocalizationService.Instance.SetLanguage(lang);
            _settingsService.Settings.Language = lang;
            _settingsService.SaveSettings();
        }
    }

    #region View Switching & Settings Loading

    private void ShowSendView()
    {
        _viewTransition?.Show(SendView);
        ToggleSettingsIcon.Data = AppIcons.SettingsOutlined;
        ToggleSettingsText.Text = Loc.Get("Common_Settings");
    }

    private void ShowSettingsView()
    {
        _viewTransition?.Show(SettingsView);
        ToggleSettingsIcon.Data = AppIcons.ArrowBackOutlined;
        ToggleSettingsText.Text = Loc.Get("Common_BackToSend");
    }

    private void ToggleSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isUploading)
        {
            MessageBox.Show(Loc.Get("Msg_UploadInProgress_Body"), Loc.Get("Msg_UploadInProgress_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SendView.Visibility == Visibility.Visible)
        {
            ShowSettingsView();
        }
        else
        {
            ShowSendView();
            if (_telegramService.IsConfigured && _telegramService.HasSessionFile && _allChats.Count == 0)
            {
                _ = LoadContactsAsync();
            }
        }
    }

    private void LoadSettingsToUi()
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

    private void SaveUiToSettings()
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

    private async Task UpdateAuthBadgesAsync()
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
                TgStatusBadge.Text = Loc.Get("Status_NotConnected");
                TgStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
                TgStatusDot.Fill = (Brush)FindResource("SettingsMuted");
                TgLoginBtn.Visibility = Visibility.Visible;
                TgLogoutBtn.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            TgStatusBadge.Text = Loc.Get("Status_NotConnected");
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
                GoogleStatusBadge.Text = !string.IsNullOrWhiteSpace(email) ? email : Loc.Get("Status_Authorized");
                GoogleStatusBadge.Foreground = (Brush)FindResource("SettingsAccent");
                GoogleStatusDot.Fill = (Brush)FindResource("SettingsAccent");
                GoogleAuthBtn.Visibility = Visibility.Collapsed;
                GoogleResetBtn.Visibility = Visibility.Visible;
            }
            else
            {
                GoogleStatusBadge.Text = _driveService.IsConfigured ? Loc.Get("Status_LoginRequired") : Loc.Get("Status_NotConfigured");
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
            GoogleStatusBadge.Text = Loc.Get("Status_NotConfigured");
            GoogleStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
            GoogleStatusDot.Fill = (Brush)FindResource("SettingsMuted");
            GoogleAuthBtn.Visibility = Visibility.Visible;
            GoogleResetBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSendToStatus()
    {
        bool installed = ShellIntegrationService.IsShortcutInstalled();
        if (installed)
        {
            SendToStatusBadge.Text = Loc.Get("Status_Added");
            SendToStatusBadge.Foreground = (Brush)FindResource("SettingsAccent");
            SendToStatusDot.Fill = (Brush)FindResource("SettingsAccent");
            AddSendToBtn.Visibility = Visibility.Collapsed;
            RemoveSendToBtn.Visibility = Visibility.Visible;
        }
        else
        {
            SendToStatusBadge.Text = Loc.Get("Status_NotAdded");
            SendToStatusBadge.Foreground = (Brush)FindResource("SettingsMuted");
            SendToStatusDot.Fill = (Brush)FindResource("SettingsMuted");
            AddSendToBtn.Visibility = Visibility.Visible;
            RemoveSendToBtn.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region File Handling

    private void SetSelectedFile(string path)
    {
        _selectedFilePath = path;
        var fi = new FileInfo(path);
        FileNameText.Text = fi.Name;
        FileSizeText.Text = Loc.Format("Send_FileSize_Format", FormatBytes(fi.Length));
        ChangeFileBtnText.Text = Loc.Get("Common_Change");
    }

    private void ChangeFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Dialog_SelectFile_Title"),
            Filter = Loc.Get("Dialog_SelectFile_Filter")
        };

        if (dialog.ShowDialog() == true)
        {
            SetSelectedFile(dialog.FileName);
            UpdateSendButtonState();
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
                SetSelectedFile(files[0]);
                ShowSendView();
                UpdateSendButtonState();
            }
        }
    }

    #endregion

    #region Contacts & Telegram

    private StateCardTransitions.ExitHandle? _loadingExit;
    private bool _isLoadingContacts;

    private void ShowContactsLoading(string? status = null)
    {
        if (_isLoadingContacts) return;
        _isLoadingContacts = true;

        _loadingExit?.Dispose();
        _loadingExit = null;

        SearchBox.IsEnabled = false;
        ContactsLoadingStatus.Text = status ?? Loc.Get("Send_LoadingContacts");
        ContactsLoadingOverlay.Visibility = Visibility.Visible;

        bool animated = UiAnimationPolicy.Enabled;
        if (animated)
        {
            var backdropAnim = StateCardTransitions.CreateDoubleAnimation(
                0, 1.0, StateCardTransitions.EntranceDuration, EasingMode.EaseOut);
            ContactsLoadingBackdrop.BeginAnimation(UIElement.OpacityProperty, backdropAnim);
        }
        else
        {
            ContactsLoadingBackdrop.Opacity = 1.0;
        }

        ContactsLoadingSpinner.Start(animated);
        StateCardTransitions.BeginEntrance(ContactsLoadingCenterPanel, animated);
    }

    private async Task HideContactsLoadingAsync()
    {
        if (!_isLoadingContacts) return;
        _isLoadingContacts = false;

        bool animated = UiAnimationPolicy.Enabled;
        if (!animated)
        {
            ContactsLoadingBackdrop.BeginAnimation(UIElement.OpacityProperty, null);
            ContactsLoadingBackdrop.Opacity = 0;
            ContactsLoadingSpinner.Stop();
            ContactsLoadingOverlay.Visibility = Visibility.Collapsed;
            SearchBox.IsEnabled = true;
            return;
        }

        var backdropExit = StateCardTransitions.CreateDoubleAnimation(
            1.0, 0, StateCardTransitions.ExitDuration, EasingMode.EaseIn);
        ContactsLoadingBackdrop.BeginAnimation(UIElement.OpacityProperty, backdropExit);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _loadingExit = StateCardTransitions.BeginExit(ContactsLoadingCenterPanel, animated, () => tcs.TrySetResult());

        await Task.WhenAny(tcs.Task, Task.Delay(StateCardTransitions.ExitDuration + TimeSpan.FromMilliseconds(100)));

        _loadingExit?.Dispose();
        _loadingExit = null;
        ContactsLoadingSpinner.Stop();
        ContactsLoadingOverlay.Visibility = Visibility.Collapsed;
        SearchBox.IsEnabled = true;
    }

    private async Task LoadContactsAsync()
    {
        if (!_telegramService.IsConfigured || !_telegramService.HasSessionFile)
        {
            ContactsStatusText.Text = Loc.Get("Send_TgNotConnectedHint");
            ContactsStatusText.Visibility = Visibility.Visible;
            ContactsListBox.Visibility = Visibility.Collapsed;
            _allChats.Clear();
            _filteredChats.Clear();
            return;
        }

        ShowContactsLoading(Loc.Get("Send_LoadingContacts"));
        var minTimeTask = Task.Delay(400);

        try
        {
            var chatsTask = _telegramService.GetChatsAndContactsAsync();
            await Task.WhenAll(chatsTask, minTimeTask);
            var chats = await chatsTask;

            _allChats.Clear();
            _allChats.AddRange(chats);

            ApplyFilter(SearchBox.Text);

            if (_filteredChats.Count == 0)
            {
                ContactsStatusText.Text = Loc.Get("Send_NoContactsFound");
                ContactsStatusText.Visibility = Visibility.Visible;
                ContactsListBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                ContactsStatusText.Visibility = Visibility.Collapsed;
                ContactsListBox.Visibility = Visibility.Visible;
                ContactsListBox.SelectedIndex = 0;
            }

            _ = Dispatcher.InvokeAsync(HookContactsScroll, System.Windows.Threading.DispatcherPriority.Loaded);

            // Give WPF a moment to render list items before starting the fade-out
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            ContactsStatusText.Text = Loc.Format("Send_ContactsLoadError", ex.Message);
            ContactsStatusText.Visibility = Visibility.Visible;
            ContactsListBox.Visibility = Visibility.Collapsed;
        }
        finally
        {
            await HideContactsLoadingAsync();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        ApplyFilter(SearchBox.Text);
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        UpdateSearchPlaceholder();
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdateSearchPlaceholder();
    }

    private void UpdateSearchPlaceholder()
    {
        SearchPlaceholder.Visibility = (string.IsNullOrEmpty(SearchBox.Text) && !SearchBox.IsKeyboardFocused)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateMessagePlaceholder();
    }

    private void MessageTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        UpdateMessagePlaceholder();
    }

    private void MessageTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdateMessagePlaceholder();
    }

    private void UpdateMessagePlaceholder()
    {
        MessagePlaceholder.Visibility = (string.IsNullOrEmpty(MessageTextBox.Text) && !MessageTextBox.IsKeyboardFocused)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyFilter(string query)
    {
        _filteredChats.Clear();
        var q = query.Trim().ToLowerInvariant();

        foreach (var chat in _allChats)
        {
            if (string.IsNullOrEmpty(q) ||
                chat.Title.ToLowerInvariant().Contains(q) ||
                chat.Username.ToLowerInvariant().Contains(q) ||
                chat.DisplaySubtitle.ToLowerInvariant().Contains(q))
            {
                _filteredChats.Add(chat);
            }
        }
    }

    private void ContactsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSendButtonState();
    }

    #endregion

    #region Send Process

    private void UpdateSendButtonState()
    {
        bool hasFile = !string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath);
        bool hasRecipient = ContactsListBox.SelectedItem is TelegramChatItem;
        SendButton.IsEnabled = !_isUploading && hasFile && hasRecipient;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
        {
            MessageBox.Show(Loc.Get("Msg_NoFileSelected_Body"), Loc.Get("Msg_NoFileSelected_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedRecipient = ContactsListBox.SelectedItem as TelegramChatItem;
        if (selectedRecipient == null)
        {
            MessageBox.Show(Loc.Get("Msg_NoRecipientSelected_Body"), Loc.Get("Msg_NoRecipientSelected_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check Google Drive auth
        if (!_driveService.IsConfigured)
        {
            MessageBox.Show(Loc.Get("Msg_GDriveNotConfigured_Body"), Loc.Get("Msg_GDriveNotConfigured_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            ShowSettingsView();
            return;
        }

        _isUploading = true;
        _uploadCts = new CancellationTokenSource();
        UpdateSendButtonState();
        CloseButton.IsEnabled = false;
        ChangeFileBtn.IsEnabled = false;

        ProgressCard.Visibility = Visibility.Visible;
        UploadProgressBar.Value = 0;
        UploadProgressBar.IsIndeterminate = false;
        ProgressStatusText.Text = Loc.Get("Send_Progress_ConnectingGDrive");
        ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
        ProgressPercentText.Text = "0%";
        ProgressDetailsText.Text = Loc.Get("Send_Progress_Preparing");

        try
        {
            var progress = new Progress<UploadProgressInfo>(info =>
            {
                UploadProgressBar.Value = info.Percent;
                ProgressPercentText.Text = $"{info.Percent}%";

                string sentStr = FormatBytes(info.BytesSent);
                string totalStr = FormatBytes(info.TotalBytes);
                string speedStr = info.BytesPerSecond > 0 ? Loc.FormatSpeed((long)info.BytesPerSecond) : "";

                ProgressDetailsText.Text = Loc.Format("Send_Progress_DetailsFormat", sentStr, totalStr, speedStr);
                ProgressStatusText.Text = Loc.Get("Send_Progress_UploadingGoogleDrive");
                ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
            });

            // 1. Upload to Google Drive and get public link
            string publicLink = await _driveService.UploadAndShareAsync(_selectedFilePath, progress, _uploadCts.Token);

            // 2. Send via Telegram
            ProgressStatusText.Text = Loc.Get("Send_Progress_SendingTelegram");
            ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
            UploadProgressBar.IsIndeterminate = true;

            string fileName = Path.GetFileName(_selectedFilePath);
            string userComment = MessageTextBox.Text.Trim();

            var (messageToSend, entities) = TelegramClientService.FormatFileMessage(userComment, fileName, publicLink);
            await _telegramService.SendMessageAsync(selectedRecipient.Peer, messageToSend, entities);

            // 3. Done!
            UploadProgressBar.IsIndeterminate = false;
            UploadProgressBar.Value = 100;
            ProgressPercentText.Text = "100%";
            ProgressStatusText.Text = Loc.Get("Send_Progress_Success");
            ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
            ProgressDetailsText.Text = Loc.Format("Send_Progress_SentTo", selectedRecipient.Title);

            if (_settingsService.Settings.AutoCloseOnSuccess)
            {
                await Task.Delay(1800);
                Close();
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = Loc.Get("Send_Progress_Canceled");
            ProgressStatusText.Foreground = (Brush)FindResource("SettingsWarning");
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = Loc.Get("Send_Progress_Error");
            ProgressStatusText.Foreground = (Brush)FindResource("SettingsError");
            ProgressDetailsText.Text = ex.Message;
            MessageBox.Show(Loc.Format("Msg_ErrorOccurred_Body", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isUploading = false;
            UpdateSendButtonState();
            CloseButton.IsEnabled = true;
            ChangeFileBtn.IsEnabled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUploading)
        {
            var result = MessageBox.Show(Loc.Get("Msg_ConfirmAbort_Body"), Loc.Get("Msg_ConfirmAbort_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _uploadCts?.Cancel();
                Close();
            }
        }
        else
        {
            Close();
        }
    }

    #endregion

    #region Settings Actions

    private async void TgLoginBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();

        if (_settingsService.Settings.TelegramApiId <= 0 || string.IsNullOrWhiteSpace(_settingsService.Settings.TelegramApiHash))
        {
            MessageBox.Show(Loc.Get("Msg_FillTgApiCredentials_Body"), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            TgLoginBtn.IsEnabled = false;
            TgStatusBadge.Text = Loc.Get("Status_LoggingIn");
            TgStatusBadge.Foreground = (Brush)FindResource("SettingsWarning");
            TgStatusDot.Fill = (Brush)FindResource("SettingsWarning");

            var user = await _telegramService.LoginAsync(promptType =>
            {
                return Dispatcher.Invoke(() =>
                {
                    if (promptType == "verification_code")
                    {
                        var dlg = new PromptDialog(Loc.Get("Prompt_VerificationCode_Title"), Loc.Get("Prompt_VerificationCode_Body"))
                        {
                            Owner = this
                        };
                        return dlg.ShowDialog() == true ? dlg.ResponseText : null;
                    }
                    if (promptType == "password")
                    {
                        var dlg = new PromptDialog(Loc.Get("Prompt_2FA_Title"), Loc.Get("Prompt_2FA_Body"), isPassword: true)
                        {
                            Owner = this
                        };
                        return dlg.ShowDialog() == true ? dlg.ResponseText : null;
                    }
                    if (promptType == "phone_number")
                    {
                        var dlg = new PromptDialog(Loc.Get("Prompt_Phone_Title"), Loc.Get("Prompt_Phone_Body"))
                        {
                            Owner = this
                        };
                        return dlg.ShowDialog() == true ? dlg.ResponseText : null;
                    }
                    return null;
                });
            });

            if (user != null)
            {
                await UpdateAuthBadgesAsync();
                MessageBox.Show(Loc.Format("Msg_TgAuthSuccess_Body", user.first_name, user.last_name), Loc.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadContactsAsync();
            }
            else
            {
                await UpdateAuthBadgesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.Format("Msg_TgAuthError_Body", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            await UpdateAuthBadgesAsync();
        }
        finally
        {
            TgLoginBtn.IsEnabled = true;
        }
    }

    private void TgLogoutBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(Loc.Get("Msg_ConfirmTgLogout_Body"), Loc.Get("Msg_ConfirmTgLogout_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _telegramService.Logout();
            _ = UpdateAuthBadgesAsync();
            _allChats.Clear();
            _filteredChats.Clear();
            MessageBox.Show(Loc.Get("Msg_TgSessionDeleted_Body"), Loc.Get("Msg_TgSessionDeleted_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void LoadSecretJsonBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Dialog_SelectJson_Title"),
            Filter = Loc.Get("Dialog_SelectJson_Filter")
        };

        if (dialog.ShowDialog() == true)
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
                    MessageBox.Show(Loc.Get("Msg_GoogleKeysLoaded_Body"), Loc.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Loc.Get("Msg_GoogleKeysFormatWarning_Body"), Loc.Get("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Format("Msg_ReadJsonError_Body", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void GoogleAuthBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();

        if (string.IsNullOrWhiteSpace(_settingsService.Settings.GoogleClientId) ||
            string.IsNullOrWhiteSpace(_settingsService.Settings.GoogleClientSecret))
        {
            MessageBox.Show(Loc.Get("Msg_FillGoogleCredentials_Body"), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            GoogleAuthBtn.IsEnabled = false;
            GoogleStatusBadge.Text = Loc.Get("Status_BrowserLogin");
            GoogleStatusBadge.Foreground = (Brush)FindResource("SettingsWarning");
            GoogleStatusDot.Fill = (Brush)FindResource("SettingsWarning");

            bool success = await _driveService.AuthorizeAsync();
            if (success)
            {
                await UpdateAuthBadgesAsync();
                MessageBox.Show(Loc.Get("Msg_GoogleAuthSuccess_Body"), Loc.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await UpdateAuthBadgesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.Format("Msg_GoogleAuthError_Body", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            await UpdateAuthBadgesAsync();
        }
        finally
        {
            GoogleAuthBtn.IsEnabled = true;
        }
    }

    private void GoogleResetBtn_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(Loc.Get("Msg_ConfirmGoogleReset_Body"), Loc.Get("Msg_ConfirmGoogleReset_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _driveService.ResetAuthorization();
            _ = UpdateAuthBadgesAsync();
            MessageBox.Show(Loc.Get("Msg_GoogleResetDone_Body"), Loc.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AddSendToBtn_Click(object sender, RoutedEventArgs e)
    {
        bool ok = ShellIntegrationService.InstallShortcut();
        if (ok)
        {
            UpdateSendToStatus();
            MessageBox.Show(Loc.Get("Msg_SendToAdded_Body"), Loc.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(Loc.Get("Msg_SendToCreateError_Body"), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveSendToBtn_Click(object sender, RoutedEventArgs e)
    {
        bool ok = ShellIntegrationService.UninstallShortcut();
        if (ok)
        {
            UpdateSendToStatus();
            MessageBox.Show(Loc.Get("Msg_SendToRemoved_Body"), Loc.Get("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void CreateBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUiToSettings();

            var dialog = new SaveFileDialog
            {
                Title = Loc.Get("Settings_Backup_CreateBtn"),
                Filter = BackupService.DialogFilter,
                DefaultExt = BackupService.BackupFileExtension,
                FileName = $"shareman_Backup_{DateTime.Now:yyyy-MM-dd_HHmm}.gdtbak"
            };

            if (dialog.ShowDialog(this) == true)
            {
                // Temporarily release Telegram client session locks and flush pending writes
                _telegramService.ResetClient();

                BackupService.CreateBackup(dialog.FileName);

                await UpdateAuthBadgesAsync();

                MessageBox.Show(
                    Loc.Format("Msg_BackupCreated_Body", dialog.FileName),
                    Loc.Get("Msg_BackupCreated_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Loc.Format("Msg_BackupCreateError_Body", ex.Message),
                Loc.Get("Common_Error"),
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
                Title = Loc.Get("Settings_Backup_RestoreBtn"),
                Filter = BackupService.DialogFilter,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var confirm = MessageBox.Show(
                Loc.Get("Msg_RestoreConfirm_Body"),
                Loc.Get("Msg_RestoreConfirm_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _telegramService.ResetClient();
            _driveService.ResetAuthorization();

            BackupService.RestoreBackup(dialog.FileName);

            _settingsService.LoadSettings();
            LoadSettingsToUi();
            await UpdateAuthBadgesAsync();

            _allChats.Clear();
            _filteredChats.Clear();
            _ = LoadContactsAsync();

            MessageBox.Show(
                Loc.Get("Msg_RestoreSuccess_Body"),
                Loc.Get("Msg_RestoreSuccess_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Loc.Format("Msg_RestoreError_Body", ex.Message),
                Loc.Get("Common_Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToSettings();
        _telegramService.ResetClient();
        await UpdateAuthBadgesAsync();
        ShowSendView();
        await LoadContactsAsync();
    }

    #endregion

    #region Helpers

    private static string FormatBytes(long bytes)
    {
        return LocalizationService.Instance.FormatBytes(bytes);
    }

    private void HookContactsScroll()
    {
        ContactsListBox.ApplyTemplate();
        var scroll = ContactsListBox.Template?.FindName("PART_ScrollViewer", ContactsListBox) as ScrollViewer
                     ?? FindVisualChild<ScrollViewer>(ContactsListBox);

        if (scroll != null && _contactsScrollController == null)
        {
            _contactsScrollController = new SettingsScrollController(scroll);
        }

        if (scroll != null && _contactsScrollMotion == null)
        {
            var presenter = ContactsListBox.Template?.FindName("ContactsItemsPresenter", ContactsListBox) as ItemsPresenter
                            ?? FindVisualChild<ItemsPresenter>(scroll);

            if (presenter != null)
            {
                if (presenter.RenderTransform is not TranslateTransform translation || translation.IsFrozen)
                {
                    translation = new TranslateTransform();
                    presenter.RenderTransform = translation;
                }
                _contactsScrollMotion = new SettingsScrollMotionController(scroll, translation);
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    #endregion
}