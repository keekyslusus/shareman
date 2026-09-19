using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GDriveTelegramSender.Ui;

public partial class MaskedInputField : UserControl
{
    private bool _isRevealed;
    private bool _isSyncing;
    private bool _isFocused;

    public MaskedInputField()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => _isRevealed ? PlainBox.Text : MaskedBox.Password;
        set
        {
            _isSyncing = true;
            PlainBox.Text = value ?? string.Empty;
            MaskedBox.Password = value ?? string.Empty;
            _isSyncing = false;
        }
    }

    public string Password
    {
        get => Text;
        set => Text = value;
    }

    private void ToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        _isRevealed = !_isRevealed;
        if (_isRevealed)
        {
            PlainBox.Text = MaskedBox.Password;
            PlainBox.Visibility = Visibility.Visible;
            MaskedBox.Visibility = Visibility.Collapsed;
            EyeIconPath.Data = AppIcons.EyeOffOutlined;
            PlainBox.Focus();
            PlainBox.CaretIndex = PlainBox.Text.Length;
        }
        else
        {
            MaskedBox.Password = PlainBox.Text;
            MaskedBox.Visibility = Visibility.Visible;
            PlainBox.Visibility = Visibility.Collapsed;
            EyeIconPath.Data = AppIcons.EyeOutlined;
            MaskedBox.Focus();
        }
    }

    private void MaskedBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        PlainBox.Text = MaskedBox.Password;
        _isSyncing = false;
    }

    private void PlainBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        MaskedBox.Password = PlainBox.Text;
        _isSyncing = false;
    }

    private void UpdateBorder()
    {
        if (RootBorder == null) return;

        if (_isFocused)
        {
            if (TryFindResource("SettingsAccent") is Brush b) RootBorder.BorderBrush = b;
        }
        else if (IsMouseOver)
        {
            if (TryFindResource("SettingsAccentLine") is Brush b) RootBorder.BorderBrush = b;
        }
        else
        {
            if (TryFindResource("SettingsLine") is Brush b) RootBorder.BorderBrush = b;
        }
    }

    private void Input_GotFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = true;
        UpdateBorder();
    }

    private void Input_LostFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = false;
        UpdateBorder();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateBorder();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateBorder();
    }
}
