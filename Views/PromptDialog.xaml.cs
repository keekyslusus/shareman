using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GDriveTelegramSender.Ui;

namespace GDriveTelegramSender.Views;

public partial class PromptDialog : Window
{
    public string? ResponseText { get; private set; }
    private readonly bool _isPassword;

    public PromptDialog(string title, string message, bool isPassword = false, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        _isPassword = isPassword;

        ThemeManager.RegisterWindow(this);

        if (isPassword)
        {
            InputTextBox.Visibility = Visibility.Collapsed;
            InputPasswordBox.Visibility = Visibility.Visible;
            Loaded += (s, e) => InputPasswordBox.Focus();
        }
        else
        {
            InputTextBox.Visibility = Visibility.Visible;
            InputPasswordBox.Visibility = Visibility.Collapsed;
            InputTextBox.Text = defaultValue;
            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        ResponseText = _isPassword ? InputPasswordBox.Password : InputTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OkBtn_Click(sender, e);
        }
    }

    private void InputPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OkBtn_Click(sender, e);
        }
    }
}
