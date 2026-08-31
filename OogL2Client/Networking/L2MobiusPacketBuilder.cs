using System.Text;

namespace OogL2Client.Networking;

public static class L2MobiusPacketBuilder
{
    // Login server opcodes.
    public const byte AuthGameGuardOpcode = 0x07;
    public const byte AuthLoginOpcode = 0x00;
    public const byte RequestPlayOpcode = 0x02;
    public const byte RequestServerListOpcode = 0x05;

    // Game server opcodes.
    public const byte GameProtocolVersionOpcode = 0x00;
    public const byte GameAuthLoginOpcode = 0x08;
    public const byte GameEnterWorldOpcode = 0x03;
    public const byte GameCharacterSelectOpcode = 0x0D;

    // Common in-game interaction opcodes used by L2-style clients.
    public const byte MoveToLocationOpcode = 0x01;
    public const byte AttackOpcode = 0x02;
    public const byte UseSkillOpcode = 0x0A;
    public const byte UseItemOpcode = 0x12;
    public const byte ActionOpcode = 0x15;
    public const byte RequestTargetOpcode = 0x18;
    public const byte AssistTargetOpcode = 0x1C;
    public const byte StopMoveOpcode = 0x1E;

    // Incoming key packet / char select info for logging/parsing.
    public const byte KeyPacketOpcode = 0x00;
    public const byte CharSelectInfoOpcode = 0x13;
    public const byte PingOpcode = 0x7F;

    public static byte[] BuildAuthGameGuardPayload(int sessionId)
    {
        var body = new List<byte> { AuthGameGuardOpcode };
        body.AddRange(BitConverter.GetBytes(sessionId));
        body.AddRange(new byte[16]);
        return body.ToArray();
    }

    public static byte[] BuildAuthGameGuardRequest(int sessionId)
    {
        return BuildPacket(BuildAuthGameGuardPayload(sessionId));
    }

    public static byte[] BuildAuthLoginPayload(byte[] encryptedRsaBlock)
    {
        if (encryptedRsaBlock.Length != 128)
        {
            throw new ArgumentException("Encrypted RSA block must be exactly 128 bytes.", nameof(encryptedRsaBlock));
        }

        var body = new List<byte> { AuthLoginOpcode };
        body.AddRange(encryptedRsaBlock);
        return body.ToArray();
    }

    public static byte[] BuildAuthLoginRequest(string username, string password)
    {
        var rsaBlock = BuildAuthLoginCredentialBlock(username, password);
        return BuildPacket(BuildAuthLoginPayload(rsaBlock));
    }

    public static byte[] BuildAuthLoginCredentialBlock(string username, string password)
    {
        // Mobius login expects a 128-byte RSA block.
        // Username/password are read from offsets 94 and 108 after RSA decrypt.
        var rsaBlock = new byte[128];

        WriteFixedAscii(rsaBlock, 94, 14, username);
        WriteFixedAscii(rsaBlock, 108, 16, password);
        return rsaBlock;
    }

    public static byte[] BuildAuthLoginRequest(byte[] encryptedRsaBlock)
    {
        return BuildPacket(BuildAuthLoginPayload(encryptedRsaBlock));
    }

    public static byte[] BuildSelectServerRequest(int serverId)
    {
        return BuildRequestPlay(serverId);
    }

    public static byte[] BuildRequestServerList()
    {
        return BuildRequestServerList(0, 0);
    }

