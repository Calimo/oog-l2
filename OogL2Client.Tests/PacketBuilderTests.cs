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

    [Fact]
    public void BuildGameInteractionPackets_ShouldProduceStructuredPayloads()
    {
        var movePacket = L2MobiusPacketBuilder.BuildMoveToLocation(101, 202, 303, 0, 0, 0);
        var skillPacket = L2MobiusPacketBuilder.BuildUseSkill(1001, 42, 0, 0);
        var itemPacket = L2MobiusPacketBuilder.BuildUseItem(77, 1000, 42);
        var actionPacket = L2MobiusPacketBuilder.BuildAction(42, 10);
        var assistPacket = L2MobiusPacketBuilder.BuildAssistTarget(42);

        Assert.Equal((ushort)movePacket.Length, BitConverter.ToUInt16(movePacket, 0));
        Assert.Equal((ushort)skillPacket.Length, BitConverter.ToUInt16(skillPacket, 0));
        Assert.Equal((ushort)itemPacket.Length, BitConverter.ToUInt16(itemPacket, 0));
        Assert.Equal((ushort)actionPacket.Length, BitConverter.ToUInt16(actionPacket, 0));
        Assert.Equal((ushort)assistPacket.Length, BitConverter.ToUInt16(assistPacket, 0));

        Assert.NotEmpty(movePacket);
        Assert.NotEmpty(skillPacket);
        Assert.NotEmpty(itemPacket);
        Assert.NotEmpty(actionPacket);
        Assert.NotEmpty(assistPacket);
    }
}
