using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GDriveTelegramSender.Models;
using GDriveTelegramSender.Services;
using GDriveTelegramSender.Ui;
using Microsoft.Win32;

namespace GDriveTelegramSender.Views;

public partial class SendViewControl : UserControl, IDisposable
{
    private readonly SendWorkflowCoordinator _sendWorkflow;
    private readonly TelegramClientService _telegramService;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;

    private string? _selectedFilePath;
    private readonly List<TelegramChatItem> _allChats = new();
    private readonly ObservableCollection<TelegramChatItem> _filteredChats = new();

    private CancellationTokenSource? _uploadCts;
    private bool _isUploading;
    private StateCardTransitions.ExitHandle? _loadingExit;
    private bool _isLoadingContacts;

    private SettingsScrollController? _contactsScrollController;
    private SettingsScrollMotionController? _contactsScrollMotion;

    public bool IsUploading => _isUploading;

    public event Action? RequestOpenSettings;
    public event Action? RequestClose;

    public SendViewControl(
        SendWorkflowCoordinator sendWorkflow,
        TelegramClientService telegramService,
        SettingsService settingsService,
        LocalizationService localizationService)
    {
        InitializeComponent();

        _sendWorkflow = sendWorkflow ?? throw new ArgumentNullException(nameof(sendWorkflow));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        ContactsListBox.ItemsSource = _filteredChats;

        HookContactsScroll();
        ContactsListBox.Loaded += (_, _) => HookContactsScroll();

        _localizationService.LanguageChanged += OnLanguageChanged;

        Loaded += (_, _) => UpdateSendButtonState();
    }

    public void SetSelectedFile(string path)
    {
        _selectedFilePath = path;
        var fi = new FileInfo(path);
        FileNameText.Text = fi.Name;
        FileSizeText.Text = _localizationService.Format("Send_FileSize_Format", _localizationService.FormatBytes(fi.Length));
        ChangeFileBtnText.Text = _localizationService.Get("Common_Change");
        UpdateSendButtonState();
    }

