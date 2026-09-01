namespace OogL2Client.Models;

public sealed class SavedServerProfile
{
    public string Name { get; set; } = string.Empty;
    public string ServerHost { get; set; } = "127.0.0.1";
    public int LoginPort { get; set; } = 2106;
    public int GamePort { get; set; } = 7777;
    public int ServerId { get; set; } = 1;
    public int ProtocolVersion { get; set; } = 746;

    public string DisplayText => $"{Name} ({ServerHost}:{LoginPort}/{GamePort})";
}
