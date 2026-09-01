namespace OogL2Client.World;

public sealed record WorldPacketApplyResult(bool Changed, bool PositionChanged, bool ThreatChanged, string Summary);

public static class WorldPacketParser
{
    public const byte UserInfoOpcode = 0x04;
    public const byte MoveToLocationOpcode = 0x01;
    public const byte AttackOpcode = 0x05;
    public const byte DeleteObjectOpcode = 0x12;
    public const byte NpcInfoOpcode = 0x16;
    public const byte MagicSkillUseOpcode = 0x48;
    public const byte CharInfoOpcode = 0x31;

    public static bool TryApply(WorldState worldState, byte opcode, byte[] payload, int selfObjectId, string selfName, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);

        if (payload.Length <= 1)
        {
            return false;
        }

        return opcode switch
        {
            MoveToLocationOpcode => TryApplyMove(worldState, payload, out result),
            UserInfoOpcode => TryApplyUserInfo(worldState, payload, selfObjectId, selfName, out result),
            AttackOpcode => TryApplyAttack(worldState, payload, out result),
            DeleteObjectOpcode => TryApplyDelete(worldState, payload, out result),
            NpcInfoOpcode => TryApplyNpcInfo(worldState, payload, out result),
            CharInfoOpcode => TryApplyCharInfo(worldState, payload, selfObjectId, selfName, out result),
            MagicSkillUseOpcode => TryApplySkillUse(worldState, payload, out result),
            _ => TryApplyHeuristicPlayerInfo(opcode, worldState, payload, selfObjectId, selfName, out result)
        };
    }

    public static void ApplyTargetUpdate(WorldState worldState, int monsterObjectId, int targetObjectId)
    {
        if (monsterObjectId <= 0)
        {
            return;
        }

        var monster = worldState.Get(monsterObjectId) ?? new WorldObject
        {
            ObjectId = monsterObjectId,
            Type = WorldObjectType.Monster,
            Name = "Unknown Monster",
            IsVisible = true
        };

        monster.Type = WorldObjectType.Monster;
        monster.IsAggroed = targetObjectId > 0;
        monster.AggroTargetObjectId = monster.IsAggroed ? targetObjectId : 0;
        monster.Relation = monster.IsAggroed && targetObjectId == worldState.Self?.ObjectId ? WorldObjectRelation.Enemy : monster.Relation;
        worldState.Upsert(monster);
    }

    public static void ApplyObjectSpawn(WorldState worldState, WorldObject worldObject)
    {
        worldState.Upsert(worldObject);
    }

    public static void ApplyObjectDelete(WorldState worldState, int objectId)
    {
        worldState.Remove(objectId);
    }

    private static bool TryApplyDelete(WorldState worldState, byte[] payload, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var objectId))
        {
            return false;
        }

        var existing = worldState.Get(objectId);
        if (existing is null)
        {
            return false;
        }

        worldState.Remove(objectId);
        result = new WorldPacketApplyResult(true, false, existing.IsAggroed, $"Object removed: {objectId}");
        return true;
    }

    private static bool TryApplyMove(WorldState worldState, byte[] payload, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var objectId) ||
            !TryReadInt(payload, 5, out var toX) ||
            !TryReadInt(payload, 9, out var toY) ||
            !TryReadInt(payload, 13, out var toZ))
        {
            return false;
        }

        var obj = worldState.Get(objectId) ?? new WorldObject
        {
            ObjectId = objectId,
            Name = $"Object {objectId}",
            Type = WorldObjectType.Unknown,
            IsVisible = true
        };

        var moved = obj.X != toX || obj.Y != toY || obj.Z != toZ;
        obj.X = toX;
        obj.Y = toY;
        obj.Z = toZ;
        obj.LastSeenUtc = DateTime.UtcNow;
        worldState.Upsert(obj);

        result = new WorldPacketApplyResult(true, moved, false, $"Move update: {objectId} => ({toX}, {toY}, {toZ})");
        return true;
    }

    private static bool TryApplyAttack(WorldState worldState, byte[] payload, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var attackerObjectId) || !TryReadInt(payload, 5, out var targetObjectId))
        {
            return false;
        }

        if (attackerObjectId <= 0)
        {
            return false;
        }

        var before = worldState.GetAggroTargetObjectId(attackerObjectId);
        ApplyTargetUpdate(worldState, attackerObjectId, targetObjectId);
        var after = worldState.GetAggroTargetObjectId(attackerObjectId);
        var threatChanged = before != after;
        result = new WorldPacketApplyResult(true, false, threatChanged, $"Attack: {attackerObjectId} -> {targetObjectId}");
        return true;
    }

    private static bool TryApplySkillUse(WorldState worldState, byte[] payload, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var casterObjectId) || !TryReadInt(payload, 5, out var targetObjectId))
        {
            return false;
        }

        if (casterObjectId <= 0 || targetObjectId <= 0)
        {
            return false;
        }

        var caster = worldState.Get(casterObjectId);
        if (caster is null)
        {
            return false;
        }

        var wasAggro = caster.IsAggroed;
        var previousTarget = caster.AggroTargetObjectId;
        caster.IsAggroed = true;
        caster.AggroTargetObjectId = targetObjectId;
        caster.LastSeenUtc = DateTime.UtcNow;
        worldState.Upsert(caster);
        var threatChanged = !wasAggro || previousTarget != targetObjectId;
        result = new WorldPacketApplyResult(true, false, threatChanged, $"SkillUse: {casterObjectId} -> {targetObjectId}");
        return true;
    }

    private static bool TryApplyNpcInfo(WorldState worldState, byte[] payload, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var objectId) ||
            !TryReadInt(payload, 5, out var npcTemplateId) ||
            !TryReadInt(payload, 9, out var isAttackable) ||
            !TryReadInt(payload, 13, out var x) ||
            !TryReadInt(payload, 17, out var y) ||
            !TryReadInt(payload, 21, out var z) ||
            !TryReadInt(payload, 25, out var heading))
        {
            return false;
        }

        var obj = worldState.Get(objectId) ?? new WorldObject
        {
            ObjectId = objectId,
            IsVisible = true
        };

        obj.Type = isAttackable == 1 ? WorldObjectType.Monster : WorldObjectType.NPC;
        obj.TemplateId = NormalizeNpcTemplateId(npcTemplateId);
        obj.Relation = obj.Type == WorldObjectType.Monster ? WorldObjectRelation.Enemy : WorldObjectRelation.Neutral;
        obj.Name = string.IsNullOrWhiteSpace(obj.Name)
            ? (obj.Type == WorldObjectType.Monster ? "Monster" : "NPC")
            : obj.Name;
        obj.X = x;
        obj.Y = y;
        obj.Z = z;
        obj.Heading = heading;
        obj.IsVisible = true;
        obj.LastSeenUtc = DateTime.UtcNow;
        worldState.Upsert(obj);

        result = new WorldPacketApplyResult(true, true, false, $"NpcInfo: {objectId} at ({x}, {y}, {z})");
        return true;
    }

    private static int NormalizeNpcTemplateId(int rawNpcTemplateId)
    {
        if (rawNpcTemplateId == 0)
        {
            return 0;
        }

        var normalized = Math.Abs(rawNpcTemplateId);
        if (normalized >= 1_000_000)
        {
            normalized -= 1_000_000;
        }

        return normalized;
    }

    private static bool TryApplyCharInfo(WorldState worldState, byte[] payload, int selfObjectId, string selfName, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var x) ||
            !TryReadInt(payload, 5, out var y) ||
            !TryReadInt(payload, 9, out var z))
        {
            return false;
        }

        var candidateA = TryReadInt(payload, 17, out var objectIdA) ? objectIdA : 0;
        var candidateB = TryReadInt(payload, 13, out var objectIdB) ? objectIdB : 0;
        var nameFromA = TryReadL2String(payload, 21);
        var nameFromB = TryReadL2String(payload, 17);
        var objectId = PickBestObjectId(candidateA, candidateB, selfObjectId, worldState, nameFromA, nameFromB);
        if (objectId <= 0)
        {
            return false;
        }

        var heading = TryReadInt(payload, 13, out var readHeading) ? readHeading : 0;
        var name = objectId == candidateA ? nameFromA : nameFromB;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = TryReadL2String(payload, 21);
        }

        var obj = worldState.Get(objectId) ?? new WorldObject
        {
            ObjectId = objectId,
            IsVisible = true
        };

        obj.Type = WorldObjectType.Player;
        obj.Name = string.IsNullOrWhiteSpace(name) ? obj.Name : name;
        obj.X = x;
        obj.Y = y;
        obj.Z = z;
        obj.Heading = heading;
        obj.IsVisible = true;
        obj.LastSeenUtc = DateTime.UtcNow;

        var normalizedSelfName = selfName?.Trim() ?? string.Empty;
        var isSelf = (selfObjectId > 0 && objectId == selfObjectId) ||
                     (!string.IsNullOrWhiteSpace(normalizedSelfName) && string.Equals(obj.Name, normalizedSelfName, StringComparison.OrdinalIgnoreCase));

        obj.Relation = isSelf ? WorldObjectRelation.Self : WorldObjectRelation.Friendly;

        if (isSelf)
        {
            worldState.SetSelf(obj);
        }
        else
        {
            worldState.Upsert(obj);
        }

        result = new WorldPacketApplyResult(true, true, false, $"CharInfo: {obj.Name} ({objectId}) at ({x}, {y}, {z})");
        return true;
    }

    private static bool TryApplyUserInfo(WorldState worldState, byte[] payload, int selfObjectId, string selfName, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);
        if (!TryReadInt(payload, 1, out var x) ||
            !TryReadInt(payload, 5, out var y) ||
            !TryReadInt(payload, 9, out var z))
        {
            return false;
        }

        if (!TryReadInt(payload, 17, out var objectId) || objectId <= 0)
        {
            // Some implementations shift by one int (vehicle/object order variance).
            if (!TryReadInt(payload, 13, out objectId) || objectId <= 0)
            {
                return false;
            }
        }

        var name = TryReadL2String(payload, 21);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = selfName;
        }

        var obj = worldState.Get(objectId) ?? new WorldObject
        {
            ObjectId = objectId,
            IsVisible = true
        };

        obj.Type = WorldObjectType.Player;
        obj.Name = string.IsNullOrWhiteSpace(name) ? obj.Name : name;
        obj.X = x;
        obj.Y = y;
        obj.Z = z;
        obj.IsVisible = true;
        obj.LastSeenUtc = DateTime.UtcNow;

        var normalizedSelfName = selfName?.Trim() ?? string.Empty;
        var isSelf = (selfObjectId > 0 && objectId == selfObjectId) ||
                     (!string.IsNullOrWhiteSpace(normalizedSelfName) && string.Equals(obj.Name, normalizedSelfName, StringComparison.OrdinalIgnoreCase));

        obj.Relation = isSelf ? WorldObjectRelation.Self : WorldObjectRelation.Friendly;
        if (isSelf)
        {
            worldState.SetSelf(obj);
        }
        else
        {
            worldState.Upsert(obj);
        }

        result = new WorldPacketApplyResult(true, true, false, $"UserInfo: {obj.Name} ({objectId}) at ({x}, {y}, {z})");
        return true;
    }

    private static bool TryApplyHeuristicPlayerInfo(byte opcode, WorldState worldState, byte[] payload, int selfObjectId, string selfName, out WorldPacketApplyResult result)
    {
        result = new WorldPacketApplyResult(false, false, false, string.Empty);

        // Fallback for protocol variants where player info uses different opcodes than 0x31/0x04.
        if (!TryReadInt(payload, 1, out var x) ||
            !TryReadInt(payload, 5, out var y) ||
            !TryReadInt(payload, 9, out var z))
        {
            return false;
        }

        var objectId = TryReadInt(payload, 17, out var idA) && idA > 0 ? idA :
                       TryReadInt(payload, 13, out var idB) && idB > 0 ? idB : 0;
        if (objectId <= 0)
        {
            return false;
        }

        var candidateName = TryReadL2String(payload, 21);
        if (!LooksLikeCharacterName(candidateName))
        {
            candidateName = TryReadL2String(payload, 25);
        }

        var looksSelf = objectId == selfObjectId;
        var nameMatchesSelf = !string.IsNullOrWhiteSpace(selfName) &&
                              string.Equals(candidateName, selfName, StringComparison.OrdinalIgnoreCase);
        if (!looksSelf && !nameMatchesSelf && !LooksLikeCharacterName(candidateName))
        {
            return false;
        }

        var obj = worldState.Get(objectId) ?? new WorldObject
        {
            ObjectId = objectId,
            IsVisible = true
        };

        obj.Type = WorldObjectType.Player;
        obj.Name = string.IsNullOrWhiteSpace(candidateName) ? obj.Name : candidateName;
        obj.X = x;
        obj.Y = y;
        obj.Z = z;
        obj.IsVisible = true;
        obj.LastSeenUtc = DateTime.UtcNow;
        obj.Relation = looksSelf || nameMatchesSelf ? WorldObjectRelation.Self : WorldObjectRelation.Friendly;

        if (obj.Relation == WorldObjectRelation.Self)
        {
            worldState.SetSelf(obj);
        }
        else
        {
            worldState.Upsert(obj);
        }

        result = new WorldPacketApplyResult(true, true, false, $"PlayerInfoFallback 0x{opcode:X2}: {obj.Name} ({objectId}) at ({x}, {y}, {z})");
        return true;
    }

    private static int PickBestObjectId(int first, int second, int selfObjectId, WorldState worldState, string firstName, string secondName)
    {
        if (selfObjectId > 0)
        {
            if (first == selfObjectId)
            {
                return first;
            }

            if (second == selfObjectId)
            {
                return second;
            }
        }

        var firstLooksValid = first > 0 && LooksLikeCharacterName(firstName);
        var secondLooksValid = second > 0 && LooksLikeCharacterName(secondName);
        if (firstLooksValid && !secondLooksValid)
        {
            return first;
        }

        if (secondLooksValid && !firstLooksValid)
        {
            return second;
        }

        if (first > 0 && worldState.Get(first) is not null)
        {
            return first;
        }

        if (second > 0 && worldState.Get(second) is not null)
        {
            return second;
        }

        if (first > 0)
        {
            return first;
        }

        return second > 0 ? second : 0;
    }

    private static bool LooksLikeCharacterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Length < 2 || name.Length > 24)
        {
            return false;
        }

        return name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or ' ' or '\'' or '.');
    }

    private static bool TryReadInt(byte[] payload, int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset + 4 > payload.Length)
        {
            return false;
        }

        value = BitConverter.ToInt32(payload, offset);
        return true;
    }

    private static string TryReadL2String(byte[] payload, int offset)
    {
        if (offset >= payload.Length)
        {
            return string.Empty;
        }

        var chars = new List<char>();
        var pos = offset;

        while (pos + 1 < payload.Length)
        {
            var next = BitConverter.ToInt16(payload, pos);
            pos += 2;
            if (next == 0)
            {
                break;
            }

            chars.Add((char)next);
        }

        return chars.Count == 0 ? string.Empty : new string(chars.ToArray());
    }
}
