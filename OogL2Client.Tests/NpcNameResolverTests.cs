using OogL2Client.Storage;

namespace OogL2Client.Tests;

public class NpcNameResolverTests
{
    [Fact]
    public void BuildMapFromGameData_ShouldMergeLocalizationAndSpawnNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "oogl2-npc-map-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "lang", "en"));
        Directory.CreateDirectory(Path.Combine(root, "spawns", "Town"));

        File.WriteAllText(Path.Combine(root, "lang", "en", "NpcNameLocalisation.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <list>
              <localisation id="12077" name="Guard" />
            </list>
            """);

        File.WriteAllText(Path.Combine(root, "spawns", "Town", "TownNPCs.xml"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <list>
              <spawn name="TownNPCs">
                <npc id="30001" x="1" y="2" z="3" heading="0" /> <!-- Arnold -->
                <npc id="30002" x="1" y="2" z="3" heading="0" /> <!-- Jackson -->
              </spawn>
            </list>
            """);

        var map = NpcNameResolver.BuildMapFromGameData(root);

        Assert.Equal("Guard", map[12077]);
        Assert.Equal("Arnold", map[30001]);
        Assert.Equal("Jackson", map[30002]);

        Directory.Delete(root, recursive: true);
    }
}
