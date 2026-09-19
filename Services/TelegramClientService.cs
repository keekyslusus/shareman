using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GDriveTelegramSender.Models;
using TL;
using WTelegram;

namespace GDriveTelegramSender.Services;

public class TelegramClientService : IDisposable
{
    private readonly SettingsService _settingsService = SettingsService.Instance;
    private Client? _client;
    private Func<string, string?>? _promptCallback;

    private static readonly string[] AvatarPalette = new[]
    {
        "#2563EB", "#059669", "#D97706", "#7C3AED", "#DB2777", "#0891B2", "#4F46E5", "#0D9488"
    };

    public bool IsConfigured =>
        _settingsService.Settings.TelegramApiId > 0 &&
        !string.IsNullOrWhiteSpace(_settingsService.Settings.TelegramApiHash);

    public bool HasSessionFile => File.Exists(_settingsService.TelegramSessionPath);

    public void ResetClient()
    {
        _client?.Dispose();
        _client = null;
    }

    private string? ConfigProvider(string what)
    {
        return what switch
        {
            "api_id" => _settingsService.Settings.TelegramApiId > 0
                ? _settingsService.Settings.TelegramApiId.ToString()
                : null,
            "api_hash" => _settingsService.Settings.TelegramApiHash,
            "phone_number" => !string.IsNullOrWhiteSpace(_settingsService.Settings.TelegramPhoneNumber)
                ? _settingsService.Settings.TelegramPhoneNumber
                : _promptCallback?.Invoke("phone_number"),
            "session_pathname" => _settingsService.TelegramSessionPath,
            "verification_code" => _promptCallback?.Invoke("verification_code"),
            "password" => _promptCallback?.Invoke("password"),
            _ => null
        };
    }

    private Client GetOrCreateClient()
    {
        if (_client != null) return _client;
        _client = new Client(ConfigProvider);
        return _client;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (!IsConfigured || !HasSessionFile) return null;

        try
        {
            var client = GetOrCreateClient();
            var user = await client.LoginUserIfNeeded();
            return user;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting current Telegram user: {ex.Message}");
            return null;
        }
    }

