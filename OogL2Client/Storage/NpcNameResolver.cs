using System.Text.Json;
using System.Xml.Linq;

namespace OogL2Client.Storage;

public sealed class NpcNameResolver
{
    private readonly Dictionary<int, string> _names;

    public NpcNameResolver(string mapPath)
    {
        _names = LoadMap(mapPath);
    }

    public string? Resolve(int templateId)
    {
        if (templateId <= 0)
        {
            return null;
        }

        if (_names.TryGetValue(templateId, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return null;
    }

    public static Dictionary<int, string> BuildMapFromGameData(string gameDataRoot)
    {
        var result = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(gameDataRoot) || !Directory.Exists(gameDataRoot))
        {
            return result;
        }

        foreach (var localisationFile in Directory.EnumerateFiles(gameDataRoot, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                var doc = XDocument.Load(localisationFile);
                foreach (var element in doc.Descendants("localisation"))
                {
                    var idValue = element.Attribute("id")?.Value;
                    var name = element.Attribute("name")?.Value;
                    if (int.TryParse(idValue, out var id) && !string.IsNullOrWhiteSpace(name))
                    {
                        result[id] = name.Trim();
                    }
                }
            }
            catch
            {
                // Ignore malformed XML files in the data tree.
            }
        }

        foreach (var spawnFile in Directory.EnumerateFiles(gameDataRoot, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                var doc = XDocument.Load(spawnFile);
                foreach (var npcElement in doc.Descendants("npc"))
                {
                    var idValue = npcElement.Attribute("id")?.Value;
                    if (!int.TryParse(idValue, out var id))
                    {
                        continue;
                    }

                    var commentText = GetAdjacentComment(npcElement);
                    var candidate = commentText?.Trim();
                    if (!string.IsNullOrWhiteSpace(candidate) && !result.ContainsKey(id))
                    {
                        result[id] = candidate;
                    }

                    if (string.IsNullOrWhiteSpace(candidate) && npcElement.Attribute("name") is not null)
                    {
                        var named = npcElement.Attribute("name")?.Value?.Trim();
                        if (!string.IsNullOrWhiteSpace(named) && !result.ContainsKey(id))
                        {
                            result[id] = named;
                        }
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

    private static string? GetAdjacentComment(XElement element)
    {
        var next = element.NodesAfterSelf().OfType<XComment>().FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(next?.Value))
        {
            return next.Value;
        }

        var previous = element.PreviousNode as XComment;
        if (!string.IsNullOrWhiteSpace(previous?.Value))
        {
            return previous.Value;
        }

        return element.Nodes().OfType<XComment>().FirstOrDefault()?.Value;
    }

    private static Dictionary<int, string> LoadMap(string path)
    {
        var result = new Dictionary<int, string>();

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (raw is not null)
                {
                    foreach (var kv in raw)
                    {
                        if (int.TryParse(kv.Key, out var id) && !string.IsNullOrWhiteSpace(kv.Value))
                        {
                            result[id] = kv.Value.Trim();
                        }
                    }
                }
            }
        }

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
