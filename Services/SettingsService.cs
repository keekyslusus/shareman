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
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        DataDirectory = Path.Combine(appData, "GDriveTelegramSender");
        Directory.CreateDirectory(DataDirectory);

        SettingsFilePath = Path.Combine(DataDirectory, "settings.json");
        TelegramSessionPath = Path.Combine(DataDirectory, "telegram.session");
        GoogleTokensDirectory = Path.Combine(DataDirectory, "google_tokens");
        Directory.CreateDirectory(GoogleTokensDirectory);

        Settings = LoadSettings();
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                    return Settings;
                }
            }
        }
        catch
        {
            // fallback to default
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
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