    public async Task<User?> LoginAsync(Func<string, string?> promptCallback)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(Loc.Get("Exception_TgNotConfigured"));
        }

        _promptCallback = promptCallback;
        ResetClient();

        try
        {
            var client = GetOrCreateClient();
            var user = await client.LoginUserIfNeeded();
            return user;
        }
        finally
        {
            _promptCallback = null;
        }
    }

    public async Task<List<TelegramChatItem>> GetChatsAndContactsAsync()
    {
        var client = GetOrCreateClient();
        await client.LoginUserIfNeeded();

        var result = new List<TelegramChatItem>();
        var seenIds = new HashSet<long>();

        // 1. Saved Messages (Self)
        result.Add(new TelegramChatItem
        {
            Id = client.UserId,
            Title = Loc.Get("Chat_SavedMessages_Title"),
            DisplaySubtitle = Loc.Get("Chat_SavedMessages_Subtitle"),
            Username = client.User?.MainUsername != null ? $"@{client.User.MainUsername}" : "",
            Initials = "★",
            AvatarBgColor = "#2563EB",
            Peer = InputPeer.Self,
            IsUser = true
        });
        seenIds.Add(client.UserId);

        // 2. Recent Dialogs
        try
        {
            var dialogs = await client.Messages_GetAllDialogs();
            foreach (var dialog in dialogs.dialogs)
            {
                if (result.Count >= 30) break;

                long peerId = dialog.Peer.ID;
                if (seenIds.Contains(peerId)) continue;

                if (dialogs.users.TryGetValue(peerId, out var user))
                {
                    if (user.IsActive)
                    {
                        string name = FormatUserName(user);
                        string username = !string.IsNullOrWhiteSpace(user.MainUsername) ? $"@{user.MainUsername}" : "";
                        string initials = GetInitials(name);
                        string color = GetColorForId(user.id);

                        result.Add(new TelegramChatItem
                        {
                            Id = user.id,
                            Title = name,
                            Username = username,
                            DisplaySubtitle = !string.IsNullOrEmpty(username) ? username : (user.phone ?? Loc.Get("Chat_Contact_Default")),
                            Initials = initials,
                            AvatarBgColor = color,
                            Peer = user,
                            IsUser = true
                        });
                        seenIds.Add(peerId);
                    }
                }
                else if (dialogs.chats.TryGetValue(peerId, out var chat))
                {
                    // Исключаем каналы из списка
                    if (chat is Channel ch && ch.IsChannel)
                    {
                        continue;
                    }

                    if (chat.IsActive)
                    {
                        string title = chat.Title ?? Loc.Get("Chat_Group_Default");
                        string initials = GetInitials(title);
                        string color = GetColorForId(chat.ID);

                        result.Add(new TelegramChatItem
                        {
                            Id = chat.ID,
                            Title = title,
                            Username = "",
                            DisplaySubtitle = Loc.Get("Chat_Group_Default"),
                            Initials = initials,
                            AvatarBgColor = color,
                            Peer = chat,
                            IsUser = false
                        });
                        seenIds.Add(peerId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load dialogs: {ex.Message}");
        }

        // 3. Contacts (only if needed to reach 30)
        if (result.Count < 30)
        {
            try
            {
                var contacts = await client.Contacts_GetContacts();
                foreach (var (id, user) in contacts.users)
                {
                    if (result.Count >= 30) break;
                    if (seenIds.Contains(id) || !user.IsActive) continue;

                    string name = FormatUserName(user);
                    string username = !string.IsNullOrWhiteSpace(user.MainUsername) ? $"@{user.MainUsername}" : "";
                    string initials = GetInitials(name);
                    string color = GetColorForId(user.id);

                    result.Add(new TelegramChatItem
                    {
                        Id = user.id,
                        Title = name,
                        Username = username,
                        DisplaySubtitle = !string.IsNullOrEmpty(username) ? username : (user.phone ?? Loc.Get("Chat_Contact_Default")),
                        Initials = initials,
                        AvatarBgColor = color,
                        Peer = user,
                        IsUser = true
                    });
                    seenIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load contacts: {ex.Message}");
            }
        }

        return result.Take(30).ToList();
    }

    public static (string Message, MessageEntity[] Entities) FormatFileMessage(string? userComment, string fileName, string publicLink)
    {
        var sb = new System.Text.StringBuilder();
        string cleanComment = userComment?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(cleanComment))
        {
            sb.Append("comment: ").Append(cleanComment).Append('\n');
        }

        sb.Append("filename: ");
        int codeOffset = sb.Length;
        string safeFileName = fileName ?? string.Empty;
        sb.Append(safeFileName);
        int codeLength = safeFileName.Length;
        sb.Append('\n');
        sb.Append("link: ").Append(publicLink ?? string.Empty);

        string message = sb.ToString();
        var entities = new MessageEntity[]
        {
            new MessageEntityCode { offset = codeOffset, length = codeLength }
        };

        return (message, entities);
    }

    public async Task SendMessageAsync(InputPeer peer, string message, MessageEntity[]? entities = null)
    {
        var client = GetOrCreateClient();
        await client.LoginUserIfNeeded();
        await client.SendMessageAsync(peer, message, entities: entities);
    }

    public void Logout()
    {
        try
        {
            ResetClient();

            if (File.Exists(_settingsService.TelegramSessionPath))
            {
                File.Delete(_settingsService.TelegramSessionPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during logout: {ex.Message}");
        }
    }

    private static string FormatUserName(User user)
    {
        string name = $"{user.first_name} {user.last_name}".Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = !string.IsNullOrWhiteSpace(user.MainUsername) ? $"@{user.MainUsername}" : Loc.Get("Chat_NoName");
        }
        return name;
    }

    private static string GetInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "?";
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }
        return text.Trim()[..Math.Min(2, text.Trim().Length)].ToUpperInvariant();
    }

    private static string GetColorForId(long id)
    {
        int index = (int)(Math.Abs(id) % AvatarPalette.Length);
        return AvatarPalette[index];
    }

    public void Dispose()
    {
        ResetClient();
    }
}