    public static byte[] BuildRequestServerList(int loginKey1, int loginKey2)
    {
        var body = new List<byte> { RequestServerListOpcode };
        body.AddRange(BitConverter.GetBytes(loginKey1));
        body.AddRange(BitConverter.GetBytes(loginKey2));
        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildRequestPlay(int serverId)
    {
        return BuildRequestPlay(serverId, 0, 0);
    }

    public static byte[] BuildRequestPlay(int serverId, int loginKey1, int loginKey2)
    {
        var body = new List<byte> { RequestPlayOpcode };
        body.AddRange(BitConverter.GetBytes(loginKey1));
        body.AddRange(BitConverter.GetBytes(loginKey2));
        body.Add((byte)serverId);
        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildGameProtocolVersion(int protocolVersion)
    {
        var body = new List<byte> { GameProtocolVersionOpcode };
        body.AddRange(BitConverter.GetBytes(protocolVersion));
        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildGameAuthLogin(string loginName, int playKey1, int playKey2, int loginKey1, int loginKey2)
    {
        var body = new List<byte> { GameAuthLoginOpcode };
        WriteNullTerminatedL2String(body, loginName.ToLowerInvariant());
        body.AddRange(BitConverter.GetBytes(playKey2));
        body.AddRange(BitConverter.GetBytes(playKey1));
        body.AddRange(BitConverter.GetBytes(loginKey1));
        body.AddRange(BitConverter.GetBytes(loginKey2));
        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildGameCharacterSelect(int slot)
    {
        var body = new List<byte> { GameCharacterSelectOpcode };
        body.AddRange(BitConverter.GetBytes(slot));
        body.AddRange(BitConverter.GetBytes((short)0));
        body.AddRange(BitConverter.GetBytes(0));
        body.AddRange(BitConverter.GetBytes(0));
        body.AddRange(BitConverter.GetBytes(0));
        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildGameEnterWorld()
    {
        var body = new List<byte> { GameEnterWorldOpcode };
        body.AddRange(new byte[104]);
        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildMoveToLocation(int x, int y, int z, int heading = 0, int originX = 0, int originY = 0, int originZ = 0)
    {
        return BuildGameInteractionPacket(MoveToLocationOpcode, x, y, z, heading, originX, originY, originZ);
    }

    public static byte[] BuildStopMove()
    {
        return BuildGameInteractionPacket(StopMoveOpcode, 0);
    }

    public static byte[] BuildAttack(int targetObjectId)
    {
        return BuildGameInteractionPacket(AttackOpcode, targetObjectId, 0, 0, 0);
    }

    public static byte[] BuildUseSkill(int skillId, int targetObjectId, int ctrlPressed = 0, int shiftPressed = 0)
    {
        return BuildGameInteractionPacket(UseSkillOpcode, skillId, targetObjectId, ctrlPressed, shiftPressed, 0, 0);
    }

    public static byte[] BuildUseItem(int objectId, int itemId, int targetObjectId = 0, int itemCount = 1)
    {
        return BuildGameInteractionPacket(UseItemOpcode, objectId, itemId, targetObjectId, itemCount, 0, 0);
    }

    public static byte[] BuildAction(int targetObjectId, int actionId, int actionType = 0)
    {
        return BuildGameInteractionPacket(ActionOpcode, targetObjectId, actionId, actionType, 0, 0, 0);
    }

    public static byte[] BuildRequestTarget(int targetObjectId)
    {
        return BuildGameInteractionPacket(RequestTargetOpcode, targetObjectId, 0, 0, 0);
    }

    public static byte[] BuildAssistTarget(int targetObjectId)
    {
        return BuildGameInteractionPacket(AssistTargetOpcode, targetObjectId, 0, 0, 0);
    }

    public static byte[] BuildPingPacket()
    {
        return BuildPacket(new[] { PingOpcode });
    }

    public static byte[] BuildGameInteractionPacket(byte opcode, params int[] values)
    {
        var body = new List<byte> { opcode };
        foreach (var value in values)
        {
            body.AddRange(BitConverter.GetBytes(value));
        }

        return BuildPacket(body.ToArray());
    }

    public static byte[] BuildPacket(byte[] body)
    {
        var packet = new List<byte>(body.Length + 2);
        var packetLength = (ushort)(body.Length + 2);
        packet.AddRange(BitConverter.GetBytes(packetLength));
        packet.AddRange(body);
        return packet.ToArray();
    }

    public static byte GetOpcode(byte[] framedPacket)
    {
        if (framedPacket.Length < 3)
        {
            throw new ArgumentException("Packet is too short.", nameof(framedPacket));
        }

        return framedPacket[2];
    }

    private static void WriteFixedAscii(byte[] target, int offset, int maxLength, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var count = Math.Min(maxLength, bytes.Length);
        Array.Copy(bytes, 0, target, offset, count);
    }

    private static void WriteNullTerminatedL2String(List<byte> buffer, string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value);
        buffer.AddRange(bytes);
        buffer.Add(0x00);
        buffer.Add(0x00);
    }
}
