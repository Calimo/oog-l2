using OogL2Client.Networking;

namespace OogL2Client.Tests;

public class PacketBuilderTests
{
    [Fact]
    public void BuildAuthLoginRequest_ShouldContainLengthOpcodeAndRsaBlock()
    {
        var packet = L2MobiusPacketBuilder.BuildAuthLoginRequest("user", "pass");

        Assert.Equal(131, packet.Length);
        Assert.Equal((ushort)packet.Length, BitConverter.ToUInt16(packet, 0));
        Assert.Equal(L2MobiusPacketBuilder.AuthLoginOpcode, packet[2]);

        // Username at offset 94 in RSA block => 3 + 94 in framed packet.
        Assert.Equal((byte)'u', packet[97]);
        Assert.Equal((byte)'p', packet[111]);
    }

    [Fact]
    public void BuildSelectServerRequest_ShouldContainServerIdBytes()
    {
        var packet = L2MobiusPacketBuilder.BuildSelectServerRequest(7);

        Assert.Equal((ushort)packet.Length, BitConverter.ToUInt16(packet, 0));
        Assert.Equal(L2MobiusPacketBuilder.RequestPlayOpcode, packet[2]);
        Assert.Equal(0, BitConverter.ToInt32(packet, 3));
        Assert.Equal(0, BitConverter.ToInt32(packet, 7));
        Assert.Equal(7, packet[11]);
    }
}
