using System.Text.Json;
using OogL2Client.Models;

namespace OogL2Client.Storage;

public sealed class SavedCharacterStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public SavedCharacterStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<SavedCharacterProfile> Load()
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<SavedCharacterProfile>();
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SavedCharacterProfile>();
        }

        return JsonSerializer.Deserialize<List<SavedCharacterProfile>>(json, SerializerOptions) ?? new List<SavedCharacterProfile>();
    }

    public void Save(IReadOnlyCollection<SavedCharacterProfile> profiles)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(profiles, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }
}
