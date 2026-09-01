namespace OogL2Client.Models;

public sealed class SavedCharacterProfile
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ServerHost { get; set; } = "127.0.0.1";
    public int LoginPort { get; set; } = 2106;
    public int GamePort { get; set; } = 7777;
    public int ServerId { get; set; } = 1;
    public int ProtocolVersion { get; set; } = 746;
    public int CharacterSlot { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public int CharacterLevel { get; set; }
    public int CharacterClassId { get; set; }
    public string CharacterClassName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;

    public string DisplayText => $"{CharacterName} | Lv {CharacterLevel} | {GetClassLabel()} | {Username}{GetServerSuffix()}";

    private string GetClassLabel()
    {
        if (!string.IsNullOrWhiteSpace(CharacterClassName))
        {
            return CharacterClassName;
        }

        return $"Class {CharacterClassId}";
    }

    private string GetServerSuffix()
    {
        return string.IsNullOrWhiteSpace(ServerName) ? string.Empty : $" | {ServerName}";
    }

    public AccountProfile ToAccountProfile()
    {
        return new AccountProfile
        {
            Username = Username,
            Password = Password,
            ServerHost = ServerHost,
            LoginPort = LoginPort,
            GamePort = GamePort,
            ServerId = ServerId,
            ProtocolVersion = ProtocolVersion,
            CharacterSlot = CharacterSlot
        };
    }
}
