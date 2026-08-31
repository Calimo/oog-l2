using OogL2Client.World;
using System.Text;

namespace OogL2Client.Tests;

public class WorldParserTests
{
    [Fact]
    public void WorldPacketParser_ShouldMarkMonsterAsAggroedAndTrackTarget()
    {
        var worldState = new WorldState();
        worldState.SetSelf(new WorldObject { ObjectId = 100, Name = "Healer", Type = WorldObjectType.Player, X = 100, Y = 100, Z = 0 });
        worldState.Upsert(new WorldObject { ObjectId = 200, Name = "Wolf", Type = WorldObjectType.Monster, X = 101, Y = 101, Z = 0 });

        WorldPacketParser.ApplyTargetUpdate(worldState, 200, 100);

        var wolf = worldState.Get(200);
        Assert.NotNull(wolf);
        Assert.True(wolf!.IsAggroed);
        Assert.Equal(100, wolf.AggroTargetObjectId);
        Assert.Equal(100, worldState.GetAggroTargetObjectId(200));
        Assert.True(worldState.IsMonsterAggroedOn(200, 100));
    }

    [Fact]
    public void WorldState_ShouldFindThreatsTargetingAHealer()
    {
        var worldState = new WorldState();
        worldState.SetSelf(new WorldObject { ObjectId = 10, Name = "Healer", Type = WorldObjectType.Player, X = 100, Y = 100, Z = 0 });
        worldState.Upsert(new WorldObject { ObjectId = 20, Name = "Wolf", Type = WorldObjectType.Monster, X = 101, Y = 101, Z = 0, AggroTargetObjectId = 10 });
        worldState.Upsert(new WorldObject { ObjectId = 30, Name = "Skeleton", Type = WorldObjectType.Monster, X = 98, Y = 101, Z = 0, AggroTargetObjectId = 11 });

        var threats = worldState.ThreatsTargeting(10).ToList();

        Assert.Single(threats);
        Assert.Equal(20, threats[0].ObjectId);
    }

    [Fact]
    public void WorldPacketParser_ShouldApplyAttackOpcodeFromRawPayload()
    {
        var worldState = new WorldState();
        worldState.SetSelf(new WorldObject { ObjectId = 100, Name = "Healer", Type = WorldObjectType.Player, X = 100, Y = 100, Z = 0 });

        var payload = BuildPayload(WorldPacketParser.AttackOpcode, 222, 100, 0, 0);
        var applied = WorldPacketParser.TryApply(worldState, WorldPacketParser.AttackOpcode, payload, 100, "Healer", out var update);

        Assert.True(applied);
        Assert.True(update.Changed);
        Assert.True(update.ThreatChanged);
        Assert.True(worldState.IsMonsterAggroedOn(222, 100));
    }

    [Fact]
    public void WorldPacketParser_ShouldApplyMoveOpcodeFromRawPayload()
    {
        var worldState = new WorldState();
        worldState.Upsert(new WorldObject { ObjectId = 501, Name = "Mover", Type = WorldObjectType.Monster, X = 1, Y = 1, Z = 1 });

        var payload = BuildPayload(WorldPacketParser.MoveToLocationOpcode, 501, 1000, 2000, -3000);
        var applied = WorldPacketParser.TryApply(worldState, WorldPacketParser.MoveToLocationOpcode, payload, 0, string.Empty, out var update);

        Assert.True(applied);
        Assert.True(update.Changed);
        Assert.True(update.PositionChanged);

        var mover = worldState.Get(501);
        Assert.NotNull(mover);
        Assert.Equal(1000, mover!.X);
        Assert.Equal(2000, mover.Y);
        Assert.Equal(-3000, mover.Z);
    }

    [Fact]
    public void WorldPacketParser_ShouldIdentifySelfFromCharInfo()
    {
        var worldState = new WorldState();
        var payload = BuildCharInfoPayload(900, 910, 920, 1800, 777, "Healer");

        var applied = WorldPacketParser.TryApply(worldState, WorldPacketParser.CharInfoOpcode, payload, 777, "Healer", out var update);

        Assert.True(applied);
        Assert.True(update.Changed);
        Assert.True(update.PositionChanged);
        Assert.NotNull(worldState.Self);
        Assert.Equal(777, worldState.Self!.ObjectId);
        Assert.Equal(900, worldState.Self.X);
        Assert.Equal(910, worldState.Self.Y);
        Assert.Equal(920, worldState.Self.Z);
    }

    [Fact]
    public void WorldPacketParser_ShouldIdentifySelfFromUserInfo()
    {
        var worldState = new WorldState();
        var payload = BuildUserInfoPayload(1200, 1300, 1400, 777, "Healer");

        var applied = WorldPacketParser.TryApply(worldState, WorldPacketParser.UserInfoOpcode, payload, 777, "Healer", out var update);

        Assert.True(applied);
        Assert.True(update.Changed);
        Assert.True(update.PositionChanged);
        Assert.NotNull(worldState.Self);
        Assert.Equal(777, worldState.Self!.ObjectId);
        Assert.Equal(1200, worldState.Self.X);
        Assert.Equal(1300, worldState.Self.Y);
        Assert.Equal(1400, worldState.Self.Z);
    }

    private static byte[] BuildPayload(byte opcode, params int[] values)
    {
        var payload = new byte[1 + values.Length * 4];
        payload[0] = opcode;
        for (var i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(payload, 1 + i * 4);
        }

        return payload;
    }

    private static byte[] BuildCharInfoPayload(int x, int y, int z, int heading, int objectId, string name)
    {
        var bytes = new List<byte>(64)
        {
            WorldPacketParser.CharInfoOpcode
        };

        bytes.AddRange(BitConverter.GetBytes(x));
        bytes.AddRange(BitConverter.GetBytes(y));
        bytes.AddRange(BitConverter.GetBytes(z));
        bytes.AddRange(BitConverter.GetBytes(heading));
        bytes.AddRange(BitConverter.GetBytes(objectId));
        bytes.AddRange(Encoding.Unicode.GetBytes(name));
        bytes.AddRange(new byte[] { 0x00, 0x00 });

        return bytes.ToArray();
    }

    private static byte[] BuildUserInfoPayload(int x, int y, int z, int objectId, string name)
    {
        var bytes = new List<byte>(96)
        {
            WorldPacketParser.UserInfoOpcode
        };

        bytes.AddRange(BitConverter.GetBytes(x));
        bytes.AddRange(BitConverter.GetBytes(y));
        bytes.AddRange(BitConverter.GetBytes(z));
        bytes.AddRange(BitConverter.GetBytes(0)); // vehicle id placeholder
        bytes.AddRange(BitConverter.GetBytes(objectId));
        bytes.AddRange(Encoding.Unicode.GetBytes(name));
        bytes.AddRange(new byte[] { 0x00, 0x00 });
        return bytes.ToArray();
    }
}
