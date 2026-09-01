using System.Text.Json;
using OogL2Client.Models;

namespace OogL2Client.Storage;

public sealed class SavedServerStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public SavedServerStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<SavedServerProfile> Load()
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<SavedServerProfile>();
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SavedServerProfile>();
        }

        return JsonSerializer.Deserialize<List<SavedServerProfile>>(json, SerializerOptions) ?? new List<SavedServerProfile>();
    }

    public void Save(IReadOnlyCollection<SavedServerProfile> profiles)
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
