using OogL2Client.Networking;

namespace OogL2Client.Tests;

public class PacketBuilderTests
{
    [Fact]
    public void BuildAuthLoginRequest_ShouldStartWithProtocolMarkerAndOpcode()
    {
        var packet = L2MobiusPacketBuilder.BuildAuthLoginRequest("user", "pass");

        Assert.Equal(0x00, packet[0]);
        Assert.Equal(0x01, packet[1]);
        Assert.Contains((byte)0x75, packet);
    }

    [Fact]
    public void BuildSelectServerRequest_ShouldContainServerIdBytes()
    {
        var packet = L2MobiusPacketBuilder.BuildSelectServerRequest(7);

        Assert.Equal(0x00, packet[0]);
        Assert.Equal(0x02, packet[1]);
        Assert.Equal((byte)7, packet[2]);
    }
}
