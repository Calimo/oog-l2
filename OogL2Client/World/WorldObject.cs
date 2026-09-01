namespace OogL2Client.World;

public enum WorldObjectType
{
    Player,
    NPC,
    Monster,
    Item,
    Unknown
}

public enum WorldObjectRelation
{
    Self,
    Friendly,
    Enemy,
    Neutral,
    Unknown
}

public class WorldObject
{
    public int ObjectId { get; set; }
    public int TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public WorldObjectType Type { get; set; } = WorldObjectType.Unknown;
    public WorldObjectRelation Relation { get; set; } = WorldObjectRelation.Unknown;
    public bool IsAggroed { get; set; }
    public int AggroTargetObjectId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Heading { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public bool IsAlive { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public int Level { get; set; }
    public int ClassId { get; set; }
}
