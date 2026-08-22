using System.Text;

namespace OogL2Client.Networking;

public static class L2MobiusPacketBuilder
{
    public static byte[] BuildAuthLoginRequest(string username, string password)
    {
        var payload = Encoding.ASCII.GetBytes($"{username}\0{password}\0");
        return BuildPacket(0x01, payload);
    }

    public static byte[] BuildSelectServerRequest(int serverId)
    {
        var payload = BitConverter.GetBytes(serverId);
        return BuildPacket(0x02, payload);
    }

    public static byte[] BuildPingPacket()
    {
        return BuildPacket(0x7F, Array.Empty<byte>());
    }

    public static byte[] BuildPacket(byte opCode, byte[] payload)
    {
        var packet = new List<byte>();

        packet.Add(0x00);
        packet.Add(opCode);
        packet.AddRange(payload);

        return packet.ToArray();
    }
}
