using TL;

namespace GDriveTelegramSender.Models;

public class TelegramChatItem
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplaySubtitle { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string AvatarBgColor { get; set; } = "#3B82F6";
    public InputPeer Peer { get; set; } = null!;
    public bool IsUser { get; set; }

    public override string ToString() => Title;
}
