using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace GDriveTelegramSender.Services;

public class LanguageOption : INotifyPropertyChanged
{
    public string Code { get; }
    private string _displayName;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName != value)
            {
                _displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
    }

    public LanguageOption(string code, string displayName)
    {
        Code = code;
        _displayName = displayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class LocalizationService : INotifyPropertyChanged
{
    public const string SystemLanguageCode = "system";
    private const string DefaultLanguage = "en";

    private static readonly Lazy<LocalizationService> _lazy = new(() => new LocalizationService());
    public static LocalizationService Instance => _lazy.Value;

    private readonly LanguageOption _systemOption;
    private readonly List<LanguageOption> _supportedLanguages;

    public static IReadOnlyList<LanguageOption> SupportedLanguages => Instance._supportedLanguages;
    public bool HasMultipleLanguages => _translations.Count > 1;

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    private string _configuredLanguage = SystemLanguageCode;
    private string _currentLanguage = DefaultLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    public string ConfiguredLanguage => _configuredLanguage;
    public string CurrentLanguage => _currentLanguage;

    public string this[string key] => Get(key);

    private static readonly Dictionary<string, string> KnownLanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["ru"] = "Русский",
        ["de"] = "Deutsch",
        ["fr"] = "Français",
        ["es"] = "Español",
        ["it"] = "Italiano",
        ["zh"] = "中文",
        ["ja"] = "日本語",
        ["ko"] = "한국어",
        ["pt"] = "Português",
        ["pl"] = "Polski",
        ["tr"] = "Türkçe",
        ["uk"] = "Українська"
    };

    private LocalizationService()
    {
        _systemOption = new LanguageOption(SystemLanguageCode, "System default");
        _supportedLanguages = new List<LanguageOption>();

        LoadEmbeddedTranslations();
        BuildSupportedLanguages();
        UpdateLanguageOptionNames();
    }

    private void BuildSupportedLanguages()
    {
        _supportedLanguages.Clear();
        if (_translations.Count > 1)
        {
            _supportedLanguages.Add(_systemOption);
        }

        foreach (var code in _translations.Keys.OrderBy(c => c))
        {
            string displayName;
            if (KnownLanguageNames.TryGetValue(code, out var knownName))
            {
                displayName = knownName;
            }
            else
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(code);
                    displayName = culture.NativeName;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        displayName = char.ToUpper(displayName[0], culture) + displayName[1..];
                    }
                }
                catch
                {
                    displayName = code.ToUpperInvariant();
                }
            }

            _supportedLanguages.Add(new LanguageOption(code, displayName));
        }

        if (_supportedLanguages.Count == 0)
        {
            _supportedLanguages.Add(new LanguageOption(DefaultLanguage, "English"));
        }
    }

    public void Initialize(string? preferredLanguage)
    {
        string target = string.IsNullOrWhiteSpace(preferredLanguage) ? SystemLanguageCode : preferredLanguage;
        SetLanguage(target);
    }

    public string ResolveEffectiveLanguage(string? langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode) || string.Equals(langCode, SystemLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (_translations.ContainsKey(twoLetter))
            {
                return twoLetter;
            }
            return DefaultLanguage;
        }

        if (_translations.ContainsKey(langCode))
        {
            return langCode;
        }

        return DefaultLanguage;
    }

    public void SetLanguage(string langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode))
        {
            langCode = SystemLanguageCode;
        }

        _configuredLanguage = langCode;
        string effectiveLang = ResolveEffectiveLanguage(langCode);

        if (!_translations.ContainsKey(effectiveLang))
        {
            effectiveLang = DefaultLanguage;
        }

        _currentLanguage = effectiveLang;

        try
        {
            var culture = new CultureInfo(effectiveLang);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch
        {
            // fallback if culture code isn't supported by Windows NLS
        }

        UpdateLanguageOptionNames();

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    private void UpdateLanguageOptionNames()
    {
        if (_systemOption != null)
        {
            _systemOption.DisplayName = Get("Language_System");
        }
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        if (_translations.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return val;
        }

        if (_currentLanguage != DefaultLanguage &&
            _translations.TryGetValue(DefaultLanguage, out var defaultDict) &&
            defaultDict.TryGetValue(key, out var defaultVal))
        {
            return defaultVal;
        }

        return $"[{key}]";
    }

    public string Format(string key, params object[] args)
    {
        string pattern = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    public string FormatBytes(long bytes)
    {
        string unitsStr = Get("Format_ByteUnits");
        string[] suffixes = unitsStr.Split(',', StringSplitOptions.TrimEntries);
        if (suffixes.Length == 0) suffixes = new[] { "B", "KB", "MB", "GB", "TB" };

        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number.ToString("n1", CultureInfo.CurrentUICulture)} {suffixes[counter]}";
    }

    public string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec <= 0) return string.Empty;
        string bytesFormatted = FormatBytes(bytesPerSec);
        return Format("Send_Progress_SpeedFormat", bytesFormatted);
    }

    private void LoadEmbeddedTranslations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            if (resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                resourceName.Contains("Resources.Localization.", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = resourceName;
                int idx = resourceName.IndexOf("Resources.Localization.", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    fileName = resourceName[(idx + "Resources.Localization.".Length)..];
                }
                string langCode = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                        if (dict != null)
                        {
                            _translations[langCode] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load localization resource {resourceName}: {ex.Message}");
                }
            }
        }
    }
}

public static class Loc
{
    public static string Get(string key) => LocalizationService.Instance.Get(key);
    public static string Format(string key, params object[] args) => LocalizationService.Instance.Format(key, args);
    public static string FormatBytes(long bytes) => LocalizationService.Instance.FormatBytes(bytes);
    public static string FormatSpeed(long bytesPerSec) => LocalizationService.Instance.FormatSpeed(bytesPerSec);
}
