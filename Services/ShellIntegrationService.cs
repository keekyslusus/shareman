using System;
using System.IO;

namespace GDriveTelegramSender.Services;

public static class ShellIntegrationService
{
    private const string ShortcutFileName = "Google Drive & Telegram.lnk";

    public static string GetSendToDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
    }

    public static string GetShortcutPath()
    {
        return Path.Combine(GetSendToDirectory(), ShortcutFileName);
    }

    public static bool IsShortcutInstalled()
    {
        string shortcutPath = GetShortcutPath();
        return File.Exists(shortcutPath);
    }

    public static bool InstallShortcut()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            string sendToDir = GetSendToDirectory();
            if (!Directory.Exists(sendToDir))
            {
                Directory.CreateDirectory(sendToDir);
            }

            string shortcutPath = GetShortcutPath();

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return false;
            }

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null)
            {
                return false;
            }

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = "Загрузить видео на Google Drive и отправить ссылку в Telegram";
            shortcut.Save();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to install SendTo shortcut: {ex.Message}");
            return false;
        }
    }

    public static bool UninstallShortcut()
    {
        try
        {
            string shortcutPath = GetShortcutPath();
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to remove SendTo shortcut: {ex.Message}");
            return false;
        }
    }
}
