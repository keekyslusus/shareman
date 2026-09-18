using System;

namespace GDriveTelegramSender.Models;

public class AppSettings
{
    public int TelegramApiId { get; set; } = 0;
    public string TelegramApiHash { get; set; } = string.Empty;
    public string TelegramPhoneNumber { get; set; } = string.Empty;

    public string GoogleClientId { get; set; } = string.Empty;
    public string GoogleClientSecret { get; set; } = string.Empty;

    public string DefaultMessage { get; set; } = string.Empty;
    public bool AutoCloseOnSuccess { get; set; } = true;
}
