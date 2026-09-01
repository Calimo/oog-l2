using System.Text.Json;

namespace OogL2Client.Storage;

public sealed class ClassNameResolver
{
    private readonly Dictionary<int, string> _classNames;

    public ClassNameResolver(string classMapPath)
    {
        _classNames = LoadMap(classMapPath);
    }

    public string Resolve(int classId)
    {
        if (_classNames.TryGetValue(classId, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return $"Class {classId}";
    }

    private static Dictionary<int, string> LoadMap(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<int, string>();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<int, string>();
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (raw is null)
        {
            return new Dictionary<int, string>();
        }

        var result = new Dictionary<int, string>();
        foreach (var item in raw)
        {
            if (int.TryParse(item.Key, out var classId))
            {
                result[classId] = item.Value;
            }
        }

        return result;
    }
}
