using System.Xml.Linq;

namespace OogL2Client.Storage;

public sealed class SkillNameResolver
{
    private readonly Dictionary<int, string> _skills;

    public SkillNameResolver()
    {
        _skills = LoadMap();
    }

    public string? Resolve(int skillId)
    {
        if (skillId <= 0)
        {
            return null;
        }

        if (_skills.TryGetValue(skillId, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return null;
    }

    public IReadOnlyDictionary<int, string> GetAllSkills() => _skills;

    public static Dictionary<int, string> BuildMapFromGameData(string gameDataRoot)
    {
        var result = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(gameDataRoot) || !Directory.Exists(gameDataRoot))
        {
            return result;
        }

        foreach (var xmlFile in Directory.EnumerateFiles(gameDataRoot, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                var doc = XDocument.Load(xmlFile);
                foreach (var skillElement in doc.Descendants("skill"))
                {
                    var idValue = skillElement.Attribute("id")?.Value;
                    var name = skillElement.Attribute("name")?.Value?.Trim();
                    if (int.TryParse(idValue, out var skillId) && !string.IsNullOrWhiteSpace(name))
                    {
                        result.TryAdd(skillId, name);
                    }
                }
            }
            catch
            {
                // Ignore malformed XML files in the data tree.
            }
        }

        return result;
    }

    private static Dictionary<int, string> LoadMap()
    {
        var result = new Dictionary<int, string>();

        var candidateRoots = new[]
        {
            Path.Combine("D:\\", "L2Server", "game", "data"),
            Path.Combine("E:\\", "L2Server", "game", "data"),
            Path.Combine("D:\\", "L2Adrenaline", "game", "data"),
            Path.Combine("E:\\", "L2Adrenaline", "game", "data"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "D:", "L2Server", "game", "data")
        };

        foreach (var candidate in candidateRoots)
        {
            var fullServerRoot = Path.GetFullPath(candidate);
            if (Directory.Exists(fullServerRoot))
            {
                foreach (var kv in BuildMapFromGameData(fullServerRoot))
                {
                    result.TryAdd(kv.Key, kv.Value);
                }
            }
        }

        return result;
    }
}
