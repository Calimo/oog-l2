using OogL2Client.Storage;

namespace OogL2Client.Tests;

public class SkillNameResolverTests
{
    [Fact]
    public void BuildMapFromSkills_ShouldReadSkillIdAndNameFromXml()
    {
        var root = Path.Combine(Path.GetTempPath(), "oogl2-skill-map-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "stats", "skills"));

        File.WriteAllText(Path.Combine(root, "stats", "skills", "00001-00099.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <list>
              <skill id="100" name="Power Strike" />
              <skill id="101" name="Mortal Strike" />
            </list>
            """);

        var map = SkillNameResolver.BuildMapFromGameData(root);

        Assert.Equal("Power Strike", map[100]);
        Assert.Equal("Mortal Strike", map[101]);

        Directory.Delete(root, recursive: true);
    }
}
