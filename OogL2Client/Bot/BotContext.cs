using OogL2Client.Models;

namespace OogL2Client.Bot;

public sealed class BotContext
{
    public AccountProfile? Account { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public int CharacterSlot { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int TargetObjectId { get; set; }
    public bool IsLoggedIn { get; set; }
    public bool IsInWorld { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object?> Properties { get; } = new();
}
