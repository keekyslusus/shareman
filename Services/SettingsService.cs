using System;
using System.IO;
using System.Text.Json;
using GDriveTelegramSender.Models;

namespace GDriveTelegramSender.Services;

public class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    public string DataDirectory { get; }
    public string SettingsFilePath { get; }
    public string TelegramSessionPath { get; }
    public string GoogleTokensDirectory { get; }

    public AppSettings Settings { get; private set; }

    private SettingsService()
    {
        string? appDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(appDir))
        {
            appDir = AppContext.BaseDirectory;
        }

        DataDirectory = Path.Combine(appDir, "UserData");
        Directory.CreateDirectory(DataDirectory);

        SettingsFilePath = Path.Combine(DataDirectory, "settings.json");
        TelegramSessionPath = Path.Combine(DataDirectory, "telegram.session");
        GoogleTokensDirectory = Path.Combine(DataDirectory, "google_tokens");
        Directory.CreateDirectory(GoogleTokensDirectory);

        EnsureUserDataEncrypted();

        Settings = LoadSettings();
    }

    public void EnsureUserDataEncrypted()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                byte[] raw = File.ReadAllBytes(SettingsFilePath);
                if (!DataEncryptionService.IsEncrypted(raw))
                {
                    byte[] encrypted = DataEncryptionService.Encrypt(raw);
                    File.WriteAllBytes(SettingsFilePath, encrypted);
                }
            }

            if (Directory.Exists(GoogleTokensDirectory))
            {
                foreach (string file in Directory.GetFiles(GoogleTokensDirectory))
                {
                    byte[] raw = File.ReadAllBytes(file);
                    if (!DataEncryptionService.IsEncrypted(raw))
                    {
                        byte[] encrypted = DataEncryptionService.Encrypt(raw);
                        File.WriteAllBytes(file, encrypted);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to ensure encryption: {ex.Message}");
        }
    }


    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                byte[] raw = File.ReadAllBytes(SettingsFilePath);
                string json;
                if (DataEncryptionService.IsEncrypted(raw))
                {
                    byte[] decrypted = DataEncryptionService.Decrypt(raw);
                    json = System.Text.Encoding.UTF8.GetString(decrypted);
                }
                else
                {
                    json = System.Text.Encoding.UTF8.GetString(raw);
                    // Automatically encrypt on disk
                    try
                    {
                        byte[] encrypted = DataEncryptionService.Encrypt(raw);
                        File.WriteAllBytes(SettingsFilePath, encrypted);
                    }
                    catch { }
                }

                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                    return Settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        Settings = new AppSettings();
        return Settings;
    }

    public void SaveSettings()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Settings, options);
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] encrypted = DataEncryptionService.Encrypt(raw);
            File.WriteAllBytes(SettingsFilePath, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
