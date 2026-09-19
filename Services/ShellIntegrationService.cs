using System;
using System.IO;

namespace GDriveTelegramSender.Services;

public class ShellIntegrationService
{
    private const string ShortcutFileName = "shareman.lnk";
    private const string LegacyShortcutFileName = "Google Drive & Telegram.lnk";

    public string GetSendToDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
    }

    public string GetShortcutPath()
    {
        return Path.Combine(GetSendToDirectory(), ShortcutFileName);
    }

    private string GetLegacyShortcutPath()
    {
        return Path.Combine(GetSendToDirectory(), LegacyShortcutFileName);
    }

    public bool IsShortcutInstalled()
    {
        string shortcutPath = GetShortcutPath();
        return File.Exists(shortcutPath);
    }

    public bool InstallShortcut()
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

            // Remove legacy shortcut if it exists
            string legacyShortcutPath = GetLegacyShortcutPath();
            if (File.Exists(legacyShortcutPath))
            {
                try { File.Delete(legacyShortcutPath); } catch { }
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
            shortcut.Description = Loc.Get("SendTo_Shortcut_Description");
            shortcut.Save();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to install SendTo shortcut: {ex.Message}");
            return false;
        }
    }

    public bool UninstallShortcut()
    {
        try
        {
            string shortcutPath = GetShortcutPath();
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            string legacyShortcutPath = GetLegacyShortcutPath();
            if (File.Exists(legacyShortcutPath))
            {
                try { File.Delete(legacyShortcutPath); } catch { }
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