    public void OnLanguageChanged()
    {
        if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            var fi = new FileInfo(_selectedFilePath);
            FileSizeText.Text = _localizationService.Format("Send_FileSize_Format", _localizationService.FormatBytes(fi.Length));
            ChangeFileBtnText.Text = _localizationService.Get("Common_Change");
        }
        else
        {
            ChangeFileBtnText.Text = _localizationService.Get("Common_Choose");
        }
    }

    public async Task LoadContactsAsync()
    {
        if (!_telegramService.IsConfigured || !_telegramService.HasSessionFile)
        {
            ContactsStatusText.Text = _localizationService.Get("Send_TgNotConnectedHint");
            ContactsStatusText.Visibility = Visibility.Visible;
            ContactsListBox.Visibility = Visibility.Collapsed;
            _allChats.Clear();
            _filteredChats.Clear();
            return;
        }

        ShowContactsLoading(_localizationService.Get("Send_LoadingContacts"));
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
                ContactsStatusText.Text = _localizationService.Get("Send_NoContactsFound");
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
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            ContactsStatusText.Text = _localizationService.Format("Send_ContactsLoadError", ex.Message);
            ContactsStatusText.Visibility = Visibility.Visible;
            ContactsListBox.Visibility = Visibility.Collapsed;
        }
        finally
        {
            await HideContactsLoadingAsync();
        }
    }

    private void ShowContactsLoading(string? status = null)
    {
        if (_isLoadingContacts) return;
        _isLoadingContacts = true;

        _loadingExit?.Dispose();
        _loadingExit = null;

        SearchBox.IsEnabled = false;
        ContactsLoadingStatus.Text = status ?? _localizationService.Get("Send_LoadingContacts");
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

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        ApplyFilter(SearchBox.Text);
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e) => UpdateSearchPlaceholder();
    private void SearchBox_LostFocus(object sender, RoutedEventArgs e) => UpdateSearchPlaceholder();

    private void UpdateSearchPlaceholder()
    {
        SearchPlaceholder.Visibility = (string.IsNullOrEmpty(SearchBox.Text) && !SearchBox.IsKeyboardFocused)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMessagePlaceholder();
    private void MessageTextBox_GotFocus(object sender, RoutedEventArgs e) => UpdateMessagePlaceholder();
    private void MessageTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdateMessagePlaceholder();

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

    private void UpdateSendButtonState()
    {
        bool hasFile = !string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath);
        bool hasRecipient = ContactsListBox.SelectedItem is TelegramChatItem;
        SendButton.IsEnabled = !_isUploading && hasFile && hasRecipient;
    }

    private void ChangeFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = _localizationService.Get("Dialog_SelectFile_Title"),
            Filter = _localizationService.Get("Dialog_SelectFile_Filter")
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            SetSelectedFile(dialog.FileName);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
        {
            MessageBox.Show(_localizationService.Get("Msg_NoFileSelected_Body"), _localizationService.Get("Msg_NoFileSelected_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedRecipient = ContactsListBox.SelectedItem as TelegramChatItem;
        if (selectedRecipient == null)
        {
            MessageBox.Show(_localizationService.Get("Msg_NoRecipientSelected_Body"), _localizationService.Get("Msg_NoRecipientSelected_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_sendWorkflow.IsDriveConfigured)
        {
            MessageBox.Show(_localizationService.Get("Msg_GDriveNotConfigured_Body"), _localizationService.Get("Msg_GDriveNotConfigured_Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            RequestOpenSettings?.Invoke();
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
        ProgressStatusText.Text = _localizationService.Get("Send_Progress_ConnectingGDrive");
        ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
        ProgressPercentText.Text = "0%";
        ProgressDetailsText.Text = _localizationService.Get("Send_Progress_Preparing");

        try
        {
            var progress = new Progress<SendWorkflowProgress>(p =>
            {
                switch (p.Stage)
                {
                    case SendWorkflowStage.Preparing:
                        ProgressStatusText.Text = _localizationService.Get("Send_Progress_ConnectingGDrive");
                        ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
                        UploadProgressBar.IsIndeterminate = false;
                        UploadProgressBar.Value = 0;
                        ProgressPercentText.Text = "0%";
                        ProgressDetailsText.Text = _localizationService.Get("Send_Progress_Preparing");
                        break;

                    case SendWorkflowStage.UploadingToDrive:
                        if (p.UploadInfo != null)
                        {
                            UploadProgressBar.IsIndeterminate = false;
                            UploadProgressBar.Value = p.UploadInfo.Percent;
                            ProgressPercentText.Text = $"{p.UploadInfo.Percent}%";

                            string sentStr = _localizationService.FormatBytes(p.UploadInfo.BytesSent);
                            string totalStr = _localizationService.FormatBytes(p.UploadInfo.TotalBytes);
                            string speedStr = p.UploadInfo.BytesPerSecond > 0 ? _localizationService.FormatSpeed((long)p.UploadInfo.BytesPerSecond) : "";

                            ProgressDetailsText.Text = _localizationService.Format("Send_Progress_DetailsFormat", sentStr, totalStr, speedStr);
                        }
                        ProgressStatusText.Text = _localizationService.Get("Send_Progress_UploadingGoogleDrive");
                        ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
                        break;

                    case SendWorkflowStage.SendingTelegram:
                        ProgressStatusText.Text = _localizationService.Get("Send_Progress_SendingTelegram");
                        ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
                        UploadProgressBar.IsIndeterminate = true;
                        break;

                    case SendWorkflowStage.Completed:
                        UploadProgressBar.IsIndeterminate = false;
                        UploadProgressBar.Value = 100;
                        ProgressPercentText.Text = "100%";
                        ProgressStatusText.Text = _localizationService.Get("Send_Progress_Success");
                        ProgressStatusText.Foreground = (Brush)FindResource("SettingsAccent");
                        ProgressDetailsText.Text = _localizationService.Format("Send_Progress_SentTo", selectedRecipient.Title);
                        break;
                }
            });

            string comment = MessageTextBox.Text;
            await _sendWorkflow.ExecuteAsync(_selectedFilePath, selectedRecipient, comment, progress, _uploadCts.Token);

            if (_settingsService.Settings.AutoCloseOnSuccess)
            {
                await Task.Delay(1800);
                RequestClose?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = _localizationService.Get("Send_Progress_Canceled");
            ProgressStatusText.Foreground = (Brush)FindResource("SettingsWarning");
        }
        catch (Exception ex)
        {
            ProgressStatusText.Text = _localizationService.Get("Send_Progress_Error");
            ProgressStatusText.Foreground = (Brush)FindResource("SettingsError");
            ProgressDetailsText.Text = ex.Message;
            MessageBox.Show(_localizationService.Format("Msg_ErrorOccurred_Body", ex.Message), _localizationService.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            var result = MessageBox.Show(
                _localizationService.Get("Msg_ConfirmAbort_Body"),
                _localizationService.Get("Msg_ConfirmAbort_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _uploadCts?.Cancel();
                RequestClose?.Invoke();
            }
        }
        else
        {
            RequestClose?.Invoke();
        }
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

    public void Dispose()
    {
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _contactsScrollController?.Dispose();
        _contactsScrollController = null;
        _contactsScrollMotion?.Dispose();
        _contactsScrollMotion = null;
        _loadingExit?.Dispose();
        _loadingExit = null;
    }
}
